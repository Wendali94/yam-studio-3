using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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
        decimal collected = all.Sum(c => c.Paid);

        var root = new StackPanel();

        var stats = new WrapPanel();
        stats.Children.Add(Ui.Stat(Loc.T("የዛሬ ቀጠሮዎች", "Today's Shoots"), today.Count.ToString(), "📅", Ui.Blue));
        stats.Children.Add(Ui.Stat(Loc.T("ምርጫ የሚጠባበቁ", "Awaiting Proofing"), waitSel.ToString(), "❤", Ui.Warn));
        stats.Children.Add(Ui.Stat(Loc.T("በኤዲቲንግ ላይ", "In Production"), wip.Count.ToString(), "🎬", Ui.Gold));
        stats.Children.Add(Ui.Stat(Loc.T("ጠቅላላ ገቢ", "Total Revenue"), Loc.Money(collected), "💎", Ui.Ok));
        stats.Children.Add(Ui.Stat(Loc.T("ቀሪ ክፍያ", "Pending Balance"), Loc.Money(due), "⏳", Ui.Bad));
        root.Children.Add(stats);

        var mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.55, GridUnitType.Star) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var leftCol = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        var rightCol = new StackPanel();

        var todayBox = new StackPanel();
        todayBox.Children.Add(Ui.Txt($"⚡ {Loc.T("የዛሬ ቀጠሮዎች", "Today's Schedule")} · {EthCal.DateWd(t)}", 14, Ui.Fg, true));
        todayBox.Children.Add(new Border { Height = 12 });
        if (today.Count == 0) todayBox.Children.Add(Ui.Txt(Loc.T("ዛሬ የተያዘ ቀጠሮ የለም", "No appointments scheduled for today"), 13.5, Ui.Mut));
        foreach (var c in today) todayBox.Children.Add(JobRow(c, Reload, false));
        leftCol.Children.Add(Ui.Card(todayBox));

        var wipBox = new StackPanel();
        wipBox.Children.Add(Ui.Txt($"🎬 {Loc.T("በሂደት ላይ ያሉ ስራዎች", "Active Production Queue")}", 14, Ui.Fg, true));
        wipBox.Children.Add(new Border { Height = 12 });
        if (wip.Count == 0) wipBox.Children.Add(Ui.Txt(Loc.T("በኤዲቲንግ ላይ ያለ ስራ የለም", "No active editing jobs"), 13.5, Ui.Mut));
        foreach (var c in wip) wipBox.Children.Add(JobRow(c, Reload, true));
        leftCol.Children.Add(Ui.Card(wipBox));

        rightCol.Children.Add(BuildAnalyticsCard(all, j));

        var nextBox = new StackPanel();
        nextBox.Children.Add(Ui.Txt($"🗓 {Loc.T("በሚመጡት 7 ቀናት", "Upcoming 7 Days")}", 14, Ui.Fg, true));
        nextBox.Children.Add(new Border { Height = 12 });
        if (next.Count == 0) nextBox.Children.Add(Ui.Txt(Loc.T("ምንም ቀጠሮ የለም", "No upcoming bookings"), 13.5, Ui.Mut));
        foreach (var c in next) nextBox.Children.Add(JobRow(c, Reload, true));
        rightCol.Children.Add(Ui.Card(nextBox));

        var late = all.Where(c => c.Status != JobStatus.Delivered && c.Status != JobStatus.Booked && EthCal.IsoToJdn(c.Date) < j - 7).ToList();
        if (late.Count > 0 || all.Any(c => c.Status == JobStatus.Delivered && c.Balance > 0))
        {
            var rem = new StackPanel();
            rem.Children.Add(Ui.Txt("🔔 " + Loc.T("ማሳሰቢያዎች", "Studio Alerts"), 14, Ui.Warn, true));
            rem.Children.Add(new Border { Height = 8 });
            foreach (var c in late)
                rem.Children.Add(Ui.Txt($"• {c.Name} — {Loc.T("ከ 7 ቀን በላይ ሆኖታል", "Overdue > 7 days")} ({JobStatus.Label(c.Status)})", 13, Ui.Warn));
            foreach (var c in all.Where(c => c.Status == JobStatus.Delivered && c.Balance > 0))
                rem.Children.Add(Ui.Txt($"• {c.Name} — {Loc.T("ቀሪ ክፍያ", "Unpaid")} {Loc.Money(c.Balance)}", 13, Ui.Bad));
            rightCol.Children.Add(Ui.Card(rem));
        }

        Grid.SetColumn(leftCol, 0);
        Grid.SetColumn(rightCol, 1);
        mainGrid.Children.Add(leftCol);
        mainGrid.Children.Add(rightCol);
        root.Children.Add(mainGrid);

        Set(root);
    }

    UIElement BuildAnalyticsCard(List<Client> all, long todayJdn)
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.Txt("📈 " + Loc.T("የሳምንቱ እንቅስቃሴ (Analytics)", "7-Day Studio Pulse"), 14, Ui.Fg, true));
        sp.Children.Add(Ui.Txt(Loc.T("ባሉት 7 ቀናት የተሰሩ ስራዎች እና ክፍያዎች", "Booking & payment activity across the last 7 days"), 11.5, Ui.Mut));
        sp.Children.Add(new Border { Height = 14 });

        var canvas = new Canvas { Height = 120, Width = 340, HorizontalAlignment = HorizontalAlignment.Left };
        double[] vals = new double[7];
        for (int i = 0; i < 7; i++)
        {
            string d = EthCal.JdnToIso(todayJdn - (6 - i));
            vals[i] = all.Count(c => c.Date == d) * 2 + all.SelectMany(c => c.Payments).Count(p => p.Date == d);
        }
        double max = Math.Max(4, vals.Max());

        var pts = new PointCollection();
        var areaPts = new PointCollection { new Point(0, 110) };
        for (int i = 0; i < 7; i++)
        {
            double x = i * (320.0 / 6.0) + 10;
            double y = 100 - (vals[i] / max) * 80;
            pts.Add(new Point(x, y));
            areaPts.Add(new Point(x, y));
        }
        areaPts.Add(new Point(330, 110));

        var polyArea = new Polygon
        {
            Points = areaPts,
            Fill = new LinearGradientBrush(Color.FromArgb(95, 168, 85, 247), Color.FromArgb(0, 99, 102, 241), new Point(0, 0), new Point(0, 1))
        };
        var polyLine = new Polyline
        {
            Points = pts,
            Stroke = Ui.Gold,
            StrokeThickness = 3
        };
        canvas.Children.Add(polyArea);
        canvas.Children.Add(polyLine);

        foreach (var pt in pts)
        {
            var dot = new Ellipse { Width = 8, Height = 8, Fill = Brushes.White, Stroke = Ui.Gold, StrokeThickness = 2 };
            Canvas.SetLeft(dot, pt.X - 4);
            Canvas.SetTop(dot, pt.Y - 4);
            canvas.Children.Add(dot);
        }

        sp.Children.Add(canvas);
        return Ui.Card(sp);
    }
}
