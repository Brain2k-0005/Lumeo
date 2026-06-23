using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DensityScope;

public class DensityScopeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public DensityScopeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Child_Content()
    {
        var cut = _ctx.Render<L.DensityScope>(p => p.AddChildContent("inside"));
        Assert.Contains("inside", cut.Markup);
    }

    [Fact]
    public void Cascades_Its_Density_To_Descendants()
    {
        // A density-aware descendant (Chip) must render differently inside a Compact
        // scope than inside a Comfortable scope — proving the cascading value flows.
        var compact = _ctx.Render<L.DensityScope>(p => p
            .Add(d => d.Value, L.Density.Compact)
            .AddChildContent<L.Chip>(c => c.AddChildContent("X")));
        var comfy = _ctx.Render<L.DensityScope>(p => p
            .Add(d => d.Value, L.Density.Comfortable)
            .AddChildContent<L.Chip>(c => c.AddChildContent("X")));

        var compactClass = compact.Find("div").GetAttribute("class");
        var comfyClass = comfy.Find("div").GetAttribute("class");
        Assert.NotEqual(comfyClass, compactClass);
    }
}
