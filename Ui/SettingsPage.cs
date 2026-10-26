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

        // Language Card
        var lang = new StackPanel { Orientation = Orientation.Horizontal };
        lang.Children.Add(Ui.Btn("አማርኛ", () => SetLang("am"), Loc.Am ? 0 : 1));
        lang.Children.Add(Ui.Btn("English", () => SetLang("en"), Loc.Am ? 1 : 0));
        var lc = new StackPanel();
        lc.Children.Add(Ui.Txt("🌐 " + Loc.T("ቋንቋ / Language", "Studio Language"), 14, Ui.Fg, true));
        lc.Children.Add(new Border { Height = 10 });
        lc.Children.Add(lang);
        lc.Children.Add(new Border { Height = 6 });
        lc.Children.Add(Ui.Txt(Loc.T("ሲቀየር ሁሉም ስክሪን፣ ደረሰኝ እና ሪፖርት በአንድ ላይ ይቀየራል። ቀን እና ሰዓት ሁልጊዜ የኢትዮጵያ ነው።",
            "Changes every screen, receipt and report together. Dates and clock always follow the Ethiopian calendar."), 12, Ui.Mut));
        root.Children.Add(Ui.Card(lc));

        // Storage & Camera Card
        var data = new StackPanel();
        data.Children.Add(Ui.Txt("💾 " + Loc.T("የዳታ ማስቀመጫ እና የካሜራ ቅንብር", "Storage & Camera FTP Configuration"), 14, Ui.Fg, true));
        data.Children.Add(new Border { Height = 12 });

        var rootDir = Ui.Input(s.RootDir, 420);
        var rootRow = new StackPanel { Orientation = Orientation.Horizontal };
        rootRow.Children.Add(Ui.Field(Loc.T("ዋና ዳታ እና ፎቶ የሚቀመጥበት Folder", "Primary Studio Storage Folder (Local or Shared Drive)"), rootDir));
        var browseRoot = Ui.Btn("🗂 " + Loc.T("ምረጥ", "Browse"), () =>
        {
            var d = new Microsoft.Win32.OpenFolderDialog();
            if (d.ShowDialog() == true) rootDir.Text = d.FolderName;
        }, 1);
        browseRoot.VerticalAlignment = VerticalAlignment.Bottom;
        browseRoot.Margin = new Thickness(0, 0, 0, 12);
        rootRow.Children.Add(browseRoot);
        data.Children.Add(rootRow);

        var ftpPort = Ui.Input(s.FtpPort.ToString(), 110);
        var ftpUser = Ui.Input(s.FtpUser, 170);
        var ftpPass = Ui.Input(s.FtpPass, 170);
        var watch = Ui.Input(s.WatchDir, 420);

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Ui.Field("FTP Port", ftpPort));
        row.Children.Add(Ui.Field(Loc.T("FTP ስም", "FTP User"), ftpUser));
        row.Children.Add(Ui.Field(Loc.T("FTP የይለፍ ቃል", "FTP Password"), ftpPass));
        data.Children.Add(row);
        data.Children.Add(Ui.Field(Loc.T("ተጨማሪ የሚከታተል Folder (ለምሳሌ የ Tether ሶፍትዌር output)", "Optional Watched Folder (e.g. Tethering output folder)"), watch));

        var bk = Ui.Input(s.BackupDir, 420);
        var bt = Ui.Input(s.BackupTime, 110);
        var onExit = new CheckBox
        {
            Content = Loc.T("ፕሮግራሙ ሲዘጋ በራሱ Backup ያድርግ", "Automatically run backup when closing application"),
            IsChecked = s.BackupOnExit,
            Foreground = Ui.Fg,
            Margin = new Thickness(0, 4, 0, 12)
        };

        var bkRow = new StackPanel { Orientation = Orientation.Horizontal };
        bkRow.Children.Add(Ui.Field(Loc.T("የ Backup ማስቀመጫ ቦታ (ሁለተኛ ዲስክ / External Drive)", "Backup Destination Folder"), bk));
        var browseBk = Ui.Btn("🗂 " + Loc.T("ምረጥ", "Browse"), () =>
        {
            var d = new Microsoft.Win32.OpenFolderDialog();
            if (d.ShowDialog() == true) bk.Text = d.FolderName;
        }, 1);
        browseBk.VerticalAlignment = VerticalAlignment.Bottom;
        browseBk.Margin = new Thickness(0, 0, 0, 12);
        bkRow.Children.Add(browseBk);
        data.Children.Add(bkRow);

        data.Children.Add(Ui.Field(Loc.T("የዕለታዊ Backup ሰዓት (24 ሰዓት፣ ለምሳሌ 22:00)", "Scheduled Backup Time (24h, e.g. 22:00)"), bt));
        data.Children.Add(onExit);
        data.Children.Add(Ui.Txt($"{Loc.T("የመጨረሻ Backup", "Last backup")}: {(string.IsNullOrEmpty(s.LastBackup) ? "—" : s.LastBackup)}", 12, Ui.Mut));

        var br = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        br.Children.Add(Ui.Btn("💾 " + Loc.T("ቅንብሮችን አስቀምጥ", "Save Settings"), () =>
        {
            if (!string.IsNullOrWhiteSpace(rootDir.Text))
            {
                try { Directory.CreateDirectory(rootDir.Text.Trim()); s.RootDir = rootDir.Text.Trim(); } catch { }
            }
            if (int.TryParse(ftpPort.Text, out int fp) && fp > 0) s.FtpPort = fp;
            s.FtpUser = ftpUser.Text.Trim();
            s.FtpPass = ftpPass.Text.Trim();
            s.WatchDir = watch.Text.Trim();
            s.BackupDir = bk.Text.Trim();
            s.BackupTime = bt.Text.Trim();
            s.BackupOnExit = onExit.IsChecked == true;
            SettingsStore.Save();
            C.Toast?.Invoke("✔ " + Loc.T("ቅንብሮች ተቀምጠዋል", "Settings saved"));
        }));
        br.Children.Add(Ui.Btn("▶ " + Loc.T("አሁን Backup አድርግ", "Run Backup Now"), () =>
        {
            s.BackupDir = bk.Text.Trim(); SettingsStore.Save();
            Safe(async () => { string r = await Task.Run(() => C.Backup.Run()); MessageBox.Show(r); await Reload(); });
        }, 1));

        data.Children.Add(br);
        root.Children.Add(Ui.Card(data));

        root.Children.Add(Ui.Txt("Yam PICTURES · Studio Management Suite v2.0", 12, Ui.Mut));
        Set(root);
        return Task.CompletedTask;
    }

    void SetLang(string l)
    {
        C.S.Lang = l; Loc.Lang = l; SettingsStore.Save();
        C.Rebuild();
    }
}
