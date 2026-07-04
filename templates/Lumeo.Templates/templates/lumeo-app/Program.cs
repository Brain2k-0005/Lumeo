using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Lumeo;
using MyApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Registers every Lumeo service (theming, toasts, overlays, keyboard shortcuts, …).
// Required — components such as <ThemeToggle>, <Tooltip> and <Form> resolve services
// from DI at runtime.
builder.Services.AddLumeo();

await builder.Build().RunAsync();
