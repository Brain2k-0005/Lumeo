using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Map;

public class MapTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public MapTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_An_Application_Landmark_Container()
    {
        var cut = _ctx.Render<L.Map>();
        var container = cut.Find("[role='application']");
        Assert.Equal("Map", container.GetAttribute("aria-label"));
    }

    [Fact]
    public void Height_Is_Applied_To_The_Container()
    {
        var cut = _ctx.Render<L.Map>(p => p.Add(m => m.Height, "300px"));
        Assert.Contains("300px", cut.Find("[role='application']").GetAttribute("style") ?? "");
    }
}
