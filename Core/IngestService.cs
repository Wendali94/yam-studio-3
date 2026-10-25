using System.Collections.Concurrent;

namespace YamStudio.Core;

/// <summary>
/// Receives photos from the camera (built-in FTP and/or a watched folder) and files them under the ACTIVE client.
/// If no client is active yet, files wait in _Inbox and are filed the moment a client is chosen.
/// </summary>
public class IngestService
{
    readonly LocalStudio _studio;
    readonly AppSettings _s;
    readonly string _inbox;
    FtpServer _ftp;
    FileSystemWatcher _fsw, _fswExtra;
    readonly BlockingCollection<(string path, bool move)> _queue = new();
    int _active;

    public int ReceivedToday;
    public string FtpStatus { get; private set; } = "";
    public string InboxDir => _inbox;
    public event Action<int, Photo> Filed;
    public event Action<string> Info;

    public IngestService(LocalStudio studio, AppSettings s)
    {
        _studio = studio; _s = s;
        _inbox = Path.Combine(s.RootDir, "_Inbox");
        Directory.CreateDirectory(_inbox);
        _active = s.ActiveClientId;
    }

    public int ActiveClientId
    {
        get => _active;
        set
        {
            _active = value;
            _s.ActiveClientId = value; SettingsStore.Save();
            if (value > 0) DrainInbox();
        }
    }

    public void Start()
    {
        _ftp = new FtpServer(_inbox, _s.FtpPort, _s.FtpUser, _s.FtpPass);
        _ftp.FileReceived += p => Enqueue(p, true);
        _ftp.Log += m => Info?.Invoke(m);
        FtpStatus = _ftp.Start() ? $"FTP {_s.FtpPort}" : "FTP ✕";

        _fsw = Watch(_inbox, true);
        if (!string.IsNullOrWhiteSpace(_s.WatchDir) && Directory.Exists(_s.WatchDir)) _fswExtra = Watch(_s.WatchDir, false);

        var t = new Thread(Worker) { IsBackground = true, Name = "ingest" };
        t.Start();
        DrainInbox();
    }

    FileSystemWatcher Watch(string dir, bool move)
    {
        var w = new FileSystemWatcher(dir) { IncludeSubdirectories = false, EnableRaisingEvents = true };
        w.Created += (_, e) => { if (!e.FullPath.EndsWith(".part")) Enqueue(e.FullPath, move); };
        w.Renamed += (_, e) => { if (!e.FullPath.EndsWith(".part")) Enqueue(e.FullPath, move); };
        return w;
    }

    readonly ConcurrentDictionary<string, byte> _pending = new();

    void Enqueue(string path, bool move)
    {
        if (!LocalStudio.IsPhotoFile(path)) return;
        if (_pending.TryAdd(path, 0)) _queue.Add((path, move));
    }

    void DrainInbox()
    {
        try { foreach (var f in Directory.GetFiles(_inbox)) Enqueue(f, true); } catch { }
    }

    void Worker()
    {
        foreach (var (path, move) in _queue.GetConsumingEnumerable())
        {
            try
            {
                if (_active <= 0)
                {
                    // Nobody chosen yet: keep the file in the inbox. Files from an extra watched folder are left where they are.
                    _pending.TryRemove(path, out _);
                    continue;
                }
                var photo = _studio.Ingest(_active, path, move);
                _pending.TryRemove(path, out _);
                if (photo != null)
                {
                    Interlocked.Increment(ref ReceivedToday);
                    Filed?.Invoke(_active, photo);
                }
            }
            catch (Exception e)
            {
                _pending.TryRemove(path, out _);
                Info?.Invoke("Ingest: " + e.Message);
            }
        }
    }

    public int WaitingInInbox()
    {
        try { return Directory.GetFiles(_inbox).Count(f => LocalStudio.IsPhotoFile(f)); } catch { return 0; }
    }

    public void Stop()
    {
        try { _ftp?.Stop(); _fsw?.Dispose(); _fswExtra?.Dispose(); _queue.CompleteAdding(); } catch { }
    }
}
