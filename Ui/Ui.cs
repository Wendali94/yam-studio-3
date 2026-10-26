using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using YamStudio.Core;

namespace YamStudio.Views;

public class Ctx
{
    public static Ctx I;
    public IStudio Studio;
    public LocalStudio Local;
    public IngestService Ingest;
    public BackupService Backup;
    public AppSettings S => SettingsStore.Current;
    public Action<string> Nav;
    public Action Rebuild;
    public Action<bool> ClientMode;
    public Action<string> Toast;
}

public static class Ui
{
    static SolidColorBrush B(string h) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(h); b.Freeze(); return b; }

    // Ultra-Modern Dark SaaS Palette (inspired by your reference screenshots)
    public static readonly Brush Bg = B("#09090E"),
        Panel = B("#12131C"),
        Panel2 = B("#191B28"),
        InputBg = B("#141622"),
        Line = B("#23263A"),
        LineGlow = B("#3B3F5C"),
        Fg = B("#F4F5F8"),
        Mut = B("#8A90A6"),
        Gold = B("#A855F7"),      // Electric Violet Accent
        Accent2 = B("#6366F1"),   // Indigo Accent
        Ok = B("#10B981"),        // Neon Emerald
        Warn = B("#F59E0B"),      // Amber Glow
        Bad = B("#F43F5E"),       // Rose Red
        Blue = B("#38BDF8"),      // Sky Cyan
        Black = B("#050508");

    public static readonly LinearGradientBrush AccentGrad = new(
        (Color)ColorConverter.ConvertFromString("#8B5CF6"),
        (Color)ColorConverter.ConvertFromString("#6366F1"),
        new Point(0, 0), new Point(1, 1));

    public static readonly LinearGradientBrush CardGrad = new(
        (Color)ColorConverter.ConvertFromString("#151722"),
        (Color)ColorConverter.ConvertFromString("#101119"),
        new Point(0, 0), new Point(0, 1));

    public static readonly FontFamily Font = new("Segoe UI Variable Display, Segoe UI, Nyala, Ebrima");

    public static Brush StatusBrush(string s) => s switch
    {
        JobStatus.Booked => Blue,
        JobStatus.Shot => Warn,
        JobStatus.Selected => B("#C084FC"),
        JobStatus.Editing => B("#FB923C"),
        JobStatus.Review => B("#2DD4BF"),
        JobStatus.Delivered => Ok,
        _ => Mut
    };

    /// <summary>Renders the Yam PICTURES logo (loads logo.png if present, or draws the custom script + filmstrip vector logo).</summary>
    public static UIElement Logo(double scale = 1.0)
    {
        string customLogo = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
        if (File.Exists(customLogo))
        {
            try
            {
                var bi = new BitmapImage(new Uri(customLogo));
                return new Image { Source = bi, Height = 58 * scale, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
            }
            catch { }
        }

        var g = new Grid { LayoutTransform = new ScaleTransform(scale, scale), HorizontalAlignment = HorizontalAlignment.Left };
        var c = new Canvas { Width = 195, Height = 64 };

        // Top Film-Strip Wave (Matches your Yam Pictures logo!)
        var waveTop = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 68,16 C 85,23 108,5 128,15 C 138,20 144,27 148,33"),
            Stroke = Brushes.White, StrokeThickness = 1.6
        };
        var waveBot = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 64,23 C 83,30 106,12 126,21 C 136,26 142,31 146,35"),
            Stroke = Brushes.White, StrokeThickness = 1.6
        };
        var rungs = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 74,18 L 72,24 M 84,18 L 82,24 M 94,16 L 92,22 M 104,13 L 103,19 M 114,12 L 113,18 M 124,14 L 123,20 M 134,18 L 132,24"),
            Stroke = Brushes.White, StrokeThickness = 2.2
        };
        c.Children.Add(waveTop);
        c.Children.Add(waveBot);
        c.Children.Add(rungs);

        // Brush-script "Yam"
        var yam = new TextBlock
        {
            Text = "Yam",
            FontFamily = new FontFamily("Segoe Script, Brush Script MT, Gabriola, Segoe UI"),
            FontSize = 38,
            FontWeight = FontWeights.Bold,
            FontStyle = FontStyles.Italic,
            Foreground = Brushes.White,
            Effect = new DropShadowEffect { Color = Color.FromRgb(168, 85, 247), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.45 }
        };
        Canvas.SetLeft(yam, 6);
        Canvas.SetTop(yam, 6);
        c.Children.Add(yam);

        // Spaced "P I C T U R E S"
        var pic = new TextBlock
        {
            Text = "P  I  C  T  U  R  E  S",
            FontFamily = new FontFamily("Segoe UI, Bahnschrift, Arial"),
            FontSize = 8.5,
            FontWeight = FontWeights.Bold,
            Foreground = B("#D1D5DB")
        };
        Canvas.SetLeft(pic, 66);
        Canvas.SetTop(pic, 47);
        c.Children.Add(pic);

        g.Children.Add(c);
        return g;
    }

    static ControlTemplate _btnTpl;
    static ControlTemplate BtnTemplate => _btnTpl ??= (ControlTemplate)XamlReader.Parse(
        "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>" +
        "<Border x:Name='bd' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' " +
        "BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='10' Padding='{TemplateBinding Padding}'>" +
        "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
        "<ControlTemplate.Triggers>" +
        "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='Opacity' Value='0.88'/><Setter TargetName='bd' Property='BorderBrush' Value='#A855F7'/></Trigger>" +
        "<Trigger Property='IsPressed' Value='True'><Setter TargetName='bd' Property='Opacity' Value='0.75'/></Trigger>" +
        "<Trigger Property='IsEnabled' Value='False'><Setter TargetName='bd' Property='Opacity' Value='0.35'/></Trigger>" +
        "</ControlTemplate.Triggers></ControlTemplate>");

    static ControlTemplate _inputTpl;
    static ControlTemplate InputTemplate => _inputTpl ??= (ControlTemplate)XamlReader.Parse(
        "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='TextBox'>" +
        "<Border x:Name='bd' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='10' Padding='{TemplateBinding Padding}'>" +
        "<ScrollViewer x:Name='PART_ContentHost' Focusable='false' HorizontalScrollBarVisibility='Hidden' VerticalScrollBarVisibility='Hidden'/>" +
        "</Border>" +
        "<ControlTemplate.Triggers>" +
        "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='#4C5175'/></Trigger>" +
        "<Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='#A855F7'/><Setter TargetName='bd' Property='Background' Value='#181B2A'/></Trigger>" +
        "</ControlTemplate.Triggers></ControlTemplate>");

    public static TextBlock Txt(string s, double size = 14, Brush fg = null, bool bold = false) => new()
    {
        Text = s, FontSize = size, Foreground = fg ?? Fg, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
    };

    /// <summary>kind: 0 = Neon Violet Gradient, 1 = Dark Glass Secondary, 2 = Danger Rose</summary>
    public static Button Btn(string text, Action click, int kind = 0, double size = 13.5)
    {
        var b = new Button
        {
            Content = text, Template = BtnTemplate, Padding = new Thickness(16, 9, 16, 9), Margin = new Thickness(0, 0, 8, 0),
            FontSize = size, Cursor = System.Windows.Input.Cursors.Hand, FontWeight = FontWeights.SemiBold,
            Background = kind == 0 ? AccentGrad : kind == 1 ? Panel2 : B("#2A1219"),
            Foreground = kind == 0 ? Brushes.White : kind == 1 ? Fg : Bad,
            BorderBrush = kind == 0 ? B("#A855F7") : kind == 1 ? Line : Bad,
            BorderThickness = new Thickness(1)
        };
        if (kind == 0)
            b.Effect = new DropShadowEffect { Color = Color.FromRgb(124, 58, 237), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.35 };
        b.Click += (_, _) => { try { click(); } catch (Exception e) { MessageBox.Show(e.Message); } };
        return b;
    }

    public static Border Card(UIElement child, double pad = 18) => new()
    {
        Background = CardGrad,
        BorderBrush = Line,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(16),
        Padding = new Thickness(pad),
        Child = child,
        Margin = new Thickness(0, 0, 0, 14),
        Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 22, ShadowDepth = 4, Opacity = 0.45 }
    };

    public static TextBox Input(string text = "", double width = double.NaN) => new()
    {
        Text = text,
        Template = InputTemplate,
        Padding = new Thickness(12, 9, 12, 9),
        FontSize = 14,
        Width = width,
        Background = InputBg,
        Foreground = Fg,
        CaretBrush = Gold,
        BorderBrush = Line,
        Margin = new Thickness(0, 4, 0, 12)
    };

    public static ComboBox Combo(IEnumerable<string> items, int sel = 0, double width = double.NaN)
    {
        var c = new ComboBox { FontSize = 14, Width = width, Margin = new Thickness(0, 4, 0, 12) };
        foreach (var i in items) c.Items.Add(i);
        c.SelectedIndex = sel;
        return c;
    }

    public static StackPanel Field(string label, UIElement input)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        sp.Children.Add(Txt(label, 12, Mut, true));
        sp.Children.Add(input);
        return sp;
    }

    public static Border Chip(string status)
    {
        var br = (SolidColorBrush)StatusBrush(status);
        var c = br.Color;
        var bg = new SolidColorBrush(Color.FromArgb(32, c.R, c.G, c.B));
        return new Border
        {
            Background = bg,
            BorderBrush = new SolidColorBrush(Color.FromArgb(95, c.R, c.G, c.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(99),
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Txt("● " + JobStatus.Label(status), 11.5, br, true)
        };
    }

    public static Border Stat(string label, string value, string icon = "✦", Brush accent = null)
    {
        accent ??= Gold;
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var badge = new Border
        {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(10),
            Background = Panel2, BorderBrush = Line, BorderThickness = new Thickness(1),
            Child = new TextBlock { Text = icon, FontSize = 15, Foreground = accent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        DockPanel.SetDock(badge, Dock.Right);
        top.Children.Add(badge);
        top.Children.Add(Txt(label, 12.5, Mut, true));

        var bar = new Border
        {
            Height = 3, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 10, 0, 0),
            Background = new LinearGradientBrush(((SolidColorBrush)accent).Color, Colors.Transparent, new Point(0, 0), new Point(1, 0))
        };

        return new Border
        {
            Background = CardGrad,
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 0, 14, 14),
            MinWidth = 205,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 3, Opacity = 0.4 },
            Child = new StackPanel { Children = { top, Txt(value, 26, Fg, true), bar } }
        };
    }

    public static BitmapImage ToImage(byte[] bytes)
    {
        var bi = new BitmapImage();
        using var ms = new MemoryStream(bytes);
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.StreamSource = ms;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    public static string Prompt(string title, string label, string def = "")
    {
        var w = new Window
        {
            Title = title, Width = 400, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow, Background = Panel, ResizeMode = ResizeMode.NoResize, FontFamily = Font
        };
        var tb = Input(def);
        string result = null;
        var sp = new StackPanel { Margin = new Thickness(22) };
        sp.Children.Add(Txt(label, 14, Fg, true));
        sp.Children.Add(new Border { Height = 8 });
        sp.Children.Add(tb);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        row.Children.Add(Btn(Loc.T("ሰርዝ", "Cancel"), () => w.Close(), 1));
        row.Children.Add(Btn(Loc.T("አስቀምጥ", "Confirm"), () => { result = tb.Text; w.Close(); }));
        sp.Children.Add(row);
        w.Content = sp;
        w.Loaded += (_, _) => { tb.Focus(); tb.SelectAll(); };
        w.ShowDialog();
        return result;
    }
}

public class EthDatePicker : StackPanel
{
    readonly ComboBox _d = new(), _m = new(), _y = new();
    bool _busy;

    public EthDatePicker(string iso = null)
    {
        Orientation = Orientation.Horizontal;
        iso ??= EthCal.TodayIso();
        var e = EthCal.IsoToEth(iso);
        int ty = EthCal.IsoToEth(EthCal.TodayIso()).y;
        foreach (var c in new[] { _d, _m, _y }) { c.FontSize = 14; c.Margin = new Thickness(0, 4, 8, 12); Children.Add(c); }
        _d.Width = 72; _m.Width = 135; _y.Width = 95;
        for (int y = Math.Min(e.y, ty - 1); y <= Math.Max(e.y, ty + 3); y++) _y.Items.Add(y);
        var names = Loc.Am ? EthCal.MonthAm : EthCal.MonthEn;
        foreach (var n in names) _m.Items.Add(n);
        _y.SelectedItem = e.y; _m.SelectedIndex = e.m - 1;
        FillDays(e.d);
        _m.SelectionChanged += (_, _) => FillDays(_d.SelectedIndex + 1);
        _y.SelectionChanged += (_, _) => FillDays(_d.SelectedIndex + 1);
    }

    void FillDays(int keep)
    {
        if (_busy || _m.SelectedIndex < 0 || _y.SelectedItem == null) return;
        _busy = true;
        int n = EthCal.DaysInMonth((int)_y.SelectedItem, _m.SelectedIndex + 1);
        _d.Items.Clear();
        for (int i = 1; i <= n; i++) _d.Items.Add(i);
        _d.SelectedIndex = Math.Min(Math.Max(keep, 1), n) - 1;
        _busy = false;
    }

    public string Value => EthCal.EthToIso((int)_y.SelectedItem, _m.SelectedIndex + 1, _d.SelectedIndex + 1);
}

public class EthTimePicker : ComboBox
{
    readonly List<string> _vals = new();

    public EthTimePicker(string hm = "09:00")
    {
        FontSize = 14; Margin = new Thickness(0, 4, 0, 12); Width = 170;
        for (int h = 0; h < 24; h++)
            foreach (int m in new[] { 0, 15, 30, 45 })
            {
                string v = $"{h:00}:{m:00}";
                _vals.Add(v);
                Items.Add(EthCal.Time(v));
            }
        int i = _vals.IndexOf(hm);
        SelectedIndex = i >= 0 ? i : _vals.IndexOf("09:00");
    }

    public string Value => _vals[Math.Max(0, SelectedIndex)];
}
