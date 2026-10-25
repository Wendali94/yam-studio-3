using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using YamStudio.Core;

namespace YamStudio.Core;

/// <summary>
/// Receives photos from FTP and watched folders.
/// Files are stored under the active client.
/// If no client is active, files remain in the inbox
/// until a client is selected.
/// </summary>
public class IngestService
{
    private readonly LocalStudio _studio;
    private readonly AppSettings _s;
    private readonly string _inbox;

    private FtpServer _ftp;
    private FileSystemWatcher _fsw;
    private FileSystemWatcher _fswExtra;

    private readonly BlockingCollection<(string path, bool move)> _queue = new();
    private readonly ConcurrentDictionary<string, byte> _pending = new(
        StringComparer.OrdinalIgnoreCase
    );

    private int _active;
    private int _stopping;

    public int ReceivedToday;

    public string FtpStatus { get; private set; } = "";

    public string InboxDir => _inbox;

    public event Action<int, Photo> Filed;
    public event Action<string> Info;

    public IngestService(LocalStudio studio, AppSettings s)
    {
        _studio = studio ?? throw new ArgumentNullException(nameof(studio));
        _s = s ?? throw new ArgumentNullException(nameof(s));

        _inbox = Path.Combine(_s.RootDir, "_Inbox");

        Directory.CreateDirectory(_inbox);

        _active = _s.ActiveClientId;
    }

    public int ActiveClientId
    {
        get => Volatile.Read(ref _active);

        set
        {
            if (Volatile.Read(ref _stopping) != 0)
                return;

            Volatile.Write(ref _active, value);

            _s.ActiveClientId = value;
            SettingsStore.Save();

            if (value > 0)
                DrainInbox();
        }
    }

    public void Start()
    {
        if (Volatile.Read(ref _stopping) != 0)
            throw new ObjectDisposedException(nameof(IngestService));

        Directory.CreateDirectory(_inbox);

        // Start the background worker before receiving files.
        var worker = new Thread(Worker)
        {
            IsBackground = true,
            Name = "YamStudio-Ingest"
        };

        worker.Start();

        // Start the FTP receiver.
        try
        {
            _ftp = new FtpServer(
                _inbox,
                _s.FtpPort,
                _s.FtpUser,
                _s.FtpPass
            );

            _ftp.FileReceived += path => Enqueue(path, true);
            _ftp.Log += message => Info?.Invoke(message);

            FtpStatus = _ftp.Start()
                ? $"FTP {_s.FtpPort}"
                : "FTP unavailable";
        }
        catch (Exception ex)
        {
            FtpStatus = "FTP unavailable";
            Info?.Invoke("FTP startup failed: " + ex.Message);
        }

        // Watch the internal inbox.
        try
        {
            _fsw = Watch(_inbox, true);
        }
        catch (Exception ex)
        {
            Info?.Invoke("Inbox watcher failed: " + ex.Message);
        }

        // Optionally watch an external camera/import folder.
        if (!string.IsNullOrWhiteSpace(_s.WatchDir))
        {
            try
            {
                if (Directory.Exists(_s.WatchDir))
                    _fswExtra = Watch(_s.WatchDir, false);
            }
            catch (Exception ex)
            {
                Info?.Invoke("External folder watcher failed: " + ex.Message);
            }
        }

        // Process files that already existed before startup.
        DrainInbox();
    }

    private FileSystemWatcher Watch(string dir, bool move)
    {
        var watcher = new FileSystemWatcher(dir)
        {
            IncludeSubdirectories = false,
            NotifyFilter =
                NotifyFilters.FileName |
                NotifyFilters.LastWrite |
                NotifyFilters.Size,
            EnableRaisingEvents = false
        };

        watcher.Created += (_, e) =>
        {
            if (!IsTemporaryFile(e.FullPath))
                Enqueue(e.FullPath, move);
        };

        watcher.Renamed += (_, e) =>
        {
            if (!IsTemporaryFile(e.FullPath))
                Enqueue(e.FullPath, move);
        };

        watcher.Error += (_, e) =>
        {
            Info?.Invoke(
                "Folder watcher error: " +
                e.GetException().Message
            );
        };

        watcher.EnableRaisingEvents = true;

        return watcher;
    }

    private static bool IsTemporaryFile(string path)
    {
        return path.EndsWith(
            ".part",
            StringComparison.OrdinalIgnoreCase
        );
    }

    private void Enqueue(string path, bool move)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (Volatile.Read(ref _stopping) != 0)
            return;

        if (_queue.IsAddingCompleted)
            return;

        if (!LocalStudio.IsPhotoFile(path))
            return;

        // Prevent duplicate notifications from FTP and filesystem events.
        if (!_pending.TryAdd(path, 0))
            return;

        try
        {
            if (!_queue.IsAddingCompleted)
            {
                _queue.Add((path, move));
            }
            else
            {
                _pending.TryRemove(path, out _);
            }
        }
        catch (InvalidOperationException)
        {
            _pending.TryRemove(path, out _);
        }
        catch (ObjectDisposedException)
        {
            _pending.TryRemove(path, out _);
        }
    }

    private void DrainInbox()
    {
        if (Volatile.Read(ref _stopping) != 0)
            return;

        try
        {
            foreach (string file in Directory.GetFiles(_inbox))
            {
                Enqueue(file, true);
            }
        }
        catch (Exception ex)
        {
            Info?.Invoke("Unable to scan inbox: " + ex.Message);
        }
    }

    private void Worker()
    {
        try
        {
            foreach (var item in _queue.GetConsumingEnumerable())
            {
                string path = item.path;
                bool move = item.move;

                try
                {
                    if (Volatile.Read(ref _stopping) != 0)
                        continue;

                    int clientId = Volatile.Read(ref _active);

                    if (clientId <= 0)
                    {
                        // Leave files in the inbox until a client is selected.
                        continue;
                    }

                    // Ingest the file for the currently active client.
                    Photo photo = _studio.Ingest(clientId, path, move);

                    if (photo != null)
                    {
                        Interlocked.Increment(ref ReceivedToday);

                        try
                        {
                            Filed?.Invoke(clientId, photo);
                        }
                        catch (Exception ex)
                        {
                            Info?.Invoke(
                                "Photo notification failed: " + ex.Message
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    Info?.Invoke("Ingest failed: " + ex.Message);
                }
                finally
                {
                    _pending.TryRemove(path, out _);
                }
            }
        }
        catch (Exception ex)
        {
            Info?.Invoke("Ingest worker stopped: " + ex.Message);
        }
    }

    public int WaitingInInbox()
    {
        try
        {
            return Directory
                .GetFiles(_inbox)
                .Count(file => LocalStudio.IsPhotoFile(file));
        }
        catch
        {
            return 0;
        }
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopping, 1) != 0)
            return;

        // Stop file notifications before completing the queue.
        try
        {
            if (_fsw != null)
            {
                _fsw.EnableRaisingEvents = false;
                _fsw.Dispose();
                _fsw = null;
            }
        }
        catch (Exception ex)
        {
            Info?.Invoke("Inbox watcher shutdown failed: " + ex.Message);
        }

        try
        {
            if (_fswExtra != null)
            {
                _fswExtra.EnableRaisingEvents = false;
                _fswExtra.Dispose();
                _fswExtra = null;
            }
        }
        catch (Exception ex)
        {
            Info?.Invoke("External watcher shutdown failed: " + ex.Message);
        }

        try
        {
            _ftp?.Stop();
            _ftp = null;
        }
        catch (Exception ex)
        {
            Info?.Invoke("FTP shutdown failed: " + ex.Message);
        }

        try
        {
            _queue.CompleteAdding();
        }
        catch (InvalidOperationException)
        {
            // The queue has already been completed.
        }
    }
}
