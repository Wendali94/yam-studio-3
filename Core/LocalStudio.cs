using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YamStudio.Core;

/// <summary>PC1: owns the database and all photo files on disk.</summary>
public class LocalStudio : IStudio
{
    public readonly Db Db;
    readonly AppSettings _s;
    public bool IsHost => true;

    /// <summary>Raised on any thread when a photo has arrived and been filed.</summary>
    public event Action<int, Photo> PhotoIngested;

    public LocalStudio(AppSettings s)
    {
        _s = s;
        Directory.CreateDirectory(ClientsRoot);
        Directory.CreateDirectory(CacheRoot);
        Db = new Db(Path.Combine(_s.RootDir, "yam.db"));
    }

    public string ClientsRoot => Path.Combine(_s.RootDir, "Clients");
    string CacheRoot => Path.Combine(_s.RootDir, ".cache");

    // ---------- folders ----------
    public static string Sanitize(string s)
    {
        foreach (var ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch.ToString(), "");
        return s.Trim().TrimEnd('.');
    }

    public static string FolderNameFor(Client c) => $"{Sanitize(c.Name)}_{c.Phone}_{EthCal.FileDate(c.Date)}";

    public string ClientFolderPath(Client c) =>
        string.IsNullOrEmpty(c.Folder) ? "" : Path.Combine(ClientsRoot, c.Folder);

    string EnsureFolder(Client c)
    {
        if (string.IsNullOrEmpty(c.Folder))
        {
            c.Folder = FolderNameFor(c);
            Db.SetFolder(c.Id, c.Folder);
        }
        string root = Path.Combine(ClientsRoot, c.Folder);
        foreach (var sub in new[] { "Originals", "Selected", "Edited", "Final" })
            Directory.CreateDirectory(Path.Combine(root, sub));
        return root;
    }

    // ---------- clients ----------
    public Task<List<Client>> GetClientsAsync() => Task.FromResult(Db.GetClients());

    public Task<int> SaveClientAsync(Client c)
    {
        int id = Db.SaveClient(c);
        return Task.FromResult(id);
    }

    public Task DeleteClientAsync(int id)
    {
        // Photos on disk are never deleted by the app; only the records are removed.
        Db.DeleteClient(id);
        return Task.CompletedTask;
    }

    public Task SetStatusAsync(int id, string status) { Db.SetStatus(id, status); return Task.CompletedTask; }

    public Task AddPaymentAsync(int id, decimal amount, string dateIso) { Db.AddPayment(id, amount, dateIso); return Task.CompletedTask; }

    public Task<List<PaymentRow>> GetPaymentsAsync(string fromIso, string toIso) => Task.FromResult(Db.GetPaymentsBetween(fromIso, toIso));

    // ---------- ingest (camera -> client folder) ----------
    public static bool IsPhotoFile(string path)
    {
        var e = Path.GetExtension(path).ToLowerInvariant();
        return e is ".jpg" or ".jpeg" or ".png" or ".arw" or ".dng";
    }

    /// <summary>Files the camera/watcher dropped for the active client. Waits until the file is fully written.</summary>
    public Photo Ingest(int clientId, string srcPath, bool move)
    {
        var c = Db.GetClient(clientId);
        if (c == null || !File.Exists(srcPath)) return null;
        WaitUntilStable(srcPath);
        string root = EnsureFolder(c);
        string orig = Path.Combine(root, "Originals");
        string name = Path.GetFileName(srcPath);
        string dest = Path.Combine(orig, name);
        if (File.Exists(dest) && new FileInfo(dest).Length != new FileInfo(srcPath).Length)
        {
            string b = Path.GetFileNameWithoutExtension(name), e = Path.GetExtension(name);
            int n = 2;
            while (File.Exists(dest)) dest = Path.Combine(orig, $"{b}_{n++}{e}");
        }
        long expected = new FileInfo(srcPath).Length;
        if (!File.Exists(dest))
        {
            File.Copy(srcPath, dest, false);
            if (new FileInfo(dest).Length != expected) // verify the copy is complete
            {
                File.Delete(dest);
                throw new IOException("Copy verification failed: " + name);
            }
        }
        if (move) { try { File.Delete(srcPath); } catch { } }
        var photo = Db.UpsertPhoto(clientId, Path.GetFileName(dest));
        if (c.Status == JobStatus.Booked) Db.SetStatus(clientId, JobStatus.Shot);
        PhotoIngested?.Invoke(clientId, photo);
        return photo;
    }

    static void WaitUntilStable(string path)
    {
        long last = -1;
        for (int i = 0; i < 120; i++)
        {
            try
            {
                long len = new FileInfo(path).Length;
                if (len == last && len > 0)
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return;
                }
                last = len;
            }
            catch (IOException) { }
            Thread.Sleep(250);
        }
    }

    // ---------- photos ----------
    public Task<List<Photo>> GetPhotosAsync(int clientId) => Task.FromResult(Db.GetPhotos(clientId));

    public Task SetSelectedAsync(int photoId, bool selected) { Db.SetSelected(photoId, selected); return Task.CompletedTask; }

    public Task<byte[]> GetImageAsync(int photoId, bool thumb) => Task.Run(() =>
    {
        var p = Db.GetPhoto(photoId);
        if (p == null) return (byte[])null;
        string cache = Path.Combine(CacheRoot, $"{photoId}{(thumb ? "_t" : "_f")}.jpg");
        if (File.Exists(cache)) return File.ReadAllBytes(cache);
        var c = Db.GetClient(p.ClientId);
        if (c == null) return null;
        string orig = Path.Combine(ClientsRoot, c.Folder, "Originals");
        byte[] full = null;
        if (!string.IsNullOrEmpty(p.JpgFile) && File.Exists(Path.Combine(orig, p.JpgFile)))
            full = File.ReadAllBytes(Path.Combine(orig, p.JpgFile));
        else if (!string.IsNullOrEmpty(p.RawFile) && File.Exists(Path.Combine(orig, p.RawFile)))
            full = RawPreview.ExtractLargestJpeg(Path.Combine(orig, p.RawFile));
        if (full == null) return null;
        byte[] result = Resize(full, thumb ? 360 : 2200);
        try { File.WriteAllBytes(cache, result); } catch { }
        return result;
    });

    static byte[] Resize(byte[] src, int maxWidth)
    {
        using var ms = new MemoryStream(src);
        var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
        BitmapSource bs = dec.Frames[0];
        int orient = 1;
        try
        {
            if (dec.Frames[0].Metadata is BitmapMetadata md && md.ContainsQuery("/app1/ifd/{ushort=274}"))
                orient = Convert.ToInt32(md.GetQuery("/app1/ifd/{ushort=274}"));
        }
        catch { }
        if (bs.PixelWidth <= maxWidth && orient == 1) return src;
        if (bs.PixelWidth > maxWidth)
        {
            double k = maxWidth / (double)bs.PixelWidth;
            bs = new TransformedBitmap(bs, new ScaleTransform(k, k));
        }
        int angle = orient == 6 ? 90 : orient == 3 ? 180 : orient == 8 ? 270 : 0;
        if (angle != 0) bs = new TransformedBitmap(bs, new RotateTransform(angle));
        var enc = new JpegBitmapEncoder { QualityLevel = 88 };
        enc.Frames.Add(BitmapFrame.Create(bs));
        using var o = new MemoryStream();
        enc.Save(o);
        return o.ToArray();
    }

    // ---------- selection / handover ----------
    public Task<string> ConfirmSelectionAsync(int clientId) => Task.Run(() =>
    {
        var c = Db.GetClient(clientId) ?? throw new InvalidOperationException("client");
        string root = EnsureFolder(c);
        string orig = Path.Combine(root, "Originals"), sel = Path.Combine(root, "Selected");
        foreach (var f in Directory.GetFiles(sel)) File.Delete(f);
        foreach (var p in Db.GetPhotos(clientId).Where(x => x.Selected))
            foreach (var f in new[] { p.JpgFile, p.RawFile }.Where(f => !string.IsNullOrEmpty(f)))
            {
                string s = Path.Combine(orig, f);
                if (File.Exists(s)) File.Copy(s, Path.Combine(sel, f), true);
            }
        Db.SetStatus(clientId, JobStatus.Selected);
        return c.Folder;
    });

    public Task<string> FetchSelectedAsync(int clientId, string localWorkDir)
    {
        var c = Db.GetClient(clientId);
        return Task.FromResult(c == null ? "" : Path.Combine(ClientsRoot, c.Folder, "Selected"));
    }

    public Task UploadFinalAsync(int clientId, IEnumerable<string> files)
    {
        var c = Db.GetClient(clientId);
        if (c == null) return Task.CompletedTask;
        string fin = Path.Combine(EnsureFolder(c), "Final");
        foreach (var f in files) File.Copy(f, Path.Combine(fin, Path.GetFileName(f)), true);
        return Task.CompletedTask;
    }

    /// <summary>Used by the LAN API: zip of the Selected folder.</summary>
    public string SelectedDirOf(int clientId)
    {
        var c = Db.GetClient(clientId);
        return c == null || string.IsNullOrEmpty(c.Folder) ? "" : Path.Combine(ClientsRoot, c.Folder, "Selected");
    }

    public string FinalDirOf(int clientId)
    {
        var c = Db.GetClient(clientId);
        return c == null ? "" : Path.Combine(EnsureFolder(c), "Final");
    }
}
