using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using YamStudio.Core;

namespace YamStudio.Views;

public class MainWindow : Window
{
    readonly AppSettings _s = SettingsStore.Current;
    readonly Ctx _c = new();
    string _page = "today";
    PageBase _cur;
    Grid _shell;
    StackPanel _side;
    TextBlock _title, _subtitle, _clock;
    ContentControl _host;
    Border _toast, _topBar;
    TextBlock _toastText;
    DispatcherTimer _clockTimer, _refresh;
    bool _clientMode;

    public MainWindow()
    {
        Title = "Yam PICTURES — Studio Management Suite";
        Width = 1360; Height = 840; MinWidth = 1024; MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ui.Bg; Foreground = Ui.Fg; FontFamily = Ui.Font;
        Ctx.I = _c;
        _c.Nav = Nav; _c.Rebuild = BuildShell; _c.ClientMode = SetClientMode; _c.Toast = ShowToast;

        StartServices();
        BuildShell();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();

        _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _refresh.Tick += async (_, _) =>
        {
            if (_cur != null && (_page == "today" || _page == "jobs")) { try { await _cur.Reload(); } catch { } }
        };
        _refresh.Start();

        Closing += OnClosing;
        Loaded += (_, _) => Nav("today");
    }

    void StartServices()
    {
        var local = new LocalStudio(_s);
        _c.Local = local;
        _c.Studio = local;
        _c.Ingest = new IngestService(local, _s);
        _c.Ingest.Start();
        _c.Backup = new BackupService(local, _s);
        _c.Backup.StartSchedule();
    }

    (string key, string icon, string label, string sub)[] Items() => new[]
    {
        ("today", "❖", Loc.T("ዳሽቦርድ / ዛሬ", "Dashboard"), Loc.T("የዛሬ ስራዎች፣ ገቢ እና አጠቃላይ እይታ", "Today's overview, analytics & active pipeline")),
        ("book", "📅", Loc.T("ምዝገባ እና ቀጠሮ", "Bookings & Clients"), Loc.T("አዲስ ደንበኛ መመዝገቢያ እና የቀጠሮ ዝርዝር", "Register clients & manage Ethiopian calendar bookings")),
        ("camera", "📷", Loc.T("ከካሜራ መቀበል", "Camera Intake"), Loc.T("ከ Sony ካሜራ ወይም ሚሞሪ ካርድ ፎቶ መቀበያ", "Live wireless FTP & SD card photo ingestion")),
        ("select", "❤", Loc.T("የደንበኛ ምርጫ", "Client Proofing"), Loc.T("ደንበኞች ፎቶ የሚመርጡበት ትልቅ ስክሪን", "Fullscreen gallery for client photo selection")),
        ("jobs", "🎬", Loc.T("ኤዲቲንግ እና ርክክብ", "Production & Delivery"), Loc.T("የፎቶ ኤዲቲንግ ሂደት እና ለደንበኛ ማስረከቢያ", "Track editing stages, final exports & USB delivery")),
        ("pay", "💳", Loc.T("ክፍያ እና ሪፖርት", "Finance & Reports"), Loc.T("ክፍያ መቀበያ፣ ደረሰኝ እና ወርሃዊ ሪፖርት", "Payments, printable receipts & monthly analytics")),
        ("settings", "⚙", Loc.T("ቅንብር", "Studio Settings"), Loc.T("ቋንቋ፣ የፋይል ማስቀመጫ እና Backup", "Storage path, camera FTP & automated backups")),
    };

    PageBase Make(string key) => key switch
    {
        "book" => new BookPage(),
        "camera" => new CameraPage(),
        "select" => new SelectPage(),
        "jobs" => new JobsPage(),
        "pay" => new PayPage(),
        "settings" => new SettingsPage(),
        _ => new TodayPage()
    };

    void BuildShell()
    {
        _shell = new Grid { Background = Ui.Bg };
        _shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(265) });
        _shell.ColumnDefinitions.Add(new ColumnDefinition());

        // Sidebar
        var sideGrid = new DockPanel { Margin = new Thickness(16, 20, 16, 18) };
        var sideBorder = new Border
        {
            Background = Ui.Panel,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = sideGrid
        };

        // Bottom Status Card in Sidebar
        var statusCard = new Border
        {
            Background = Ui.Panel2,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 12, 0, 0)
        };
        var stStack = new StackPanel();
        var langRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        langRow.Children.Add(Ui.Btn("አማ", () => SwitchLang("am"), Loc.Am ? 0 : 1, 11.5));
        langRow.Children.Add(Ui.Btn("ENG", () => SwitchLang("en"), !Loc.Am ? 0 : 1, 11.5));
        stStack.Children.Add(langRow);
        stStack.Children.Add(Ui.Txt($"📡 {_c.Ingest.FtpStatus} · 🟢 {Loc.T("ዝግጁ", "Online")}", 11.5, Ui.Ok, true));
        stStack.Children.Add(Ui.Txt($"📁 {_s.RootDir}", 11, Ui.Mut));
        statusCard.Child = stStack;
        DockPanel.SetDock(statusCard, Dock.Bottom);
        sideGrid.Children.Add(statusCard);

        // Navigation Items
        _side = new StackPanel();
        var logoBox = new Border
        {
            Padding = new Thickness(8, 4, 8, 16),
            Margin = new Thickness(0, 0, 0, 12),
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = Ui.Logo(1.0)
        };
        _side.Children.Add(logoBox);

        foreach (var (key, icon, label, _) in Items())
        {
            string k = key;
            bool active = k == _page;
            var indicator = new Border
            {
                Width = 4, Height = 20, CornerRadius = new CornerRadius(2),
                Background = active ? Ui.Gold : Brushes.Transparent,
                Margin = new Thickness(0, 0, 10, 0)
            };
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    indicator,
                    new TextBlock { Text = icon, Width = 26, FontSize = 15, Foreground = active ? Ui.Gold : Ui.Mut, VerticalAlignment = VerticalAlignment.Center },
                    Ui.Txt(label, 13.5, active ? Brushes.White : Ui.Mut, active)
                }
            };
            var b = new Button
            {
                Content = row,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 11, 12, 11),
                Margin = new Thickness(0, 0, 0, 5),
                Background = active ? Ui.Panel2 : Brushes.Transparent,
                BorderBrush = active ? Ui.LineGlow : Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = k,
                Template = Ui.Btn("", () => { }).Template
            };
            b.Click += (_, _) => Nav(k);
            _side.Children.Add(b);
        }
        sideGrid.Children.Add(_side);
        Grid.SetColumn(sideBorder, 0);
        _shell.Children.Add(sideBorder);

        // Main Area
        var main = new Grid();
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        main.RowDefinitions.Add(new RowDefinition());

        var head = new DockPanel { Margin = new Thickness(28, 18, 28, 14) };
        var rightHead = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(rightHead, Dock.Right);

        rightHead.Children.Add(Ui.Btn("＋ " + Loc.T("አዲስ ቀጠሮ", "New Booking"), () => Nav("book"), 0, 12.5));
        rightHead.Children.Add(Ui.Btn("📷 " + Loc.T("ካሜራ", "Intake"), () => Nav("camera"), 1, 12.5));

        _clock = Ui.Txt("", 12.5, Ui.Fg, true);
        _clock.TextAlignment = TextAlignment.Right;
        var clockBadge = new Border
        {
            Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(6, 0, 0, 0),
            Child = _clock
        };
        rightHead.Children.Add(clockBadge);
        head.Children.Add(rightHead);

        var titleStack = new StackPanel();
        _title = Ui.Txt("", 23, Ui.Fg, true);
        _subtitle = Ui.Txt("", 12.5, Ui.Mut);
        titleStack.Children.Add(_title);
        titleStack.Children.Add(_subtitle);
        head.Children.Add(titleStack);

        _topBar = new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Background = Ui.Bg, Child = head };
        main.Children.Add(_topBar);

        _host = new ContentControl { Margin = new Thickness(28, 20, 20, 16) };
        Grid.SetRow(_host, 1);
        main.Children.Add(_host);
        Grid.SetColumn(main, 1);
        _shell.Children.Add(main);

        // Toast Notification
        _toastText = Ui.Txt("", 13.5, Brushes.White, true);
        _toast = new Border
        {
            Background = Ui.AccentGrad,
            BorderBrush = Ui.Gold,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(99),
            Padding = new Thickness(22, 10, 22, 10),
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 28),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, Opacity = 0.6 },
            Child = _toastText
        };
        Grid.SetColumnSpan(_toast, 2);
        _shell.Children.Add(_toast);

        Content = _shell;
        UpdateClock();
        if (IsLoaded) Nav(_page);
    }

    void SwitchLang(string l)
    {
        _s.Lang = l; Loc.Lang = l; SettingsStore.Save();
        BuildShell();
    }

    void UpdateClock()
    {
        if (_clock == null) return;
        _clock.Text = $"🗓 {EthCal.DateWd(EthCal.TodayIso())}   ⏱ {EthCal.Time(EthCal.NowHm())}";
    }

    public void Nav(string key)
    {
        var items = Items();
        if (!items.Any(i => i.key == key)) key = "today";
        _page = key;
        if (_clientMode && key != "select") SetClientMode(false);

        foreach (var b in _side.Children.OfType<Button>())
        {
            bool act = (string)b.Tag == key;
            b.Background = act ? Ui.Panel2 : Brushes.Transparent;
            b.BorderBrush = act ? Ui.LineGlow : Brushes.Transparent;
            if (b.Content is StackPanel sp && sp.Children.Count >= 3)
            {
                ((Border)sp.Children[0]).Background = act ? Ui.Gold : Brushes.Transparent;
                ((TextBlock)sp.Children[1]).Foreground = act ? Ui.Gold : Ui.Mut;
                ((TextBlock)sp.Children[2]).Foreground = act ? Brushes.White : Ui.Mut;
            }
        }

        var item = items.First(i => i.key == key);
        _title.Text = item.label;
        _subtitle.Text = item.sub;
        _cur = Make(key);
        _host.Content = _cur;
        _ = LoadCurrent();
    }

    async Task LoadCurrent()
    {
        var p = _cur;
        try { await p.Reload(); }
        catch (Exception e)
        {
            if (p != _cur) return;
            var sp = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
            sp.Children.Add(Ui.Txt("⚠ " + e.Message, 15, Ui.Warn));
            sp.Children.Add(new Border { Height = 12 });
            sp.Children.Add(Ui.Btn(Loc.T("እንደገና ሞክር", "Try again"), () => Nav(_page)));
            _host.Content = sp;
        }
    }

    void SetClientMode(bool on)
    {
        _clientMode = on;
        _shell.ColumnDefinitions[0].Width = on ? new GridLength(0) : new GridLength(265);
        _topBar.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        if (on) WindowState = WindowState.Maximized;
    }

    void ShowToast(string text)
    {
        _toastText.Text = text;
        _toast.Visibility = Visibility.Visible;
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        t.Tick += (_, _) => { _toast.Visibility = Visibility.Collapsed; t.Stop(); };
        t.Start();
    }

    void OnClosing(object s, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            if (_s.BackupOnExit && !string.IsNullOrWhiteSpace(_s.BackupDir))
            {
                Cursor = System.Windows.Input.Cursors.Wait;
                _c.Backup.Run();
            }
            _c.Ingest.Stop();
            _c.Backup.Stop();
        }
        catch { }
        Application.Current.Shutdown();
    }
}
