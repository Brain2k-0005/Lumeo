using System.Globalization;
using Lumeo;
using Lumeo.Tests.ServerHost.Components;

// Codex round 2, P2 #4/#8: GanttScale's month-name header labels (and Gantt3's
// pre-existing PeriodLabel) render via CultureInfo.CurrentCulture — v2 parity
// (v2's fmtMonth/fmtMonthShort follow the BROWSER's locale, not hardcoded
// English), but a Blazor Server circuit's ambient culture otherwise falls back
// to whatever the HOST PROCESS's OS default locale happens to be. Pinning the
// default thread culture here (BEFORE the host builds/runs, so every circuit —
// which each get their own thread from ASP.NET Core's thread pool — inherits
// it) makes the Gantt E2E/visual specs' English month-name assertions
// deterministic regardless of the dev machine's or CI runner's actual OS
// locale, rather than accidentally passing only when that locale happens to
// be English. Not a RequestLocalization pipeline (no per-request culture
// negotiation needed here) — this harness has exactly one audience (the E2E
// suite), not real end users choosing a locale.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");

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
