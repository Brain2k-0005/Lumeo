using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CoreTrimDemo;
using Lumeo;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Button (like most core components) [Inject]s IComponentInteropService, so AddLumeo() is
// required for the app to actually render — omitting it isn't a smaller reference app, it's
// a broken one. The trim measurement this sample exists for is unaffected: AddLumeo()
// registers services, not component TYPES, and unreferenced service implementations are
// still trimmed away when unreachable.
builder.Services.AddLumeo();

await builder.Build().RunAsync();
