using System.Windows;
using System.Windows.Controls;
using YamStudio.Core;

namespace YamStudio.Views;

/// <summary>First run: tell the program whether this computer is PC1 (main) or PC2 (editing).</summary>
public class SetupWindow : Window
{
    public SetupWindow()
    {
        Title = "YAM Studio — Setup";
        Width = 560; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ui.Bg; Foreground = Ui.Fg; FontFamily = Ui.Font; ResizeMode = ResizeMode.NoResize;
        Build();
    }

    void Build()
    {
        var s = SettingsStore.Current;
        var sp = new StackPanel { Margin = new Thickness(26) };
        sp.Children.Add(Ui.Txt("YAM PICTURE", 26, Ui.Gold, true));
        sp.Children.Add(Ui.Txt("Studio Manager", 14, Ui.Mut));
        sp.Children.Add(new Border { Height = 14 });

        var lang = new StackPanel { Orientation = Orientation.Horizontal };
        lang.Children.Add(Ui.Btn("አማርኛ", () => { s.Lang = "am"; Loc.Lang = "am"; Build(); }, Loc.Am ? 0 : 1));
        lang.Children.Add(Ui.Btn("English", () => { s.Lang = "en"; Loc.Lang = "en"; Build(); }, Loc.Am ? 1 : 0));
        sp.Children.Add(lang);
        sp.Children.Add(new Border { Height = 14 });
        sp.Children.Add(Ui.Txt(Loc.T("ይህ ኮምፒውተር የትኛው ነው?", "Which computer is this?"), 16, Ui.Fg, true));
        sp.Children.Add(new Border { Height = 8 });

        var mode = new ComboBox { FontSize = 15, Padding = new Thickness(8, 7, 8, 7), Margin = new Thickness(0, 0, 0, 12) };
        mode.Items.Add(Loc.T("PC1 — ዋና (ቀጠሮ፣ ካሜራ፣ ምርጫ፣ ክፍያ፣ ዳታ)", "PC1 — Main (appointments, camera, selection, payments, data)"));
        mode.Items.Add(Loc.T("PC2 — ኤዲቲንግ (ከ PC1 ጋር ይገናኛል)", "PC2 — Editing (connects to PC1)"));
        mode.SelectedIndex = s.Mode == "client" ? 1 : 0;
        sp.Children.Add(mode);

        var root = Ui.Input(s.RootDir);
        var host = Ui.Input(s.HostAddress);
        var pin = Ui.Input("");
        var work = Ui.Input(s.WorkDir);
        var p1 = new StackPanel();
        p1.Children.Add(Ui.Field(Loc.T("ዳታ እና ፎቶ የሚቀመጥበት folder (ትልቅ ዲስክ ላይ)", "Folder for data & photos (on a large disk)"), root));
        var p2 = new StackPanel();
        p2.Children.Add(Ui.Field(Loc.T("የ PC1 IP አድራሻ (በ PC1 ቅንብር ውስጥ ይታያል)", "PC1 IP address (shown in PC1 Settings)"), host));
        p2.Children.Add(Ui.Field("PIN", pin));
        p2.Children.Add(Ui.Field(Loc.T("የስራ folder (ፎቶ የሚወርድበት)", "Work folder (downloads go here)"), work));
        sp.Children.Add(p1); sp.Children.Add(p2);
        void Sync() { p1.Visibility = mode.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed; p2.Visibility = mode.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed; }
        mode.SelectionChanged += (_, _) => Sync();
        Sync();

        sp.Children.Add(Ui.Btn(Loc.T("ጀምር", "Start"), async () =>
        {
            if (mode.SelectedIndex == 0)
            {
                if (string.IsNullOrWhiteSpace(root.Text)) return;
                try { Directory.CreateDirectory(root.Text.Trim()); }
                catch (Exception e) { MessageBox.Show(e.Message); return; }
                s.Mode = "host"; s.RootDir = root.Text.Trim();
            }
            else
            {
                string r = await RemoteStudio.TestAsync(host.Text.Trim(), s.ApiPort, pin.Text.Trim());
                if (r != "OK") { MessageBox.Show(r); return; }
                try { Directory.CreateDirectory(work.Text.Trim()); } catch { }
                s.Mode = "client"; s.HostAddress = host.Text.Trim(); s.Pin = pin.Text.Trim(); s.WorkDir = work.Text.Trim();
            }
            SettingsStore.Save();
            DialogResult = true;
        }));
        Content = sp;
    }
}
