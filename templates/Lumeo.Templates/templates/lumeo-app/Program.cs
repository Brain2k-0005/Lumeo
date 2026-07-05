using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
#if (AuthEnabled)
using Microsoft.AspNetCore.Components.Authorization;
#endif
#if (AuthOidc)
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
#endif
using Lumeo;
using MyApp;
#if (AuthDemo)
using MyApp.Auth;
#endif

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

#if (AuthDemo)
// Demo authentication — an AuthenticationStateProvider backed entirely by the browser's
// localStorage. There is NO backend: any email + any password >= 6 chars signs in.
//
// THIS IS THE SEAM. Swap DemoAuthenticationStateProvider for a real provider (OIDC via the
// --auth oidc option, an ASP.NET Identity API, Auth0/Entra, …) and the rest of the app —
// <AuthorizeView>, [Authorize], <RedirectToLogin> — keeps working unchanged. See the README
// section "Demo auth — swap the provider".
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<DemoAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<DemoAuthenticationStateProvider>());
#endif
#if (AuthOidc)
// Real OIDC/OAuth. Bind your authority + client id from wwwroot/appsettings.json ("Oidc").
// AddOidcAuthentication registers the AuthenticationStateProvider and authorization core;
// sign-in/out + the callback are handled by <RemoteAuthenticatorView> in Pages/Auth/Authentication.razor.
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Oidc", options.ProviderOptions);
});
#endif

// Registers every Lumeo service (theming, toasts, overlays, keyboard shortcuts, …).
// Required — components such as <ThemeToggle>, <Tooltip> and <Form> resolve services
// from DI at runtime.
builder.Services.AddLumeo();

await builder.Build().RunAsync();
