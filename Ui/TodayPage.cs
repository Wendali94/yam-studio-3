using System.Windows;
using System.Windows.Controls;
using YamStudio.Core;

namespace YamStudio.Views;

public class TodayPage : PageBase
{
    public override async Task Reload()
    {
        var all = await St.GetClientsAsync();
        string t = EthCal.TodayIso();
        long j = EthCal.IsoToJdn(t);
        var today = all.Where(c => c.Date == t).OrderBy(c => c.Time).ToList();
        var next = all.Where(c => { long x = EthCal.IsoToJdn(c.Date); return x > j && x <= j + 7; }).OrderBy(c => c.Date).ThenBy(c => c.Time).ToList();
        var wip = all.Where(c => c.Status is JobStatus.Selected or JobStatus.Editing or JobStatus.Review).OrderBy(c => c.Date).ToList();
        var waitSel = all.Count(c => c.Status == JobStatus.Shot);
        decimal due = all.Sum(c => Math.Max(0, c.Balance));

        var root = new StackPanel();
        var stats = new WrapPanel();
        stats.Children.Add(Ui.Stat(Loc.T("የዛሬ ስራዎች", "Today's jobs"), today.Count.ToString()));
        stats.Children.Add(Ui.Stat(Loc.T("ምርጫ የሚጠባበቁ", "Awaiting selection"), waitSel.ToString()));
        stats.Children.Add(Ui.Stat(Loc.T("በኤዲቲንግ ላይ", "In editing"), all.Count(c => c.Status is JobStatus.Selected or JobStatus.Editing).ToString()));
        stats.Children.Add(Ui.Stat(Loc.T("ለማረጋገጫ", "Waiting review"), all.Count(c => c.Status == JobStatus.Review).ToString()));
        if (C.Host) stats.Children.Add(Ui.Stat(Loc.T("ቀሪ ያልተከፈለ", "Unpaid balance"), Loc.Money(due)));
        root.Children.Add(stats);

        var todayBox = new StackPanel();
        todayBox.Children.Add(Ui.Txt($"{Loc.T("የዛሬ ቀጠሮዎች", "Today's appointments")} · {EthCal.DateWd(t)}", 13, Ui.Mut, true));
        if (today.Count == 0) todayBox.Children.Add(Ui.Txt(Loc.T("ዛሬ ቀጠሮ የለም", "No appointments today"), 14, Ui.Mut));
        foreach (var c in today) todayBox.Children.Add(JobRow(c, Reload, false));
        root.Children.Add(Ui.Card(todayBox));

        var wipBox = new StackPanel();
        wipBox.Children.Add(Ui.Txt(Loc.T("በሂደት ላይ ያሉ ስራዎች", "Work in progress"), 13, Ui.Mut, true));
        if (wip.Count == 0) wipBox.Children.Add(Ui.Txt("—", 14, Ui.Mut));
        foreach (var c in wip) wipBox.Children.Add(JobRow(c, Reload, true));
        root.Children.Add(Ui.Card(wipBox));

        var nextBox = new StackPanel();
        nextBox.Children.Add(Ui.Txt(Loc.T("በሚመጡት 7 ቀናት", "Next 7 days"), 13, Ui.Mut, true));
        if (next.Count == 0) nextBox.Children.Add(Ui.Txt("—", 14, Ui.Mut));
        foreach (var c in next) nextBox.Children.Add(JobRow(c, Reload, true));
        root.Children.Add(Ui.Card(nextBox));

        // Gentle reminders
        var late = all.Where(c => c.Status == JobStatus.Delivered ? false : (c.Status != JobStatus.Booked && EthCal.IsoToJdn(c.Date) < j - 7)).ToList();
        if (late.Count > 0 || all.Any(c => c.Status == JobStatus.Delivered && c.Balance > 0))
        {
            var rem = new StackPanel();
            rem.Children.Add(Ui.Txt(Loc.T("ማሳሰቢያ", "Reminders"), 13, Ui.Mut, true));
            foreach (var c in late)
                rem.Children.Add(Ui.Txt($"⏳ {c.Name} — {Loc.T("ከ 7 ቀን በላይ ሆኖታል", "over 7 days")} ({JobStatus.Label(c.Status)})", 14, Ui.Warn));
            if (C.Host)
                foreach (var c in all.Where(c => c.Status == JobStatus.Delivered && c.Balance > 0))
                    rem.Children.Add(Ui.Txt($"💳 {c.Name} — {Loc.T("ቀሪ", "Balance")} {Loc.Money(c.Balance)}", 14, Ui.Warn));
            root.Children.Add(Ui.Card(rem));
        }
        Set(root);
    }
}
