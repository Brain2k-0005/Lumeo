using Bunit;
using Lumeo;
using Lumeo.Docs.Services;
using Lumeo.Docs.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Lumeo.Docs.Tests.Catalog;

// Mounts every discovered "*Showcase" component standalone (the same way CatalogCard
// mounts one via <DynamicComponent>) and asserts none of them throws. This is the gate
// later waves rely on: add a new <Name>Showcase.razor under Shared/Showcases and it is
// automatically covered here via ShowcaseResolver.All() — no test file to edit.
public class AllShowcasesRenderTests
{
    private readonly ITestOutputHelper _out;
    public AllShowcasesRenderTests(ITestOutputHelper o) => _out = o;

    [Fact]
    public async Task Every_showcase_renders_standalone_without_throwing()
    {
        var showcaseTypes = ShowcaseResolver.All();
        Assert.NotEmpty(showcaseTypes);

        var failures = new List<string>();
        foreach (var showcaseType in showcaseTypes)
        {
            try
            {
                await using var ctx = new BunitContext();
                ctx.JSInterop.Mode = JSRuntimeMode.Loose;
                ctx.Services.AddLumeo();
                ctx.AddDocsServices();

                ctx.Render(b => { b.OpenComponent(0, showcaseType); b.CloseComponent(); });
            }
            catch (Exception ex)
            {
                var root = ex;
                while (root.InnerException is not null) root = root.InnerException;
                failures.Add($"{showcaseType.Name}: {root.GetType().Name}: {root.Message}");
            }
        }

        _out.WriteLine($"Rendered {showcaseTypes.Count} showcases; {failures.Count} threw.");
        foreach (var f in failures) _out.WriteLine("  FAIL " + f);

        Assert.True(failures.Count == 0,
            $"{failures.Count}/{showcaseTypes.Count} showcases threw on render:\n" + string.Join("\n", failures));
    }
}
