using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace YamStudio.Core;

/// <summary>PC2 side: same IStudio calls, carried over the studio LAN to PC1.</summary>
public class RemoteStudio : IStudio
{
    readonly HttpClient _http;
    readonly AppSettings _s;
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);
    public bool IsHost => false;

    public RemoteStudio(AppSettings s)
    {
        _s = s;
        _http = new HttpClient { BaseAddress = new Uri($"http://{s.HostAddress}:{s.ApiPort}/"), Timeout = TimeSpan.FromMinutes(10) };
        _http.DefaultRequestHeaders.Add("X-Pin", s.Pin);
    }

    public static async Task<string> TestAsync(string host, int port, string pin)
    {
        try
        {
            using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            var ping = await h.GetAsync($"http://{host}:{port}/api/ping");
            if (!ping.IsSuccessStatusCode) return Loc.T("PC1 አልተገኘም", "PC1 not found");
            var req = new HttpRequestMessage(HttpMethod.Get, $"http://{host}:{port}/api/clients");
            req.Headers.Add("X-Pin", pin);
            var r = await h.SendAsync(req);
            return r.IsSuccessStatusCode ? "OK" : Loc.T("PIN ትክክል አይደለም", "Wrong PIN");
        }
        catch { return Loc.T("PC1 አልተገኘም (IP/ገመድ ያረጋግጡ)", "Cannot reach PC1 (check IP/cable)"); }
    }

    public async Task<List<Client>> GetClientsAsync() => await _http.GetFromJsonAsync<List<Client>>("api/clients", J) ?? new();

    public async Task<int> SaveClientAsync(Client c)
    {
        var r = await _http.PostAsJsonAsync("api/clients", c, J);
        r.EnsureSuccessStatusCode();
        var o = await r.Content.ReadFromJsonAsync<JsonElement>(J);
        return o.GetProperty("id").GetInt32();
    }

    public async Task DeleteClientAsync(int id) => (await _http.DeleteAsync($"api/clients/{id}")).EnsureSuccessStatusCode();

    public async Task SetStatusAsync(int id, string status) =>
        (await _http.PostAsync($"api/clients/{id}/status?s={Uri.EscapeDataString(status)}", null)).EnsureSuccessStatusCode();

    public async Task AddPaymentAsync(int id, decimal amount, string dateIso) =>
        (await _http.PostAsJsonAsync($"api/clients/{id}/payments", new PayDto(amount, dateIso), J)).EnsureSuccessStatusCode();

    public async Task<List<PaymentRow>> GetPaymentsAsync(string fromIso, string toIso) =>
        await _http.GetFromJsonAsync<List<PaymentRow>>($"api/payments?from={fromIso}&to={toIso}", J) ?? new();

    public async Task<List<Photo>> GetPhotosAsync(int clientId) =>
        await _http.GetFromJsonAsync<List<Photo>>($"api/clients/{clientId}/photos", J) ?? new();

    public async Task<byte[]> GetImageAsync(int photoId, bool thumb)
    {
        var r = await _http.GetAsync($"api/photos/{photoId}/image?thumb={thumb.ToString().ToLower()}");
        return r.IsSuccessStatusCode ? await r.Content.ReadAsByteArrayAsync() : null;
    }

    public async Task SetSelectedAsync(int photoId, bool selected) =>
        (await _http.PostAsync($"api/photos/{photoId}/selected?v={selected.ToString().ToLower()}", null)).EnsureSuccessStatusCode();

    public async Task<string> ConfirmSelectionAsync(int clientId)
    {
        var r = await _http.PostAsync($"api/clients/{clientId}/confirm", null);
        r.EnsureSuccessStatusCode();
        var o = await r.Content.ReadFromJsonAsync<JsonElement>(J);
        return o.GetProperty("folder").GetString();
    }

    public async Task<string> FetchSelectedAsync(int clientId, string localWorkDir)
    {
        var clients = await GetClientsAsync();
        var c = clients.FirstOrDefault(x => x.Id == clientId);
        string name = c == null || string.IsNullOrEmpty(c.Folder) ? "client_" + clientId : c.Folder;
        string dir = Path.Combine(localWorkDir, name);
        Directory.CreateDirectory(dir);
        var files = await _http.GetFromJsonAsync<List<string>>($"api/clients/{clientId}/selected", J) ?? new();
        foreach (var f in files)
        {
            string dest = Path.Combine(dir, f);
            using var resp = await _http.GetAsync($"api/clients/{clientId}/selected/{Uri.EscapeDataString(f)}", HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            long len = resp.Content.Headers.ContentLength ?? -1;
            if (File.Exists(dest) && len > 0 && new FileInfo(dest).Length == len) continue; // already copied
            await using var fs = File.Create(dest);
            await resp.Content.CopyToAsync(fs);
        }
        return dir;
    }

    public async Task UploadFinalAsync(int clientId, IEnumerable<string> files)
    {
        foreach (var f in files)
        {
            await using var fs = File.OpenRead(f);
            var r = await _http.PutAsync($"api/clients/{clientId}/final/{Uri.EscapeDataString(Path.GetFileName(f))}", new StreamContent(fs));
            r.EnsureSuccessStatusCode();
        }
    }

    public string ClientFolderPath(Client c) => "";
}
