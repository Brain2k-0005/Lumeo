using Lumeo;
using Lumeo.Tests.ServerHost.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLumeo();

// ---------------------------------------------------------------------
// Artificial circuit latency — TWO mechanisms are available; the harness
// (scripts/server-leg/run.mjs) uses CDP network throttling (see that file
// for why), but a server-side delay middleware is wired here too, opt-in via
// LUMEO_SERVERLEG_DELAY_MS, for anyone who wants to reproduce a scenario
// without a Chromium/CDP dependency (e.g. driving this host with curl, or a
// non-Chromium engine). Delaying every HTTP request (including the SignalR
// negotiate handshake and the long-poll/WebSocket upgrade) approximates RTT
// on the circuit; it does NOT delay individual SignalR frames the way CDP
// throttling does, which is why CDP is the harness's actual mechanism — see
// scripts/server-leg/README.md for the full comparison.
// ---------------------------------------------------------------------
var delayMs = Environment.GetEnvironmentVariable("LUMEO_SERVERLEG_DELAY_MS");
if (int.TryParse(delayMs, out var ms) && ms > 0)
{
    builder.Logging.AddConsole();
}

var app = builder.Build();

if (int.TryParse(delayMs, out var delay) && delay > 0)
{
    app.Logger.LogInformation("Server-side delay middleware active: {DelayMs}ms per request", delay);
    app.Use(async (context, next) =>
    {
        await Task.Delay(delay);
        await next();
    });
}

// MapStaticAssets (not the older UseStaticFiles alone) is what actually
// serves Lumeo's _content/Lumeo/... referenced-RCL static web assets
// (css/js) outside Development — UseStaticFiles alone 404s them once the
// environment isn't Development, which this host runs under by default.
app.MapStaticAssets();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
