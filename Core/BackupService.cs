namespace YamStudio.Core;

public class BackupService
{
    readonly LocalStudio _studio;
    readonly AppSettings _s;
    System.Threading.Timer _timer;
    int _running;

    public BackupService(LocalStudio studio, AppSettings s) { _studio = studio; _s = s; }

    public void StartSchedule()
    {
        _timer = new System.Threading.Timer(_ =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_s.BackupDir)) return;
                string today = EthCal.TodayIso();
                if (DateTime.Now.ToString("HH:mm").CompareTo(_s.BackupTime) >= 0 && !(_s.LastBackup ?? "").StartsWith(today))
                    Run();
            }
            catch { }
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));
    }

    public void Stop() => _timer?.Dispose();

    public string Run()
    {
        if (string.IsNullOrWhiteSpace(_s.BackupDir)) return Loc.T("የ backup ቦታ አልተመረጠም", "No backup location set");
        if (Interlocked.Exchange(ref _running, 1) == 1) return Loc.T("backup እየተሰራ ነው", "Backup already running");
        try
        {
            Directory.CreateDirectory(_s.BackupDir);
            string dbCopy = Path.Combine(_s.BackupDir, "yam.db");
            _studio.Db.BackupTo(dbCopy);
            int copied = CopyTree(_studio.ClientsRoot, Path.Combine(_s.BackupDir, "Clients"));
            _s.LastBackup = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            SettingsStore.Save();
            return Loc.T($"ተጠናቋል · {copied} አዲስ ፋይል", $"Done · {copied} new files");
        }
        catch (Exception e) { return Loc.T("ስህተት: ", "Error: ") + e.Message; }
        finally { Interlocked.Exchange(ref _running, 0); }
    }

    static int CopyTree(string src, string dst)
    {
        int n = 0;
        if (!Directory.Exists(src)) return 0;
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src))
        {
            string d = Path.Combine(dst, Path.GetFileName(f));
            var fi = new FileInfo(f);
            if (File.Exists(d))
            {
                var di = new FileInfo(d);
                if (di.Length == fi.Length && di.LastWriteTimeUtc >= fi.LastWriteTimeUtc) continue;
            }
            File.Copy(f, d, true);
            n++;
        }
        foreach (var sub in Directory.GetDirectories(src)) n += CopyTree(sub, Path.Combine(dst, Path.GetFileName(sub)));
        return n;
    }
}
