using System.Windows;
using System.Windows.Controls;
using YamStudio.Core;

namespace YamStudio.Views;

public class BookPage : PageBase
{
    List<Client> _all = new();
    Client _edit;

    public override async Task Reload()
    {
        _all = await St.GetClientsAsync();
        Build();
    }

    void Build()
    {
        var c0 = _edit;
        var name = Ui.Input(c0?.Name ?? "");
        var phone = Ui.Input(c0?.Phone ?? "");
        var type = Ui.Combo(Enumerable.Range(0, JobTypes.All.Length).Select(JobTypes.Name), c0?.Type ?? 0);
        var date = new EthDatePicker(c0?.Date ?? EthCal.TodayIso());
        var time = new EthTimePicker(c0?.Time ?? "09:00");
        var price = Ui.Input(c0 != null ? ((long)c0.Price).ToString() : "");
        var dep = Ui.Input("");
        var notes = Ui.Input(c0?.Notes ?? "");
        notes.AcceptsReturn = true; notes.Height = 60; notes.TextWrapping = TextWrapping.Wrap;
        var hist = Ui.Txt("", 12, Ui.Warn);

        phone.TextChanged += (_, _) =>
        {
            string p = new string(phone.Text.Where(char.IsDigit).ToArray());
            var prev = _all.Where(x => x.Phone == p && x.Id != (c0?.Id ?? 0)).ToList();
            if (prev.Count > 0 && p.Length >= 9)
            {
                hist.Text = Loc.T($"ቀድሞ ደንበኛ ነው · {prev.Count} ስራ · {prev[0].Name}", $"Returning client · {prev.Count} jobs · {prev[0].Name}");
                if (string.IsNullOrWhiteSpace(name.Text)) name.Text = prev[0].Name;
            }
            else hist.Text = "";
        };

        var form = new StackPanel();
        form.Children.Add(Ui.Txt(c0 == null ? Loc.T("አዲስ ደንበኛ እና ቀጠሮ", "New client & appointment") : Loc.T("ቀጠሮ ማስተካከያ", "Edit appointment"), 13, Ui.Mut, true));
        form.Children.Add(new Border { Height = 10 });
        form.Children.Add(Ui.Field(Loc.T("ሙሉ ስም", "Full name"), name));
        form.Children.Add(Ui.Field(Loc.T("ስልክ ቁጥር", "Phone"), phone));
        form.Children.Add(hist);
        form.Children.Add(Ui.Field(Loc.T("የስራ አይነት", "Job type"), type));
        form.Children.Add(Ui.Field(Loc.T("ቀን (ኢትዮጵያ)", "Date (Ethiopian)"), date));
        form.Children.Add(Ui.Field(Loc.T("ሰዓት (ኢትዮጵያ)", "Time (Ethiopian)"), time));
        form.Children.Add(Ui.Field(Loc.T("ጠቅላላ ዋጋ (ብር)", "Total price (ETB)"), price));
        if (c0 == null) form.Children.Add(Ui.Field(Loc.T("ቅድመ ክፍያ (ብር)", "Deposit (ETB)"), dep));
        form.Children.Add(Ui.Field(Loc.T("ማስታወሻ", "Notes"), notes));

        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        btns.Children.Add(Ui.Btn("💾 " + Loc.T("አስቀምጥ", "Save"), () => Safe(async () =>
        {
            string p = new string(phone.Text.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(name.Text) || p.Length < 9)
            { MessageBox.Show(Loc.T("ስም እና ትክክለኛ ስልክ ቁጥር ያስገቡ", "Enter a name and a valid phone number")); return; }
            decimal.TryParse(price.Text, out var pr);
            decimal.TryParse(dep.Text, out var dp);
            string iso = date.Value, tm = time.Value;
            if (_all.Any(x => x.Date == iso && x.Time == tm && x.Status == JobStatus.Booked && x.Id != (c0?.Id ?? 0)) &&
                MessageBox.Show(Loc.T("በዚህ ሰዓት ሌላ ቀጠሮ አለ። ቢሆንም ይቀጥል?", "Another appointment exists at this time. Continue anyway?"),
                    "YAM", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            var c = c0 ?? new Client();
            c.Name = name.Text.Trim(); c.Phone = p; c.Type = type.SelectedIndex; c.Date = iso; c.Time = tm;
            c.Price = pr; c.Notes = notes.Text;
            if (c0 == null && dp > 0) c.Payments.Add(new Payment { Amount = dp, Date = EthCal.TodayIso() });
            await St.SaveClientAsync(c);
            C.Toast?.Invoke("✔ " + Loc.T("ቀጠሮ ተይዟል", "Appointment saved"));
            _edit = null;
            await Reload();
        })));
        if (c0 != null) btns.Children.Add(Ui.Btn(Loc.T("አዲስ", "New"), () => { _edit = null; Build(); }, 1));
        form.Children.Add(btns);

        var list = new StackPanel();
        list.Children.Add(Ui.Txt(Loc.T("ሁሉም ቀጠሮዎች", "All appointments"), 13, Ui.Mut, true));
        var search = Ui.Input("");
        search.Margin = new Thickness(0, 8, 0, 8);
        list.Children.Add(search);
        var rows = new StackPanel();
        list.Children.Add(rows);

        void Fill()
        {
            rows.Children.Clear();
            string q = search.Text.Trim().ToLowerInvariant();
            foreach (var c in _all.Where(x => q == "" || x.Name.ToLowerInvariant().Contains(q) || x.Phone.Contains(q)).Take(80))
            {
                var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var d = new StackPanel();
                d.Children.Add(Ui.Txt(EthCal.Date(c.Date), 13, Ui.Gold, true));
                d.Children.Add(Ui.Txt(EthCal.Time(c.Time), 12, Ui.Mut));
                g.Children.Add(d);
                var w = new StackPanel();
                w.Children.Add(Ui.Txt(c.Name, 14, Ui.Fg, true));
                w.Children.Add(Ui.Txt($"{c.Phone} · {JobTypes.Name(c.Type)}", 12, Ui.Mut));
                Grid.SetColumn(w, 1); g.Children.Add(w);
                var r = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                r.Children.Add(Ui.Chip(c.Status));
                var cc = c;
                r.Children.Add(Ui.Btn("✎", () => { _edit = cc; Build(); }, 1, 13));
                if (c.Status == JobStatus.Booked)
                    r.Children.Add(Ui.Btn("✕", () => Safe(async () =>
                    {
                        if (MessageBox.Show(Loc.T("ይሰረዝ?", "Delete?") + " " + cc.Name, "YAM", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                        await St.DeleteClientAsync(cc.Id);
                        await Reload();
                    }), 2, 13));
                Grid.SetColumn(r, 2); g.Children.Add(r);
                rows.Children.Add(new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = g });
            }
        }
        search.TextChanged += (_, _) => Fill();
        Fill();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 380 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        var left = Ui.Card(form); left.Margin = new Thickness(0, 0, 14, 14);
        grid.Children.Add(left);
        var right = Ui.Card(list); Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        Set(grid);
    }
}
