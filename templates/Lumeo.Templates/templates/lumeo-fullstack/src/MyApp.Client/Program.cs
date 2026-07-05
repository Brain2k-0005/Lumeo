using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Lumeo;
using MyApp;
using MyApp.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API base address. In production (Docker) "ApiBaseUrl" is empty, so the client is
// same-origin and nginx proxies /api to the API container — no CORS. In hybrid dev,
// appsettings.Development.json points it at the locally-run API (with CORS enabled).
var apiBase = builder.Configuration["ApiBaseUrl"];
var baseAddress = string.IsNullOrWhiteSpace(apiBase) ? builder.HostEnvironment.BaseAddress : apiBase;
if (!baseAddress.EndsWith('/')) baseAddress += "/";
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(baseAddress) });

// One AuthenticationStateProvider, backed by the ASP.NET Core Identity API. It owns the
// bearer token and is the seam every auth primitive (<AuthorizeView>, [Authorize],
// <RedirectToLogin>) hangs off — swap it for OIDC/Auth0/Entra without touching the UI.
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<ApiAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ApiAuthenticationStateProvider>());

// Registers every Lumeo service (theming, toasts, overlays, keyboard shortcuts, …).
builder.Services.AddLumeo();

await builder.Build().RunAsync();
