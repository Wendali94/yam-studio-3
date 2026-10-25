using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using YamStudio.Core;

namespace YamStudio.Views;

public class SettingsPage : PageBase
{
    public override Task Reload()
    {
        var s = C.S;
        var root = new StackPanel();

        // language: one switch for the whole app
        var lang = new StackPanel { Orientation = Orientation.Horizontal };
        lang.Children.Add(Ui.Btn("አማርኛ", () => SetLang("am"), Loc.Am ? 0 : 1));
        lang.Children.Add(Ui.Btn("English", () => SetLang("en"), Loc.Am ? 1 : 0));
        var lc = new StackPanel();
        lc.Children.Add(Ui.Txt(Loc.T("ቋንቋ", "Language"), 13, Ui.Mut, true));
        lc.Children.Add(new Border { Height = 8 });
        lc.Children.Add(lang);
        lc.Children.Add(Ui.Txt(Loc.T("ሲቀየር ሁሉም ስክሪን፣ ደረሰኝ እና ሪፖርት በአንድ ላይ ይቀየራል። ቀን እና ሰዓት ሁልጊዜ የኢትዮጵያ ነው።",
            "Changes every screen, receipt and report together. Dates and clock are always Ethiopian."), 12, Ui.Mut));
        root.Children.Add(Ui.Card(lc));

        if (C.Host)
        {
            var pc = new StackPanel();
            pc.Children.Add(Ui.Txt(Loc.T("PC2 ን ለማገናኘት (ይህ PC1 ነው)", "Connecting PC2 (this is PC1)"), 13, Ui.Mut, true));
            pc.Children.Add(new Border { Height = 8 });
            pc.Children.Add(Ui.Txt($"IP: {string.Join(" / ", Ips())}\nPort: {s.ApiPort}\nPIN: {s.Pin}", 18, Ui.Gold, true));
            pc.Children.Add(Ui.Txt(Loc.T("ይህንን በ PC2 ላይ በመጀመሪያ ማዋቀሪያ ያስገቡ። ሁለቱም ኮምፒውተሮች በአንድ router/ገመድ ላይ መሆን አለባቸው።",
                "Enter these on PC2 during first setup. Both computers must be on the same router/cable."), 12, Ui.Mut));
            root.Children.Add(Ui.Card(pc));

            var data = new StackPanel();
            data.Children.Add(Ui.Txt(Loc.T("ዳታ እና ካሜራ", "Data & camera"), 13, Ui.Mut, true));
            data.Children.Add(new Border { Height = 8 });
            data.Children.Add(Ui.Txt($"{Loc.T("ዳታ የሚቀመጥበት", "Data folder")}: {s.RootDir}", 13));
            var ftpPort = Ui.Input(s.FtpPort.ToString(), 100);
            var ftpUser = Ui.Input(s.FtpUser, 160);
            var ftpPass = Ui.Input(s.FtpPass, 160);
            var watch = Ui.Input(s.WatchDir, 420);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            row.Children.Add(Ui.Field("FTP Port", ftpPort));
            row.Children.Add(Ui.Field(Loc.T("FTP ስም", "FTP user"), ftpUser));
            row.Children.Add(Ui.Field(Loc.T("FTP የይለፍ ቃል", "FTP password"), ftpPass));
            data.Children.Add(row);
            data.Children.Add(Ui.Field(Loc.T("ተጨማሪ የሚከታተል folder (ከተፈለገ፣ ለምሳሌ Imaging Edge)", "Extra watched folder (optional, e.g. Imaging Edge output)"), watch));

            var bk = Ui.Input(s.BackupDir, 420);
            var bt = Ui.Input(s.BackupTime, 80);
            var onExit = new CheckBox { Content = Loc.T("ሲዘጋ backup አድርግ", "Back up when closing"), IsChecked = s.BackupOnExit, Foreground = Ui.Fg, Margin = new Thickness(0, 0, 0, 10) };
            data.Children.Add(Ui.Field(Loc.T("Backup ቦታ (ሁለተኛ ዲስክ ወይም PC2 የተጋራ folder)", "Backup location (second disk or shared folder on PC2)"), bk));
            data.Children.Add(Ui.Field(Loc.T("የ backup ሰዓት (24 ሰዓት፣ ለምሳሌ 22:00)", "Backup time (24h, e.g. 22:00)"), bt));
            data.Children.Add(onExit);
            data.Children.Add(Ui.Txt($"{Loc.T("የመጨረሻ backup", "Last backup")}: {(string.IsNullOrEmpty(s.LastBackup) ? "—" : s.LastBackup)}", 12, Ui.Mut));
            var br = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            br.Children.Add(Ui.Btn("💾 " + Loc.T("አስቀምጥ", "Save"), () =>
            {
                if (int.TryParse(ftpPort.Text, out int fp) && fp > 0) s.FtpPort = fp;
                s.FtpUser = ftpUser.Text.Trim(); s.FtpPass = ftpPass.Text.Trim(); s.WatchDir = watch.Text.Trim();
                s.BackupDir = bk.Text.Trim(); s.BackupTime = bt.Text.Trim(); s.BackupOnExit = onExit.IsChecked == true;
                SettingsStore.Save();
                MessageBox.Show(Loc.T("ተቀምጧል። የ FTP ለውጥ ፕሮግራሙ እንደገና ሲከፈት ይሰራል።", "Saved. FTP changes apply after restarting the program."));
            }));
            br.Children.Add(Ui.Btn("🗂 " + Loc.T("ቦታ ምረጥ", "Browse"), () =>
            {
                var d = new Microsoft.Win32.OpenFolderDialog();
                if (d.ShowDialog() == true) bk.Text = d.FolderName;
            }, 1));
            br.Children.Add(Ui.Btn("▶ " + Loc.T("አሁን backup አድርግ", "Back up now"), () =>
            {
                s.BackupDir = bk.Text.Trim(); SettingsStore.Save();
                Safe(async () => { string r = await Task.Run(() => C.Backup.Run()); MessageBox.Show(r); await Reload(); });
            }, 1));
            data.Children.Add(br);
            root.Children.Add(Ui.Card(data));
        }
        else
        {
            var pc2 = new StackPanel();
            pc2.Children.Add(Ui.Txt(Loc.T("PC1 ግንኙነት (ይህ PC2 ነው)", "Connection to PC1 (this is PC2)"), 13, Ui.Mut, true));
            pc2.Children.Add(new Border { Height = 8 });
            var host = Ui.Input(s.HostAddress, 220);
            var pin = Ui.Input(s.Pin, 140);
            var work = Ui.Input(s.WorkDir, 360);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(Ui.Field(Loc.T("የ PC1 IP", "PC1 IP"), host));
            row.Children.Add(Ui.Field("PIN", pin));
            pc2.Children.Add(row);
            pc2.Children.Add(Ui.Field(Loc.T("የስራ folder (ፎቶ የሚወርድበት)", "Work folder (downloads go here)"), work));
            var br = new StackPanel { Orientation = Orientation.Horizontal };
            br.Children.Add(Ui.Btn("🔌 " + Loc.T("ሞክር እና አስቀምጥ", "Test & save"), () => Safe(async () =>
            {
                string r = await RemoteStudio.TestAsync(host.Text.Trim(), s.ApiPort, pin.Text.Trim());
                if (r == "OK")
                {
                    s.HostAddress = host.Text.Trim(); s.Pin = pin.Text.Trim(); s.WorkDir = work.Text.Trim();
                    SettingsStore.Save();
                    MessageBox.Show("✔ " + Loc.T("ተገናኝቷል። ፕሮግራሙን እንደገና ክፈት።", "Connected. Please restart the program."));
                }
                else MessageBox.Show(r);
            })));
            pc2.Children.Add(br);
            root.Children.Add(Ui.Card(pc2));
        }

        var about = Ui.Txt("YAM PICTURE · Studio Manager 1.0", 12, Ui.Mut);
        root.Children.Add(about);
        Set(root);
        return Task.CompletedTask;
    }

    void SetLang(string l)
    {
        C.S.Lang = l; Loc.Lang = l; SettingsStore.Save();
        C.Rebuild();
    }

    static IEnumerable<string> Ips()
    {
        try
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)).Select(a => a.ToString()).ToList();
        }
        catch { return new[] { "?" }; }
    }
}
