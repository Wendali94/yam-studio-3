using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace YamStudio.Core;

public record PayDto(decimal Amount, string Date);

/// <summary>PC1 side: small web API on the studio LAN. PC2 uses it through RemoteStudio. Protected by the PIN shown on PC1.</summary>
public class LanApi
{
    readonly LocalStudio _st;
    readonly AppSettings _s;
    WebApplication _app;

    public LanApi(LocalStudio st, AppSettings s) { _st = st; _s = s; }

    public bool Start()
    {
        try
        {
            var b = WebApplication.CreateBuilder();
            b.Logging.ClearProviders();
            b.WebHost.UseUrls($"http://0.0.0.0:{_s.ApiPort}");
            b.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
            _app = b.Build();

            _app.Use(async (ctx, next) =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api/ping")) { await next(); return; }
                if (ctx.Request.Headers["X-Pin"] != _s.Pin) { ctx.Response.StatusCode = 401; return; }
                await next();
            });

            _app.MapGet("/api/ping", () => "yam");
            _app.MapGet("/api/clients", () => _st.Db.GetClients());
            _app.MapPost("/api/clients", async (Client c) => new { id = await _st.SaveClientAsync(c) });
            _app.MapDelete("/api/clients/{id:int}", async (int id) => { await _st.DeleteClientAsync(id); return Results.Ok(); });
            _app.MapPost("/api/clients/{id:int}/status", async (int id, string s) => { await _st.SetStatusAsync(id, s); return Results.Ok(); });
            _app.MapPost("/api/clients/{id:int}/payments", async (int id, PayDto p) => { await _st.AddPaymentAsync(id, p.Amount, p.Date); return Results.Ok(); });
            _app.MapGet("/api/payments", (string from, string to) => _st.Db.GetPaymentsBetween(from, to));
            _app.MapGet("/api/clients/{id:int}/photos", (int id) => _st.Db.GetPhotos(id));
            _app.MapGet("/api/photos/{id:int}/image", async (int id, bool thumb) =>
            {
                var bytes = await _st.GetImageAsync(id, thumb);
                return bytes == null ? Results.NotFound() : Results.File(bytes, "image/jpeg");
            });
            _app.MapPost("/api/photos/{id:int}/selected", async (int id, bool v) => { await _st.SetSelectedAsync(id, v); return Results.Ok(); });
            _app.MapPost("/api/clients/{id:int}/confirm", async (int id) => new { folder = await _st.ConfirmSelectionAsync(id) });

            _app.MapGet("/api/clients/{id:int}/selected", (int id) =>
            {
                string d = _st.SelectedDirOf(id);
                return Directory.Exists(d) ? Directory.GetFiles(d).Select(f => Path.GetFileName(f)).ToList() : new List<string>();
            });
            _app.MapGet("/api/clients/{id:int}/selected/{file}", (int id, string file) =>
            {
                string p = Path.Combine(_st.SelectedDirOf(id), Path.GetFileName(file));
                return File.Exists(p) ? Results.File(File.OpenRead(p), "application/octet-stream") : Results.NotFound();
            });
            _app.MapPut("/api/clients/{id:int}/final/{file}", async (int id, string file, HttpRequest req) =>
            {
                string dir = _st.FinalDirOf(id);
                string p = Path.Combine(dir, Path.GetFileName(file));
                string tmp = p + ".part";
                await using (var fs = File.Create(tmp)) await req.Body.CopyToAsync(fs);
                File.Move(tmp, p, true);
                return Results.Ok();
            });

            _app.StartAsync().GetAwaiter().GetResult();
            return true;
        }
        catch { return false; }
    }

    public void Stop()
    {
        try { _app?.StopAsync().GetAwaiter().GetResult(); } catch { }
    }
}
