using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YamStudio.Core;

namespace YamStudio.Views;

public abstract class PageBase : Grid
{
    protected Ctx C => Ctx.I;
    protected IStudio St => Ctx.I.Studio;

    public abstract Task Reload();

    protected void Set(UIElement content, bool scroll = true)
    {
        Children.Clear();
        if (scroll)
            Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = content, Padding = new Thickness(0, 0, 10, 0) });
        else Children.Add(content);
    }

    protected static async void Safe(Func<Task> f)
    {
        try { await f(); }
        catch (Exception e) { MessageBox.Show(Loc.T("ስህተት: ", "Error: ") + e.Message); }
    }

    public static void OpenFolder(string path)
    {
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        else MessageBox.Show(Loc.T("folder አልተገኘም", "Folder not found"));
    }

    protected List<Button> JobButtons(Client c, Func<Task> reload)
    {
        var l = new List<Button>();
        void Status(string s) => Safe(async () => { await St.SetStatusAsync(c.Id, s); C.Toast?.Invoke("✔ " + JobStatus.Label(s)); await reload(); });

        if (c.Status == JobStatus.Booked)
            l.Add(Ui.Btn("📷 " + Loc.T("ካሜራ መቀበል", "Shoot / Intake"), () => { C.Ingest.ActiveClientId = c.Id; C.Nav("camera"); }, 0, 12.5));
        if (c.Status == JobStatus.Shot)
            l.Add(Ui.Btn("❤ " + Loc.T("ምርጫ", "Proofing"), () => { C.Ingest.ActiveClientId = c.Id; C.Nav("select"); }, 0, 12.5));
        if (c.Status == JobStatus.Selected)
            l.Add(Ui.Btn("▶ " + Loc.T("ኤዲቲንግ ጀምር", "Start Editing"), () => Status(JobStatus.Editing), 0, 12.5));
        if (c.Status == JobStatus.Editing)
            l.Add(Ui.Btn("✔ " + Loc.T("ለማረጋገጫ ላክ", "Mark for Review"), () => Status(JobStatus.Review), 0, 12.5));
        if (c.Status == JobStatus.Review)
        {
            l.Add(Ui.Btn("✔ " + Loc.T("አጽድቅ እና አስረክብ", "Approve & Deliver"), () => Status(JobStatus.Delivered), 0, 12.5));
            l.Add(Ui.Btn("↩ " + Loc.T("ማስተካከያ", "Re-edit"), () => Status(JobStatus.Editing), 1, 12.5));
        }
        return l;
    }

    protected UIElement JobRow(Client c, Func<Task> reload, bool showDate)
    {
        var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(showDate ? 155 : 115) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Avatar circle
        string init = string.IsNullOrWhiteSpace(c.Name) ? "Y" : c.Name.Trim().Substring(0, 1).ToUpper();
        var av = new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(18),
            Background = Ui.Panel2, BorderBrush = Ui.LineGlow, BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock { Text = init, Foreground = Ui.Gold, FontWeight = FontWeights.Bold, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        g.Children.Add(av);

        var when = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        if (showDate) when.Children.Add(Ui.Txt(EthCal.Date(c.Date), 13, Ui.Gold, true));
        when.Children.Add(Ui.Txt(EthCal.Time(c.Time), 12.5, showDate ? Ui.Mut : Ui.Gold, !showDate));
        Grid.SetColumn(when, 1);
        g.Children.Add(when);

        var who = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        who.Children.Add(Ui.Txt(c.Name, 14.5, Ui.Fg, true));
        who.Children.Add(Ui.Txt($"{JobTypes.Name(c.Type)}  ·  📞 {c.Phone}  ·  📷 {c.PhotoCount}", 12, Ui.Mut));
        Grid.SetColumn(who, 2);
        g.Children.Add(who);

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(Ui.Chip(c.Status));
        foreach (var b in JobButtons(c, reload)) right.Children.Add(b);
        Grid.SetColumn(right, 3);
        g.Children.Add(right);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) { Opacity = 0.03 },
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 8),
            Child = g
        };
    }
}
