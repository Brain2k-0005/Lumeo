using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Lumeo;
using Lumeo.Docs;
using Lumeo.Docs.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddLumeo();
builder.Services.AddSingleton<IconService>();
builder.Services.AddSingleton<PatternFilterService>();

await builder.Build().RunAsync();
