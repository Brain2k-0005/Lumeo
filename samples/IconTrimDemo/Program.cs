using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using IconTrimDemo;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Deliberately minimal: no AddLumeo(). SvgGlyph needs no DI, and rooting extra Lumeo
// services would only muddy the Tabler-assembly trim measurement this sample exists to make.
await builder.Build().RunAsync();
