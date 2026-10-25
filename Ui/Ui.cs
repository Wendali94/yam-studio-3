using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YamStudio.Core;

namespace YamStudio.Views;

/// <summary>Everything the pages share: services and callbacks.</summary>
public class Ctx
{
    public static Ctx I;
    public IStudio Studio;
    public LocalStudio Local;       // PC1 only
    public IngestService Ingest;    // PC1 only
    public BackupService Backup;    // PC1 only
    public AppSettings S => SettingsStore.Current;
    public Action<string> Nav;
    public Action Rebuild;
    public Action<bool> ClientMode;
    public Action<string> Toast;
    public bool Host => Studio.IsHost;
}

public static class Ui
{
    static SolidColorBrush B(string h) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(h); b.Freeze(); return b; }
    public static readonly Brush Bg = B("#0E1014"), Panel = B("#161A22"), Panel2 = B("#1D222C"), Line = B("#2A303C"),
        Fg = B("#EBE8E1"), Mut = B("#8F96A6"), Gold = B("#D6A84F"), Ok = B("#4CC38A"), Warn = B("#E5A23C"), Bad = B("#E5604D"),
        Blue = B("#5AA2FF"), Black = B("#000000");
    public static readonly FontFamily Font = new("Segoe UI, Nirmala UI, Ebrima");

    public static Brush StatusBrush(string s) => s switch
    {
        JobStatus.Booked => Blue, JobStatus.Shot => Warn, JobStatus.Selected => B("#C58BFF"),
        JobStatus.Editing => B("#FF8F5A"), JobStatus.Review => B("#5AD7E5"), JobStatus.Delivered => Ok, _ => Mut
    };

    static ControlTemplate _btnTpl;
    static ControlTemplate BtnTemplate => _btnTpl ??= (ControlTemplate)XamlReader.Parse(
        "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>" +
        "<Border x:Name='bd' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' " +
        "BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9' Padding='{TemplateBinding Padding}'>" +
        "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
        "<ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='Opacity' Value='0.85'/></Trigger>" +
        "<Trigger Property='IsEnabled' Value='False'><Setter TargetName='bd' Property='Opacity' Value='0.35'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");

    public static TextBlock Txt(string s, double size = 14, Brush fg = null, bool bold = false) => new()
    {
        Text = s, FontSize = size, Foreground = fg ?? Fg, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
    };

    /// <summary>kind: 0 = gold primary, 1 = secondary, 2 = danger</summary>
    public static Button Btn(string text, Action click, int kind = 0, double size = 14)
    {
        var b = new Button
        {
            Content = text, Template = BtnTemplate, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0),
            FontSize = size, Cursor = System.Windows.Input.Cursors.Hand, FontWeight = FontWeights.SemiBold,
            Background = kind == 0 ? Gold : kind == 1 ? Panel2 : Brushes.Transparent,
            Foreground = kind == 0 ? B("#1A1405") : kind == 1 ? Fg : Bad,
            BorderBrush = kind == 0 ? Gold : kind == 1 ? Line : Bad, BorderThickness = new Thickness(1)
        };
        b.Click += (_, _) => { try { click(); } catch (Exception e) { MessageBox.Show(e.Message); } };
        return b;
    }

    public static Border Card(UIElement child, double pad = 16) => new()
    {
        Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14),
        Padding = new Thickness(pad), Child = child, Margin = new Thickness(0, 0, 0, 14)
    };

    public static TextBox Input(string text = "", double width = double.NaN) => new()
    {
        Text = text, Padding = new Thickness(8, 7, 8, 7), FontSize = 14, Width = width, Background = Brushes.White,
        Foreground = Brushes.Black, BorderBrush = Line, Margin = new Thickness(0, 0, 0, 10)
    };

    public static ComboBox Combo(IEnumerable<string> items, int sel = 0, double width = double.NaN)
    {
        var c = new ComboBox { FontSize = 14, Padding = new Thickness(8, 6, 8, 6), Width = width, Margin = new Thickness(0, 0, 0, 10) };
        foreach (var i in items) c.Items.Add(i);
        c.SelectedIndex = sel;
        return c;
    }

    public static StackPanel Field(string label, UIElement input)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        sp.Children.Add(Txt(label, 12, Mut));
        sp.Children.Add(input);
        return sp;
    }

    public static Border Chip(string status) => new()
    {
        BorderBrush = StatusBrush(status), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(99),
        Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
        Child = Txt(JobStatus.Label(status), 12, StatusBrush(status))
    };

    public static Border Stat(string label, string value) => new()
    {
        Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14),
        Padding = new Thickness(16), Margin = new Thickness(0, 0, 12, 12), MinWidth = 170,
        Child = new StackPanel { Children = { Txt(label, 13, Mut), Txt(value, 28, Gold, true) } }
    };

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
            Title = title, Width = 380, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow, Background = Panel, ResizeMode = ResizeMode.NoResize, FontFamily = Font
        };
        var tb = Input(def);
        string result = null;
        var sp = new StackPanel { Margin = new Thickness(18) };
        sp.Children.Add(Txt(label, 14));
        sp.Children.Add(new Border { Height = 8 });
        sp.Children.Add(tb);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(Btn(Loc.T("ሰርዝ", "Cancel"), () => w.Close(), 1));
        row.Children.Add(Btn("OK", () => { result = tb.Text; w.Close(); }));
        sp.Children.Add(row);
        w.Content = sp;
        w.Loaded += (_, _) => { tb.Focus(); tb.SelectAll(); };
        w.ShowDialog();
        return result;
    }

    public static Grid Header(string left, params UIElement[] right)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(Txt(left, 18, Fg, true));
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var r in right) sp.Children.Add(r);
        Grid.SetColumn(sp, 1);
        g.Children.Add(sp);
        return g;
    }
}

/// <summary>Ethiopian date picker: day / month name / year. Value is always an ISO Gregorian date.</summary>
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
        foreach (var c in new[] { _d, _m, _y }) { c.FontSize = 14; c.Padding = new Thickness(8, 6, 8, 6); c.Margin = new Thickness(0, 0, 6, 10); Children.Add(c); }
        _d.Width = 60; _m.Width = 120; _y.Width = 80;
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

/// <summary>Time picker showing the Ethiopian clock (ጠዋት 3:00 ...). Value is HH:mm 24h.</summary>
public class EthTimePicker : ComboBox
{
    readonly List<string> _vals = new();

    public EthTimePicker(string hm = "09:00")
    {
        FontSize = 14; Padding = new Thickness(8, 6, 8, 6); Margin = new Thickness(0, 0, 0, 10); Width = 150;
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
