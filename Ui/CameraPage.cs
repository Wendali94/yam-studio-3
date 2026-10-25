using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using YamStudio.Core;

namespace YamStudio.Views;

/// <summary>PC1: choose who is being photographed; photos from the Sony arrive by FTP and are filed under that client.</summary>
public class CameraPage : PageBase
{
    List<Client> _cands = new();
    ComboBox _combo;
    TextBlock _count, _state;
    WrapPanel _thumbs;
    readonly HashSet<int> _shown = new();
    readonly SemaphoreSlim _gate = new(3);
    Action<int, Photo> _handler;
    DispatcherTimer _timer;
    bool _building;

    public CameraPage()
    {
        _handler = (cid, p) => Dispatcher.InvokeAsync(() =>
        {
            if (cid == C.Ingest.ActiveClientId) { AddThumb(p); UpdateCount(); }
        });
        Loaded += (_, _) =>
        {
            C.Ingest.Filed += _handler;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += (_, _) => UpdateCount();
            _timer.Start();
        };
        Unloaded += (_, _) => { C.Ingest.Filed -= _handler; _timer?.Stop(); };
    }

    public override async Task Reload()
    {
        _building = true;
        var all = await St.GetClientsAsync();
        _cands = all.Where(c => c.Status is JobStatus.Booked or JobStatus.Shot)
                    .OrderBy(c => c.Date == EthCal.TodayIso() ? 0 : 1).ThenBy(c => c.Date).ThenBy(c => c.Time).ToList();

        _combo = new ComboBox { FontSize = 15, Padding = new Thickness(8, 7, 8, 7), MinWidth = 340, Margin = new Thickness(0, 0, 0, 10) };
        _combo.Items.Add(Loc.T("— ደንበኛ ምረጥ —", "— choose client —"));
        foreach (var c in _cands) _combo.Items.Add($"{c.Name} · {EthCal.Date(c.Date)} {EthCal.Time(c.Time)}");
        int idx = _cands.FindIndex(c => c.Id == C.Ingest.ActiveClientId);
        _combo.SelectedIndex = idx + 1;
        _combo.SelectionChanged += (_, _) =>
        {
            if (_building) return;
            int i = _combo.SelectedIndex - 1;
            C.Ingest.ActiveClientId = i >= 0 ? _cands[i].Id : 0;
            Safe(LoadThumbs);
            UpdateCount();
        };

        _state = Ui.Txt("", 14, Ui.Ok, true);
        _count = Ui.Txt("", 16, Ui.Fg, true);
        _thumbs = new WrapPanel();

        var top = new StackPanel();
        top.Children.Add(Ui.Txt(Loc.T("የሚነሳ ደንበኛ", "Active shoot"), 13, Ui.Mut, true));
        top.Children.Add(new Border { Height = 8 });
        top.Children.Add(_combo);
        top.Children.Add(_state);
        top.Children.Add(new Border { Height = 6 });
        top.Children.Add(_count);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        row.Children.Add(Ui.Btn("❤ " + Loc.T("ለደንበኛ ምርጫ አዘጋጅ", "Go to client selection"), () =>
        {
            if (C.Ingest.ActiveClientId <= 0) { MessageBox.Show(Loc.T("ደንበኛ ይምረጡ", "Choose a client")); return; }
            C.Nav("select");
        }));
        row.Children.Add(Ui.Btn("🖼 " + Loc.T("ፎቶ ከፋይል ጨምር", "Add photos from files"), () => Import(false), 1));
        row.Children.Add(Ui.Btn("📁 " + Loc.T("ከ folder / SD ካርድ ጨምር", "Add from folder / SD card"), () => Import(true), 1));
        top.Children.Add(row);

        var help = new StackPanel();
        help.Children.Add(Ui.Txt(Loc.T("የካሜራ ግንኙነት (Sony a7 IV → ይህ PC)", "Camera connection (Sony a7 IV → this PC)"), 13, Ui.Mut, true));
        help.Children.Add(new Border { Height = 6 });
        string ips = string.Join("  /  ", LocalIps());
        help.Children.Add(Ui.Txt($"IP: {ips}\nPort: {C.S.FtpPort}\n{Loc.T("ስም", "User")}: {C.S.FtpUser}\n{Loc.T("የይለፍ ቃል", "Password")}: {C.S.FtpPass}\n{Loc.T("Passive mode: ON · Secure connection: OFF", "Passive mode: ON · Secure connection: OFF")}", 14));
        help.Children.Add(Ui.Txt(Loc.T(
            "ካሜራ: Menu → Network → Transfer/Remote → FTP Transfer Func. → Server Setting. ትክክለኛው የሜኑ ስሞች በካሜራው firmware ሊለያዩ ይችላሉ።",
            "Camera: Menu → Network → Transfer/Remote → FTP Transfer Func. → Server Setting. Exact menu names may differ by firmware."), 12, Ui.Mut));

        var panel = new StackPanel();
        panel.Children.Add(Ui.Card(top));
        panel.Children.Add(Ui.Card(help));
        var th = new StackPanel();
        th.Children.Add(Ui.Txt(Loc.T("የደረሱ ፎቶዎች", "Received photos"), 13, Ui.Mut, true));
        th.Children.Add(new Border { Height = 8 });
        th.Children.Add(_thumbs);
        panel.Children.Add(Ui.Card(th));
        Set(panel);
        _building = false;
        await LoadThumbs();
        UpdateCount();
    }

    static IEnumerable<string> LocalIps()
    {
        try
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)).Select(a => a.ToString()).ToList();
        }
        catch { return new[] { "?" }; }
    }

    void UpdateCount()
    {
        if (_count == null) return;
        int id = C.Ingest.ActiveClientId;
        if (id <= 0)
        {
            int waiting = C.Ingest.WaitingInInbox();
            _state.Foreground = Ui.Warn;
            _state.Text = "○ " + Loc.T("ደንበኛ አልተመረጠም", "No client chosen") +
                (waiting > 0 ? $" · {waiting} " + Loc.T("ፎቶ እየጠበቀ ነው", "photos waiting") : "");
            _count.Text = "";
            return;
        }
        _state.Foreground = Ui.Ok;
        _state.Text = "● " + Loc.T($"ካሜራ እየተጠበቀ ነው ({C.Ingest.FtpStatus})", $"Waiting for camera ({C.Ingest.FtpStatus})");
        _count.Text = $"{_shown.Count} " + Loc.T("ፎቶዎች ደርሰዋል እና ተረጋግጠዋል", "photos received & verified");
    }

    async Task LoadThumbs()
    {
        _thumbs.Children.Clear(); _shown.Clear();
        int id = C.Ingest.ActiveClientId;
        if (id <= 0) return;
        foreach (var p in await St.GetPhotosAsync(id)) AddThumb(p);
        UpdateCount();
    }

    void AddThumb(Photo p)
    {
        if (!_shown.Add(p.Id)) return;
        var img = new Image { Width = 130, Height = 87, Stretch = Stretch.UniformToFill };
        var b = new Border
        {
            Margin = new Thickness(0, 0, 8, 8), Background = Ui.Black, CornerRadius = new CornerRadius(7), ClipToBounds = true,
            Child = new Grid { Children = { img, new TextBlock { Text = p.DisplayName, Foreground = Brushes.White, FontSize = 10, Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), VerticalAlignment = VerticalAlignment.Bottom, Padding = new Thickness(4, 1, 4, 1) } } }
        };
        _thumbs.Children.Add(b);
        _ = LoadImg(img, p.Id);
    }

    async Task LoadImg(Image img, int id)
    {
        await _gate.WaitAsync();
        try
        {
            var bytes = await St.GetImageAsync(id, true);
            if (bytes != null) img.Source = Ui.ToImage(bytes);
        }
        catch { }
        finally { _gate.Release(); }
    }

    void Import(bool folder)
    {
        int id = C.Ingest.ActiveClientId;
        if (id <= 0) { MessageBox.Show(Loc.T("መጀመሪያ ደንበኛ ይምረጡ", "Choose a client first")); return; }
        string[] files;
        if (folder)
        {
            var d = new Microsoft.Win32.OpenFolderDialog();
            if (d.ShowDialog() != true) return;
            files = Directory.GetFiles(d.FolderName, "*", SearchOption.AllDirectories);
        }
        else
        {
            var d = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Filter = "Photos|*.jpg;*.jpeg;*.png;*.arw;*.dng|All|*.*" };
            if (d.ShowDialog() != true) return;
            files = d.FileNames;
        }
        files = files.Where(LocalStudio.IsPhotoFile).OrderBy(f => f).ToArray();
        Safe(async () =>
        {
            int n = 0;
            foreach (var f in files)
            {
                var p = await Task.Run(() => C.Local.Ingest(id, f, false));
                if (p != null) { AddThumb(p); n++; UpdateCount(); }
            }
            C.Toast?.Invoke($"✔ {n} " + Loc.T("ፎቶ ተጨመረ", "photos added"));
        });
    }
}
