using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DataGridServerModeDemo.Client;
using Lumeo;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Points at the separate minimal-API server project (Server/Program.cs), not this
// client's own origin — a real cross-origin HTTP round trip, not an in-process fake.
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("http://localhost:5280") });
builder.Services.AddLumeo();

await builder.Build().RunAsync();
