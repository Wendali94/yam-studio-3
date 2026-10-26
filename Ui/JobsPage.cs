using System.Windows;
using System.Windows.Controls;
using YamStudio.Core;

namespace YamStudio.Views;

public class JobsPage : PageBase
{
    public override async Task Reload()
    {
        var all = await St.GetClientsAsync();
        var active = all.Where(c => c.Status is JobStatus.Selected or JobStatus.Editing or JobStatus.Review).OrderBy(c => c.Date).ToList();
        var done = all.Where(c => c.Status == JobStatus.Delivered).OrderByDescending(c => c.Date).Take(15).ToList();

        var root = new StackPanel();

        // Pipeline Summary Cards
        var pipe = new WrapPanel();
        foreach (var s in new[] { JobStatus.Selected, JobStatus.Editing, JobStatus.Review, JobStatus.Delivered })
        {
            int cnt = all.Count(c => c.Status == s);
            pipe.Children.Add(Ui.Stat(JobStatus.Label(s), cnt.ToString(), "🎬", Ui.StatusBrush(s)));
        }
        root.Children.Add(pipe);

        var box = new StackPanel();
        box.Children.Add(Ui.Txt(Loc.T("በኤዲቲንግ እና ርክክብ ላይ ያሉ ስራዎች", "Active Production Queue"), 14, Ui.Fg, true));
        box.Children.Add(new Border { Height = 10 });
        if (active.Count == 0) box.Children.Add(Ui.Txt(Loc.T("ምንም ስራ የለም", "No active jobs in production"), 14, Ui.Mut));
        foreach (var c in active) box.Children.Add(Row(c));
        root.Children.Add(Ui.Card(box));

        if (done.Count > 0)
        {
            var d = new StackPanel();
            d.Children.Add(Ui.Txt(Loc.T("የተረከቡ (የቅርብ ጊዜ)", "Recently Delivered"), 14, Ui.Mut, true));
            d.Children.Add(new Border { Height = 10 });
            foreach (var c in done) d.Children.Add(Row(c));
            root.Children.Add(Ui.Card(d));
        }

        Set(root);
    }

    UIElement Row(Client c)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var who = new StackPanel();
        who.Children.Add(Ui.Txt(c.Name, 16, Ui.Fg, true));
        who.Children.Add(Ui.Txt($"📁 {(string.IsNullOrEmpty(c.Folder) ? "—" : c.Folder)}   ·   ❤ {c.SelectedCount}/{c.PhotoCount}   ·   {EthCal.Date(c.Date)}", 12, Ui.Mut));
        top.Children.Add(who);

        var chip = Ui.Chip(c.Status);
        Grid.SetColumn(chip, 1);
        top.Children.Add(chip);
        sp.Children.Add(top);

        var btns = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        foreach (var b in JobButtons(c, Reload)) btns.Children.Add(b);

        btns.Children.Add(Ui.Btn("📁 " + Loc.T("የደንበኛ Folder ክፈት", "Open Client Folder"), () => OpenFolder(St.ClientFolderPath(c)), 1, 12.5));

        btns.Children.Add(Ui.Btn("⬆ " + Loc.T("የተጠናቀቁ ፎቶዎች ጨምር (Final)", "Import Edited Finals"), () => Safe(async () =>
        {
            var d = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Filter = "Images|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.psd|All|*.*" };
            if (d.ShowDialog() != true) return;
            await St.UploadFinalAsync(c.Id, d.FileNames);
            C.Toast?.Invoke($"✔ {d.FileNames.Length} " + Loc.T("ፋይል ወደ Final ተጨምሯል", "files added to Final"));
        }), 1, 12.5));

        if (c.Status is JobStatus.Review or JobStatus.Delivered)
            btns.Children.Add(Ui.Btn("💾 " + Loc.T("ወደ USB / ፍላሽ ቅዳ", "Export to USB/Drive"), () => Safe(() => Deliver(c)), 1, 12.5));

        sp.Children.Add(btns);
        return new Border
        {
            Background = Ui.Panel2,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 10),
            Child = sp
        };
    }

    async Task Deliver(Client c)
    {
        string fin = Path.Combine(St.ClientFolderPath(c), "Final");
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
