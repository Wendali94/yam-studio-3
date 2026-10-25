using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using YamStudio.Core;

namespace YamStudio.Views;

public class PayPage : PageBase
{
    int _ry, _rm; // report month (Ethiopian)

    public override async Task Reload()
    {
        var all = await St.GetClientsAsync();
        if (_ry == 0) { var e = EthCal.IsoToEth(EthCal.TodayIso()); _ry = e.y; _rm = e.m; }

        var root = new StackPanel();
        decimal collected = all.Sum(c => c.Paid), due = all.Sum(c => Math.Max(0, c.Balance));
        var stats = new WrapPanel();
        stats.Children.Add(Ui.Stat(Loc.T("ጠቅላላ የተሰበሰበ", "Total collected"), Loc.Money(collected)));
        stats.Children.Add(Ui.Stat(Loc.T("ቀሪ ያልተከፈለ", "Outstanding"), Loc.Money(due)));
        root.Children.Add(stats);

        var g = new Grid();
        string[] heads = { Loc.T("ደንበኛ", "Client"), Loc.T("ጠቅላላ", "Total"), Loc.T("የተከፈለ", "Paid"), Loc.T("ቀሪ", "Balance"), "" };
        double[] w = { 3, 1.3, 1.3, 1.3, 2.2 };
        foreach (var x in w) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(x, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition());
        for (int i = 0; i < heads.Length; i++) { var t = Ui.Txt(heads[i], 12, Ui.Mut, true); Grid.SetColumn(t, i); g.Children.Add(t); }
        int r = 1;
        foreach (var c in all.Take(120))
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var name = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
            name.Children.Add(Ui.Txt(c.Name, 14, Ui.Fg, true));
            name.Children.Add(Ui.Txt($"{EthCal.Date(c.Date)} · {JobTypes.Name(c.Type)}", 11, Ui.Mut));
            Add(g, name, 0, r);
            Add(g, Ui.Txt(Loc.Money(c.Price), 13), 1, r);
            Add(g, Ui.Txt(Loc.Money(c.Paid), 13, Ui.Ok), 2, r);
            Add(g, Ui.Txt(Loc.Money(c.Balance), 13, c.Balance > 0 ? Ui.Warn : Ui.Ok), 3, r);
            var b = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var cc = c;
            if (c.Balance > 0)
                b.Children.Add(Ui.Btn("+ " + Loc.T("ክፍያ", "Payment"), () => Safe(async () =>
                {
                    string v = Ui.Prompt(Loc.T("ክፍያ", "Payment"), Loc.T("የክፍያ መጠን (ብር)", "Payment amount (ETB)"), ((long)cc.Balance).ToString());
                    if (v != null && decimal.TryParse(v, out var a) && a > 0)
                    { await St.AddPaymentAsync(cc.Id, a, EthCal.TodayIso()); await Reload(); }
                }), 0, 12));
            b.Children.Add(Ui.Btn("🧾", () => Safe(async () =>
            {
                var fresh = (await St.GetClientsAsync()).First(x => x.Id == cc.Id);
                PrintReceipt(fresh);
            }), 1, 12));
            Add(g, b, 4, r);
            r++;
        }
        root.Children.Add(Ui.Card(g));
        root.Children.Add(await ReportCard());
        Set(root);
    }

    static void Add(Grid g, UIElement e, int col, int row)
    {
        Grid.SetColumn((FrameworkElement)e, col); Grid.SetRow((FrameworkElement)e, row);
        g.Children.Add(e);
    }

    async Task<UIElement> ReportCard()
    {
        var range = EthCal.MonthRange(_ry, _rm);
        var rows = await St.GetPaymentsAsync(range.from, range.to);
        var sp = new StackPanel();
        sp.Children.Add(Ui.Txt(Loc.T("ወርሃዊ ሪፖርት (የኢትዮጵያ ወር)", "Monthly report (Ethiopian month)"), 13, Ui.Mut, true));
        sp.Children.Add(new Border { Height = 8 });
        var pick = new StackPanel { Orientation = Orientation.Horizontal };
        var months = Ui.Combo(Loc.Am ? EthCal.MonthAm : EthCal.MonthEn, _rm - 1, 150);
        int ty = EthCal.IsoToEth(EthCal.TodayIso()).y;
        var years = Ui.Combo(Enumerable.Range(ty - 3, 6).Select(y => y.ToString()), _ry - (ty - 3), 90);
        months.SelectionChanged += (_, _) => { _rm = months.SelectedIndex + 1; Safe(Reload); };
        years.SelectionChanged += (_, _) => { if (years.SelectedIndex >= 0) { _ry = ty - 3 + years.SelectedIndex; Safe(Reload); } };
        pick.Children.Add(months); pick.Children.Add(new Border { Width = 8 }); pick.Children.Add(years);
        pick.Children.Add(new Border { Width = 12 });
        pick.Children.Add(Ui.Btn("⬇ CSV (Excel)", () => ExportCsv(rows), 1));
        sp.Children.Add(pick);
        sp.Children.Add(Ui.Txt($"{Loc.T("ጠቅላላ ገቢ", "Total income")}: {Loc.Money(rows.Sum(x => x.Amount))}  ·  {rows.Count} {Loc.T("ክፍያዎች", "payments")}", 16, Ui.Gold, true));
        sp.Children.Add(new Border { Height = 8 });
        foreach (var grp in rows.GroupBy(x => x.JobType).OrderByDescending(x => x.Sum(y => y.Amount)))
            sp.Children.Add(Ui.Txt($"{JobTypes.Name(grp.Key)}: {Loc.Money(grp.Sum(x => x.Amount))}", 14));
        return Ui.Card(sp);
    }

    void ExportCsv(List<PaymentRow> rows)
    {
        var d = new Microsoft.Win32.SaveFileDialog { Filter = "CSV|*.csv", FileName = $"income_{_ry}-{_rm:00}.csv" };
        if (d.ShowDialog() != true) return;
        var sb = new StringBuilder();
        sb.AppendLine("Date,Client,Job,Amount");
        foreach (var x in rows)
        {
            string nm = x.ClientName.Replace("\"", "\"\"");
            string job = JobTypes.Name(x.JobType);
            sb.AppendLine(EthCal.FileDate(x.Date) + ",\"" + nm + "\",\"" + job + "\"," + x.Amount.ToString("0"));
        }
        File.WriteAllText(d.FileName, sb.ToString(), new UTF8Encoding(true));
        C.Toast?.Invoke("✔ " + d.FileName);
    }

    static void PrintReceipt(Client c)
    {
        var doc = new FlowDocument { PagePadding = new Thickness(50), FontFamily = Ui.Font, FontSize = 14, ColumnWidth = 480 };
        Paragraph P(string t, double size = 14, bool bold = false) => new Paragraph(new Run(t)) { FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Margin = new Thickness(0, 0, 0, 6) };
        doc.Blocks.Add(P("YAM PICTURE", 26, true));
        doc.Blocks.Add(P($"{Loc.T("የክፍያ ደረሰኝ", "Payment receipt")} · {EthCal.Date(EthCal.TodayIso())}", 13));
        doc.Blocks.Add(P($"{c.Name}\n{c.Phone}\n{JobTypes.Name(c.Type)} · {EthCal.Date(c.Date)}", 15, true));
        foreach (var p in c.Payments) doc.Blocks.Add(P($"{EthCal.Date(p.Date)}    {Loc.Money(p.Amount)}"));
        doc.Blocks.Add(P($"{Loc.T("ጠቅላላ", "Total")}: {Loc.Money(c.Price)}\n{Loc.T("የተከፈለ", "Paid")}: {Loc.Money(c.Paid)}", 15));
        doc.Blocks.Add(P($"{Loc.T("ቀሪ", "Balance")}: {Loc.Money(c.Balance)}", 18, true));
        doc.Blocks.Add(P(Loc.T("እናመሰግናለን!", "Thank you!"), 14));
        var pd = new System.Windows.Controls.PrintDialog();
        if (pd.ShowDialog() == true)
            pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "YAM receipt");
    }
}
