using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
            Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = content, Padding = new Thickness(0, 0, 8, 0) });
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

    /// <summary>The buttons that make sense for this job on this computer (PC1 = reception/selection, PC2 = editing).</summary>
    protected List<Button> JobButtons(Client c, Func<Task> reload)
    {
        var l = new List<Button>();
        void Status(string s) => Safe(async () => { await St.SetStatusAsync(c.Id, s); C.Toast?.Invoke("✔ " + JobStatus.Label(s)); await reload(); });

        if (C.Host && c.Status == JobStatus.Booked)
            l.Add(Ui.Btn("📷 " + Loc.T("ካሜራ መቀበል", "Receive photos"), () => { C.Ingest.ActiveClientId = c.Id; C.Nav("camera"); }, 0, 13));
        if (C.Host && c.Status == JobStatus.Shot)
            l.Add(Ui.Btn("❤ " + Loc.T("ምርጫ", "Selection"), () => { C.Ingest.ActiveClientId = c.Id; C.Nav("select"); }, 0, 13));
        if (c.Status == JobStatus.Selected)
            l.Add(Ui.Btn("▶ " + Loc.T("ኤዲቲንግ ጀምር", "Start editing"), () => Status(JobStatus.Editing), 0, 13));
        if (c.Status == JobStatus.Editing)
            l.Add(Ui.Btn("✔ " + Loc.T("ለማረጋገጫ ላክ", "Send for review"), () => Status(JobStatus.Review), 0, 13));
        if (C.Host && c.Status == JobStatus.Review)
        {
            l.Add(Ui.Btn("✔ " + Loc.T("አጽድቅ እና አስረክብ", "Approve & deliver"), () => Status(JobStatus.Delivered), 0, 13));
            l.Add(Ui.Btn("↩ " + Loc.T("ማስተካከያ", "Return"), () => Status(JobStatus.Editing), 1, 13));
        }
        return l;
    }

    protected UIElement JobRow(Client c, Func<Task> reload, bool showDate)
    {
        var g = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(showDate ? 150 : 110) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var when = new StackPanel();
        if (showDate) when.Children.Add(Ui.Txt(EthCal.Date(c.Date), 13, Ui.Gold, true));
        when.Children.Add(Ui.Txt(EthCal.Time(c.Time), 13, showDate ? Ui.Mut : Ui.Gold, !showDate));
        g.Children.Add(when);
        var who = new StackPanel();
        who.Children.Add(Ui.Txt(c.Name, 15, Ui.Fg, true));
        who.Children.Add(Ui.Txt($"{JobTypes.Name(c.Type)} · {c.Phone}", 12, Ui.Mut));
        Grid.SetColumn(who, 1);
        g.Children.Add(who);
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(Ui.Chip(c.Status));
        foreach (var b in JobButtons(c, reload)) right.Children.Add(b);
        Grid.SetColumn(right, 2);
        g.Children.Add(right);
        return new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = g };
    }
}
