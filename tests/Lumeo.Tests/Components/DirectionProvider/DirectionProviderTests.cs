using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using L = Lumeo;

namespace Lumeo.Tests.Components.DirectionProvider;

/// <summary>
/// DirectionProvider sets the native dir attribute so the browser resolves Lumeo's
/// logical CSS utilities (ms/me/ps/pe/start/end/…) against that direction — an Rtl
/// provider mirrors descendant layout. Default is Ltr.
/// </summary>
public class DirectionProviderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public DirectionProviderTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Rtl_Sets_Dir_Rtl_On_The_Wrapper()
    {
        var cut = _ctx.Render<L.DirectionProvider>(p => p
            .Add(d => d.Direction, LayoutDirection.Rtl)
            .AddChildContent("<span>hi</span>"));

        Assert.Equal("rtl", cut.Find("div").GetAttribute("dir"));
        Assert.Contains("hi", cut.Markup);
    }

    [Fact]
    public void Default_Is_Ltr()
    {
        var cut = _ctx.Render<L.DirectionProvider>(p => p.AddChildContent("<span>hi</span>"));
        Assert.Equal("ltr", cut.Find("div").GetAttribute("dir"));
    }

    [Fact]
    public void Consumer_Class_And_Attributes_Are_Forwarded_To_The_Wrapper()
    {
        var cut = _ctx.Render<L.DirectionProvider>(p => p
            .Add(d => d.Direction, LayoutDirection.Rtl)
            .Add(d => d.Class, "min-h-screen")
            .AddChildContent("x"));

        var div = cut.Find("div");
        Assert.Contains("min-h-screen", div.GetAttribute("class") ?? "");
        Assert.Equal("rtl", div.GetAttribute("dir"));
    }
}
