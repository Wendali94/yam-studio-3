using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YamStudio.Core;

namespace YamStudio.Views;

public class SelectPage : PageBase
{
    Client _client;
    List<Photo> _photos = new(), _view = new();
    List<Client> _cands = new();
    int _idx;
    bool _onlyHearts, _clientMode;
    Image _big;
    Button _heart;
    TextBlock _counter, _name;
    StackPanel _strip;
    ScrollViewer _stripScroll;
    readonly Dictionary<int, BitmapImage> _cache = new();
    readonly Dictionary<int, Border> _cells = new();
    KeyEventHandler _key;
    Window _win;

    public SelectPage()
    {
        _key = (_, e) =>
        {
            if (e.OriginalSource is TextBox) return;
            if (e.Key == Key.Right || e.Key == Key.Down) { Go(_idx + 1); e.Handled = true; }
            else if (e.Key == Key.Left || e.Key == Key.Up) { Go(_idx - 1); e.Handled = true; }
            else if (e.Key == Key.Space || e.Key == Key.Enter) { ToggleHeart(); e.Handled = true; }
            else if (e.Key == Key.Escape && _clientMode) { SetClientMode(false); e.Handled = true; }
        };
        Loaded += (_, _) => { _win = Window.GetWindow(this); if (_win != null) _win.PreviewKeyDown += _key; };
        Unloaded += (_, _) => { if (_win != null) _win.PreviewKeyDown -= _key; if (_clientMode) C.ClientMode(false); };
    }

    public override async Task Reload()
    {
        var all = await St.GetClientsAsync();
        _cands = all.Where(c => c.Status is JobStatus.Shot or JobStatus.Selected && c.PhotoCount > 0)
                    .OrderByDescending(c => c.Id == C.S.ActiveClientId).ThenByDescending(c => c.Date).ToList();
        if (_cands.Count == 0)
        {
            var p = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
            p.Children.Add(Ui.Txt(Loc.T("ፎቶ የደረሰው ደንበኛ የለም። መጀመሪያ \"ከካሜራ መቀበል\" ላይ ፎቶ ይቀበሉ።", "No client with photos yet. Receive photos in Camera Intake first."), 15, Ui.Mut));
            p.Children.Add(new Border { Height = 12 });
            p.Children.Add(Ui.Btn("📷 " + Loc.T("ወደ ካሜራ መቀበል", "Go to Camera Intake"), () => C.Nav("camera")));
            Set(p);
            return;
        }
        _client = _cands.FirstOrDefault(c => c.Id == _client?.Id) ?? _cands[0];
        await LoadClient();
    }

    async Task LoadClient()
    {
        _photos = await St.GetPhotosAsync(_client.Id);
        _cache.Clear(); _cells.Clear();
        ApplyFilter(0);
        BuildUi();
        await Show();
    }

    void ApplyFilter(int keepIdx)
    {
        _view = _onlyHearts ? _photos.Where(p => p.Selected).ToList() : _photos.ToList();
        _idx = Math.Max(0, Math.Min(keepIdx, _view.Count - 1));
    }

    void BuildUi()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 12), LastChildFill = true };
        var rightBar = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(rightBar, Dock.Right);
        rightBar.Children.Add(Ui.Btn(_onlyHearts ? "☰ " + Loc.T("ሁሉም", "All Photos") : "❤ " + Loc.T("የተመረጡት ብቻ", "Selected Only"), () => { _onlyHearts = !_onlyHearts; ApplyFilter(0); BuildUi(); Safe(Show); }, 1));
        rightBar.Children.Add(Ui.Btn(_clientMode ? "⬅ " + Loc.T("ከደንበኛ ሁነታ ውጣ", "Exit Fullscreen") : "🖥 " + Loc.T("የደንበኛ ሁነታ", "Client Mode"), () => SetClientMode(!_clientMode), 1));
        rightBar.Children.Add(Ui.Btn("✔ " + Loc.T("ምርጫ አረጋግጥ", "Confirm Selection"), Confirm));
        top.Children.Add(rightBar);
        if (_clientMode) top.Children.Add(Ui.Txt(_client.Name, 20, Ui.Fg, true));
        else
        {
            var combo = Ui.Combo(_cands.Select(c => $"{c.Name} · {EthCal.Date(c.Date)}"), _cands.FindIndex(c => c.Id == _client.Id), 320);
            combo.Margin = new Thickness(0);
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedIndex < 0 || _cands[combo.SelectedIndex].Id == _client.Id) return;
                _client = _cands[combo.SelectedIndex]; C.Ingest.ActiveClientId = _client.Id;
                Safe(LoadClient);
            };
            top.Children.Add(combo);
        }
        root.Children.Add(top);

        _big = new Image { Stretch = Stretch.Uniform };
        var viewer = new Grid { Background = Ui.Black };
        viewer.Children.Add(_big);
        var left = RoundBtn("‹", HorizontalAlignment.Left, () => Go(_idx - 1));
        var right = RoundBtn("›", HorizontalAlignment.Right, () => Go(_idx + 1));
        viewer.Children.Add(left); viewer.Children.Add(right);
        _heart = new Button
        {
            Width = 76, Height = 76, FontSize = 36, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 18, 18), Cursor = Cursors.Hand, Foreground = Brushes.White, BorderThickness = new Thickness(0),
            Template = HeartTemplate()
        };
        _heart.Click += (_, _) => ToggleHeart();
        viewer.Children.Add(_heart);
        _counter = new TextBlock { Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(16, 0, 0, 16), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Background = new SolidColorBrush(Color.FromArgb(180, 15, 16, 24)), Padding = new Thickness(14, 6, 14, 6) };
        viewer.Children.Add(_counter);
        _name = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)), FontSize = 12, Margin = new Thickness(14, 10, 0, 0), VerticalAlignment = VerticalAlignment.Top, Visibility = _clientMode ? Visibility.Collapsed : Visibility.Visible };
        viewer.Children.Add(_name);
        var vb = new Border { CornerRadius = new CornerRadius(16), BorderBrush = Ui.Line, BorderThickness = new Thickness(1), ClipToBounds = true, Child = viewer };
        Grid.SetRow(vb, 1);
        root.Children.Add(vb);

        _strip = new StackPanel { Orientation = Orientation.Horizontal };
        _stripScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _strip, Margin = new Thickness(0, 12, 0, 0), Height = 112 };
        Grid.SetRow(_stripScroll, 2);
        root.Children.Add(_stripScroll);
        for (int i = 0; i < _view.Count; i++) AddCell(i);

        Set(root, false);
    }

    static ControlTemplate HeartTemplate() => (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
        "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>" +
        "<Border CornerRadius='38' Background='{TemplateBinding Background}' BorderBrush='#99FFFFFF' BorderThickness='2'>" +
        "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");

    Button RoundBtn(string t, HorizontalAlignment h, Action a)
    {
        var b = new Button
        {
            Content = t, FontSize = 30, Width = 56, Height = 56, HorizontalAlignment = h, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 14, 0), Foreground = Brushes.White, Cursor = Cursors.Hand, BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
            Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>" +
                "<Border CornerRadius='28' Background='{TemplateBinding Background}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' Margin='0,-4,0,0'/></Border></ControlTemplate>")
        };
        b.Click += (_, _) => a();
        return b;
    }

    void AddCell(int i)
    {
        var p = _view[i];
        var img = new Image { Width = 130, Height = 87, Stretch = Stretch.UniformToFill };
        var heart = new TextBlock { Text = "❤", Foreground = Ui.Bad, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 5, 0), Visibility = p.Selected ? Visibility.Visible : Visibility.Collapsed, Tag = "h" };
        var g = new Grid { Children = { img, heart } };
        var cell = new Border { Child = g, BorderThickness = new Thickness(3), BorderBrush = Brushes.Transparent, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand, Background = Ui.Black };
        int k = i;
        cell.MouseLeftButtonDown += (_, _) => Go(k);
        _strip.Children.Add(cell);
        _cells[p.Id] = cell;
        _ = LoadThumb(img, p.Id);
    }

    static readonly SemaphoreSlim Gate = new(3);
    async Task LoadThumb(Image img, int id)
    {
        await Gate.WaitAsync();
        try { var b = await St.GetImageAsync(id, true); if (b != null) img.Source = Ui.ToImage(b); }
        catch { }
        finally { Gate.Release(); }
    }

    void RefreshStrip()
    {
        for (int i = 0; i < _view.Count; i++)
        {
            if (!_cells.TryGetValue(_view[i].Id, out var cell)) continue;
            cell.BorderBrush = i == _idx ? Ui.Gold : Brushes.Transparent;
            var h = ((Grid)cell.Child).Children.OfType<TextBlock>().First();
            h.Visibility = _view[i].Selected ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_idx >= 0 && _idx < _view.Count && _cells.TryGetValue(_view[_idx].Id, out var cur))
            cur.BringIntoView();
    }

    void Go(int i)
    {
        if (_view.Count == 0) return;
        _idx = Math.Max(0, Math.Min(i, _view.Count - 1));
        Safe(Show);
    }

    async Task Show()
    {
        if (_view.Count == 0)
        {
            _big.Source = null; _counter.Text = Loc.T("ምንም የለም", "Nothing here"); _name.Text = "";
            UpdateHeart(); return;
        }
        int my = _idx;
        var p = _view[my];
        UpdateHeart(); RefreshStrip();
        _counter.Text = $"{my + 1} / {_view.Count}   ❤ {_photos.Count(x => x.Selected)}";
        _name.Text = p.DisplayName;
        var img = await GetFull(p.Id);
        if (my == _idx && img != null) _big.Source = img;
        _ = GetFull(_view[Math.Min(my + 1, _view.Count - 1)].Id);
        if (my > 0) _ = GetFull(_view[my - 1].Id);
    }

    async Task<BitmapImage> GetFull(int id)
    {
        if (_cache.TryGetValue(id, out var c)) return c;
        try
        {
            var b = await St.GetImageAsync(id, false);
            if (b == null) return null;
            var bi = Ui.ToImage(b);
            if (_cache.Count > 8) _cache.Remove(_cache.Keys.First());
            _cache[id] = bi;
            return bi;
        }
        catch { return null; }
    }

    void UpdateHeart()
    {
        bool on = _view.Count > 0 && _view[_idx].Selected;
        _heart.Content = on ? "❤" : "♡";
        _heart.Background = on ? Ui.Bad : new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
    }

    void ToggleHeart()
    {
        if (_view.Count == 0) return;
        var p = _view[_idx];
        p.Selected = !p.Selected;
        UpdateHeart(); RefreshStrip();
        _counter.Text = $"{_idx + 1} / {_view.Count}   ❤ {_photos.Count(x => x.Selected)}";
        bool v = p.Selected; int id = p.Id;
        Safe(() => St.SetSelectedAsync(id, v));
    }

    void SetClientMode(bool on)
    {
        _clientMode = on;
        C.ClientMode(on);
        BuildUi();
        Safe(Show);
    }

    void Confirm()
    {
        int n = _photos.Count(p => p.Selected);
        if (n == 0) { MessageBox.Show(Loc.T("ቢያንስ አንድ ፎቶ ይምረጡ", "Select at least one photo")); return; }
        Safe(async () =>
        {
            string folder = await St.ConfirmSelectionAsync(_client.Id);
            var w = new Window
            {
                Title = "Yam PICTURES", Width = 520, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow, Background = Ui.Panel, ResizeMode = ResizeMode.NoResize, FontFamily = Ui.Font
            };
            var sp = new StackPanel { Margin = new Thickness(24) };
            sp.Children.Add(Ui.Txt("✔ " + Loc.T("ምርጫ ተረጋግጧል", "Selection Confirmed"), 20, Ui.Ok, true));
            sp.Children.Add(Ui.Txt($"{_client.Name} · {_client.Phone}", 14, Ui.Mut));
            sp.Children.Add(new Border
            {
                Background = Ui.Bg, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0, 14, 0, 14),
                Child = new TextBlock { FontFamily = new FontFamily("Consolas"), Foreground = Ui.Fg, FontSize = 13, LineHeight = 22, Text = $"📁 {folder}\n ├─ Originals ({_photos.Count})\n ├─ Selected ({n})\n ├─ Edited\n └─ Final" }
            });
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(Ui.Btn("📁 " + Loc.T("folder ክፈት", "Open Folder"), () => OpenFolder(St.ClientFolderPath(new Client { Folder = folder })), 1));
            row.Children.Add(Ui.Btn(Loc.T("ዝጋ", "Done"), () => w.Close()));
            sp.Children.Add(row);
            w.Content = sp;
            w.ShowDialog();
            if (_clientMode) SetClientMode(false);
            C.Ingest.ActiveClientId = 0;
            C.Nav("jobs");
        });
    }
}
