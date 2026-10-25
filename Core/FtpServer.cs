using System.Net;
using System.Net.Sockets;
using System.Text;

namespace YamStudio.Core;

/// <summary>
/// Small built-in FTP server so the Sony a7 IV can push photos straight to this PC (camera menu: Network > FTP Transfer).
/// Supports passive/active mode, upload only. Finished files are announced through FileReceived.
/// </summary>
public class FtpServer
{
    readonly string _inbox, _user, _pass;
    readonly int _port;
    TcpListener _listener;
    CancellationTokenSource _cts;

    public event Action<string> FileReceived;
    public event Action<string> Log;

    public FtpServer(string inboxDir, int port, string user, string pass)
    {
        _inbox = inboxDir; _port = port; _user = user; _pass = pass;
        Directory.CreateDirectory(_inbox);
    }

    public bool Start()
    {
        try
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _ = Task.Run(() => AcceptLoop(_cts.Token));
            return true;
        }
        catch (Exception e) { Log?.Invoke("FTP start failed: " + e.Message); return false; }
    }

    public void Stop()
    {
        try { _cts?.Cancel(); _listener?.Stop(); } catch { }
    }

    async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var c = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => Session(c, ct));
            }
            catch { if (ct.IsCancellationRequested) return; }
        }
    }

    async Task Session(TcpClient ctl, CancellationToken ct)
    {
        using var _ = ctl;
        var ns = ctl.GetStream();
        var rd = new StreamReader(ns, new UTF8Encoding(false));
        var localIp = ((IPEndPoint)ctl.Client.LocalEndPoint).Address;
        async Task Send(string s) { var b = Encoding.UTF8.GetBytes(s + "\r\n"); await ns.WriteAsync(b, ct); }

        TcpListener pasv = null;
        IPEndPoint activeTarget = null;
        string user = null; bool authed = false; string renameFrom = null;
        try
        {
            await Send("220 YAM Studio ready");
            while (!ct.IsCancellationRequested)
            {
                string line = await rd.ReadLineAsync();
                if (line == null) break;
                string cmd = line, arg = "";
                int sp = line.IndexOf(' ');
                if (sp > 0) { cmd = line.Substring(0, sp); arg = line.Substring(sp + 1).Trim(); }
                cmd = cmd.ToUpperInvariant();

                if (cmd == "USER") { user = arg; await Send("331 Password required"); continue; }
                if (cmd == "PASS")
                {
                    authed = user == _user && arg == _pass;
                    await Send(authed ? "230 Logged in" : "530 Login incorrect");
                    continue;
                }
                if (cmd == "QUIT") { await Send("221 Bye"); break; }
                if (cmd == "SYST") { await Send("215 UNIX Type: L8"); continue; }
                if (cmd == "FEAT") { await Send("211-Features:\r\n EPSV\r\n SIZE\r\n UTF8\r\n211 End"); continue; }
                if (cmd == "OPTS") { await Send("200 OK"); continue; }
                if (cmd == "NOOP") { await Send("200 OK"); continue; }
                if (!authed) { await Send("530 Please login"); continue; }

                switch (cmd)
                {
                    case "PWD": case "XPWD": await Send("257 \"/\""); break;
                    case "CWD": case "CDUP": case "TYPE": case "MODE": case "STRU": await Send("200 OK"); break;
                    case "MKD": case "XMKD": await Send("257 \"" + arg + "\" created"); break;
                    case "RMD": case "XRMD": case "DELE": await Send("250 OK"); break;
                    case "SIZE": await Send("550 Not found"); break;
                    case "MDTM": await Send("550 Not found"); break;
                    case "RNFR": renameFrom = arg; await Send("350 Ready"); break;
                    case "RNTO": await Send("250 OK"); break;
                    case "PASV":
                        pasv?.Stop();
                        pasv = new TcpListener(localIp, 0);
                        pasv.Start();
                        activeTarget = null;
                        int pp = ((IPEndPoint)pasv.LocalEndpoint).Port;
                        var ib = localIp.GetAddressBytes();
                        await Send($"227 Entering Passive Mode ({ib[0]},{ib[1]},{ib[2]},{ib[3]},{pp / 256},{pp % 256})");
                        break;
                    case "EPSV":
                        pasv?.Stop();
                        pasv = new TcpListener(localIp, 0);
                        pasv.Start();
                        activeTarget = null;
                        await Send($"229 Entering Extended Passive Mode (|||{((IPEndPoint)pasv.LocalEndpoint).Port}|)");
                        break;
                    case "PORT":
                        try
                        {
                            var p = arg.Split(',').Select(int.Parse).ToArray();
                            activeTarget = new IPEndPoint(new IPAddress(new byte[] { (byte)p[0], (byte)p[1], (byte)p[2], (byte)p[3] }), p[4] * 256 + p[5]);
                            pasv?.Stop(); pasv = null;
                            await Send("200 PORT ok");
                        }
                        catch { await Send("501 Bad PORT"); }
                        break;
                    case "LIST": case "NLST": case "MLSD":
                        {
                            await Send("150 Opening data connection");
                            var dc = await OpenData(pasv, activeTarget, ct);
                            if (dc == null) { await Send("425 No data connection"); break; }
                            using (dc) { }
                            pasv?.Stop(); pasv = null; activeTarget = null;
                            await Send("226 Done");
                            break;
                        }
                    case "STOR": case "APPE":
                        {
                            string name = Path.GetFileName(arg.Replace('\\', '/'));
                            if (string.IsNullOrWhiteSpace(name)) { await Send("553 Bad name"); break; }
                            foreach (var ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch.ToString(), "_");
                            await Send("150 Ready to receive");
                            var dc = await OpenData(pasv, activeTarget, ct);
                            if (dc == null) { await Send("425 No data connection"); break; }
                            string tmp = Path.Combine(_inbox, name + ".part");
                            string fin = Path.Combine(_inbox, name);
                            try
                            {
                                using (dc)
                                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                                    await dc.GetStream().CopyToAsync(fs, ct);
                                if (File.Exists(fin)) File.Delete(fin);
                                File.Move(tmp, fin);
                                await Send("226 Transfer complete");
                                FileReceived?.Invoke(fin);
                            }
                            catch (Exception e)
                            {
                                Log?.Invoke("FTP receive failed: " + e.Message);
                                try { File.Delete(tmp); } catch { }
                                await Send("426 Transfer failed");
                            }
                            pasv?.Stop(); pasv = null; activeTarget = null;
                            break;
                        }
                    default: await Send("502 Not implemented"); break;
                }
            }
        }
        catch { }
        finally { try { pasv?.Stop(); } catch { } }
    }

    static async Task<TcpClient> OpenData(TcpListener pasv, IPEndPoint active, CancellationToken ct)
    {
        try
        {
            using var to = CancellationTokenSource.CreateLinkedTokenSource(ct);
            to.CancelAfter(TimeSpan.FromSeconds(20));
            if (pasv != null) return await pasv.AcceptTcpClientAsync(to.Token);
            if (active != null)
            {
                var c = new TcpClient();
                await c.ConnectAsync(active.Address, active.Port, to.Token);
                return c;
            }
        }
        catch { }
        return null;
    }
}
