using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using YamStudio.Core;

namespace YamStudio.Views;

public class MainWindow : Window
{
    readonly AppSettings _s = SettingsStore.Current;
    readonly Ctx _c = new();
    LanApi _api;
    string _page = "today";
    PageBase _cur;
    Grid _shell;
    StackPanel _side;
    TextBlock _title, _clock, _mode;
    ContentControl _host;
    Border _toast;
    TextBlock _toastText;
    DispatcherTimer _clockTimer, _refresh;
    bool _clientMode;

    public MainWindow()
    {
        Title = "YAM PICTURE — Studio Manager";
        Width = 1280; Height = 800; MinWidth = 960; MinHeight = 600;
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
        if (_s.Mode == "client")
        {
            _c.Studio = new RemoteStudio(_s);
            return;
        }
        var local = new LocalStudio(_s);
        _c.Local = local; _c.Studio = local;
        _c.Ingest = new IngestService(local, _s);
        _c.Ingest.Start();
        _c.Backup = new BackupService(local, _s);
        _c.Backup.StartSchedule();
        _api = new LanApi(local, _s);
        Loaded += (_, _) =>
        {
            if (!_api.Start())
                MessageBox.Show(Loc.T($"PC2 ን ለማገናኘት port {_s.ApiPort} መክፈት አልተቻለም። ሌላ ፕሮግራም እየተጠቀመበት ወይም Windows Firewall ከልክሎት ሊሆን ይችላል።",
                    $"Could not open port {_s.ApiPort} for PC2. Another program may be using it, or Windows Firewall blocked it."));
        };
    }

    (string key, string icon, string label)[] Items()
    {
        var all = new[]
        {
            ("today", "🏠", Loc.T("የዛሬ ስራ", "Today")),
            ("book", "📅", Loc.T("ምዝገባ እና ቀጠሮ", "Register & Book")),
            ("camera", "📷", Loc.T("ከካሜራ መቀበል", "Camera Intake")),
            ("select", "❤", Loc.T("የደንበኛ ምርጫ", "Client Selection")),
            ("jobs", "🛠", Loc.T("ስራዎች / ኤዲቲንግ", "Jobs / Editing")),
            ("pay", "💳", Loc.T("ክፍያ እና ሪፖርት", "Payments & Reports")),
            ("settings", "⚙", Loc.T("ቅንብር", "Settings")),
        };
        return _c.Host ? all : all.Where(i => i.Item1 is "today" or "jobs" or "settings").ToArray();
    }

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
        _shell = new Grid();
        _shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        _shell.ColumnDefinitions.Add(new ColumnDefinition());

        _side = new StackPanel { Margin = new Thickness(12) };
        var sideBorder = new Border { Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 1, 0), Child = _side };
        _side.Children.Add(Ui.Txt("YAM PICTURE", 20, Ui.Gold, true));
        _side.Children.Add(Ui.Txt("Studio Manager", 12, Ui.Mut));
        _side.Children.Add(new Border { Height = 16 });
        foreach (var (key, icon, label) in Items())
        {
            string k = key;
            var b = new Button
            {
                Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = icon, Width = 28, FontSize = 16, Foreground = Ui.Fg }, Ui.Txt(label, 14, null) } },
                HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 4),
                Background = k == _page ? Ui.Panel2 : Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Ui.Fg, Cursor = System.Windows.Input.Cursors.Hand,
                Tag = k
            };
            b.Click += (_, _) => Nav(k);
            _side.Children.Add(b);
        }
        _mode = Ui.Txt(_c.Host ? "PC1 · " + Loc.T("ዋና", "Main") : "PC2 · " + Loc.T("ኤዲቲንግ", "Editing"), 12, Ui.Mut);
        _mode.Margin = new Thickness(4, 24, 0, 0);
        _side.Children.Add(_mode);
        if (_c.Host)
        {
            string s = _c.Ingest.FtpStatus;
            _side.Children.Add(Ui.Txt($"📡 {s}  ·  🔗 {_s.ApiPort}", 11, Ui.Mut));
        }
        Grid.SetColumn(sideBorder, 0);
        _shell.Children.Add(sideBorder);

        var main = new Grid { Margin = new Thickness(0) };
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        main.RowDefinitions.Add(new RowDefinition());
        var head = new DockPanel { Margin = new Thickness(24, 14, 24, 10) };
        _clock = Ui.Txt("", 13, Ui.Mut);
        _clock.TextAlignment = TextAlignment.Right;
        DockPanel.SetDock(_clock, Dock.Right);
        head.Children.Add(_clock);
        _title = Ui.Txt("", 22, Ui.Fg, true);
        head.Children.Add(_title);
        var hb = new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = head };
        main.Children.Add(hb);
        _host = new ContentControl { Margin = new Thickness(24, 16, 16, 12) };
        Grid.SetRow(_host, 1);
        main.Children.Add(_host);
        Grid.SetColumn(main, 1);
        _shell.Children.Add(main);

        _toastText = Ui.Txt("", 14, Brushes.Black, true);
        _toast = new Border
        {
            Background = Ui.Ok, CornerRadius = new CornerRadius(99), Padding = new Thickness(20, 9, 20, 9), Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 24), Child = _toastText
        };
        Grid.SetColumnSpan(_toast, 2);
        _shell.Children.Add(_toast);

        Content = _shell;
        UpdateClock();
        if (IsLoaded) Nav(_page);
    }

    void UpdateClock()
    {
        if (_clock == null) return;
        _clock.Text = $"{EthCal.DateWd(EthCal.TodayIso())}\n{EthCal.Time(EthCal.NowHm())}";
    }

    public void Nav(string key)
    {
        if (!Items().Any(i => i.key == key)) key = "today";
        _page = key;
        if (_clientMode && key != "select") SetClientMode(false);
        foreach (var b in _side.Children.OfType<Button>())
            b.Background = (string)b.Tag == key ? Ui.Panel2 : Brushes.Transparent;
        _title.Text = Items().First(i => i.key == key).label;
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
            sp.Children.Add(Ui.Txt("⚠ " + (_c.Host ? e.Message : Loc.T("ከ PC1 ጋር መገናኘት አልተቻለም። ገመድ/ኔትወርክ እና PC1 መከፈቱን ያረጋግጡ።", "Cannot reach PC1. Check cable/network and that PC1 is running.")), 15, Ui.Warn));
            sp.Children.Add(new Border { Height = 12 });
            sp.Children.Add(Ui.Btn(Loc.T("እንደገና ሞክር", "Try again"), () => Nav(_page)));
            _host.Content = sp;
        }
    }

    void SetClientMode(bool on)
    {
        _clientMode = on;
        _shell.ColumnDefinitions[0].Width = on ? new GridLength(0) : new GridLength(240);
        _title.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        _clock.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        if (on) WindowState = WindowState.Maximized;
    }

    void ShowToast(string text)
    {
        _toastText.Text = text;
        _toast.Visibility = Visibility.Visible;
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.4) };
        t.Tick += (_, _) => { _toast.Visibility = Visibility.Collapsed; t.Stop(); };
        t.Start();
    }

    void OnClosing(object s, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            if (_c.Host)
            {
                if (_s.BackupOnExit && !string.IsNullOrWhiteSpace(_s.BackupDir))
                {
                    Cursor = System.Windows.Input.Cursors.Wait;
                    _c.Backup.Run();
                }
                _c.Ingest.Stop(); _c.Backup.Stop(); _api?.Stop();
            }
        }
        catch { }
        Application.Current.Shutdown();
    }
}
