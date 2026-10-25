using System.Windows;
using System.Windows.Controls;
using YamStudio.Core;

namespace YamStudio.Views;

/// <summary>Editing queue. PC2 downloads the selected photos, edits, uploads finals; PC1 approves and delivers.</summary>
public class JobsPage : PageBase
{
    public override async Task Reload()
    {
        var all = await St.GetClientsAsync();
        var active = all.Where(c => c.Status is JobStatus.Selected or JobStatus.Editing or JobStatus.Review).OrderBy(c => c.Date).ToList();
        var done = all.Where(c => c.Status == JobStatus.Delivered).OrderByDescending(c => c.Date).Take(15).ToList();

        var root = new StackPanel();
        var steps = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        foreach (var s in new[] { JobStatus.Selected, JobStatus.Editing, JobStatus.Review, JobStatus.Delivered })
            steps.Children.Add(Ui.Chip(s));
        root.Children.Add(steps);

        var box = new StackPanel();
        box.Children.Add(Ui.Txt(Loc.T("የሚሰሩ ስራዎች", "Active jobs"), 13, Ui.Mut, true));
        if (active.Count == 0) box.Children.Add(Ui.Txt(Loc.T("ምንም ስራ የለም", "No jobs right now"), 14, Ui.Mut));
        foreach (var c in active) box.Children.Add(Row(c));
        root.Children.Add(Ui.Card(box));

        if (done.Count > 0)
        {
            var d = new StackPanel();
            d.Children.Add(Ui.Txt(Loc.T("የተረከቡ (የቅርብ ጊዜ)", "Delivered (recent)"), 13, Ui.Mut, true));
            foreach (var c in done) d.Children.Add(Row(c));
            root.Children.Add(Ui.Card(d));
        }
        root.Children.Add(Ui.Txt(Loc.T(
            "PC2 'ፎቶ አውርድ' ሲል የተመረጡት ፎቶዎች ከ PC1 ወደ PC2 የስራ folder ይገለበጣሉ። ሲጨርስ 'የተጠናቀቁ ላክ' ብሎ ወደ PC1 `Final` ይልካል።",
            "PC2 'Download photos' copies the selected photos from PC1 into the PC2 work folder. When finished, 'Upload finals' sends them into PC1's `Final` folder."), 12, Ui.Mut));
        Set(root);
    }

    UIElement Row(Client c)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var who = new StackPanel();
        who.Children.Add(Ui.Txt(c.Name, 16, Ui.Fg, true));
        who.Children.Add(Ui.Txt($"📁 {(string.IsNullOrEmpty(c.Folder) ? "—" : c.Folder)}  ·  ❤ {c.SelectedCount}/{c.PhotoCount}", 12, Ui.Mut));
        top.Children.Add(who);
        var chip = Ui.Chip(c.Status); Grid.SetColumn(chip, 1);
        top.Children.Add(chip);
        sp.Children.Add(top);

        var btns = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var b in JobButtons(c, Reload)) btns.Children.Add(b);

        if (!C.Host && c.Status is JobStatus.Selected or JobStatus.Editing)
        {
            btns.Children.Add(Ui.Btn("⬇ " + Loc.T("ፎቶ አውርድ", "Download photos"), () => Safe(async () =>
            {
                string dir = await St.FetchSelectedAsync(c.Id, C.S.WorkDir);
                C.Toast?.Invoke("✔ " + Loc.T("ፎቶዎች ወርደዋል", "Photos downloaded"));
                OpenFolder(dir);
            }), 1, 13));
        }
        if (!C.Host && c.Status is JobStatus.Editing or JobStatus.Review)
        {
            btns.Children.Add(Ui.Btn("⬆ " + Loc.T("የተጠናቀቁ ላክ", "Upload finals"), () => Safe(async () =>
            {
                var d = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Filter = "Images|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.psd|All|*.*" };
                if (d.ShowDialog() != true) return;
                await St.UploadFinalAsync(c.Id, d.FileNames);
                C.Toast?.Invoke($"✔ {d.FileNames.Length} " + Loc.T("ፋይል ተልኳል", "files uploaded"));
            }), 1, 13));
        }
        if (C.Host && !string.IsNullOrEmpty(c.Folder))
        {
            btns.Children.Add(Ui.Btn("📁 " + Loc.T("folder ክፈት", "Open folder"), () => OpenFolder(C.Studio.ClientFolderPath(c)), 1, 13));
            if (c.Status is JobStatus.Review or JobStatus.Delivered)
                btns.Children.Add(Ui.Btn("💾 " + Loc.T("ወደ USB/HDD ቅዳ", "Copy to USB/HDD"), () => Safe(() => Deliver(c)), 1, 13));
        }
        sp.Children.Add(btns);
        return new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = sp };
    }

    async Task Deliver(Client c)
    {
        string fin = Path.Combine(C.Studio.ClientFolderPath(c), "Final");
        if (!Directory.Exists(fin) || Directory.GetFiles(fin).Length == 0)
        { MessageBox.Show(Loc.T("በ Final folder ውስጥ ፋይል የለም", "The Final folder is empty")); return; }
        var d = new Microsoft.Win32.OpenFolderDialog();
        if (d.ShowDialog() != true) return;
        string dest = Path.Combine(d.FolderName, c.Folder);
        int n = await Task.Run(() =>
        {
            Directory.CreateDirectory(dest);
            int k = 0;
            foreach (var f in Directory.GetFiles(fin)) { File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true); k++; }
            return k;
        });
        C.Toast?.Invoke($"✔ {n} " + Loc.T("ፋይል ተቀድቷል", "files copied"));
    }
}
