using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Lumeo.Tests.Components.TouchRipple;

/// <summary>
/// #310 — ripple appeared off-centre when the pointer landed on a nested child
/// (PointerEventArgs.OffsetX/OffsetY are relative to the child, not the ripple
/// host), and the animation ignored prefers-reduced-motion. The component now
/// resolves host-relative coordinates via interop and skips the ripple entirely
/// under reduced motion. bUnit can't run the CSS animation, so these tests
/// assert the C# contract via the tracking fake.
/// </summary>
public class TouchRippleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public TouchRippleTests()
    {
        _ctx.AddLumeoServices();
        // Last interface registration wins, so TouchRipple resolves the fake.
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Host_With_Ripple_Class()
    {
        var cut = _ctx.Render<Lumeo.TouchRipple>(p => p.AddChildContent("<span>x</span>"));

        Assert.Contains("lumeo-touch-ripple", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public async Task PointerDown_Uses_Host_Relative_Coords_From_Interop()
    {
        // The JS helper resolves coords relative to the host; the fake returns a
        // fixed point so we can assert the span is positioned with THAT point,
        // not the raw event offsets.
        _interop.TouchRippleCoordsResult = new RipplePoint(42, 17);

        var cut = _ctx.Render<Lumeo.TouchRipple>(p => p.AddChildContent("<span>x</span>"));

        await cut.Find("div").PointerDownAsync(new PointerEventArgs { OffsetX = 5, OffsetY = 9, ClientX = 100, ClientY = 200 });

        var span = cut.Find("span.lumeo-touch-ripple-span");
        var style = span.GetAttribute("style") ?? string.Empty;
        Assert.Contains("left: 42px", style);
        Assert.Contains("top: 17px", style);
    }

    [Fact]
    public async Task PointerDown_Passes_Client_Coords_And_Host_Id_To_Interop()
    {
        var cut = _ctx.Render<Lumeo.TouchRipple>();

        await cut.Find("div").PointerDownAsync(new PointerEventArgs { ClientX = 123, ClientY = 456 });

        var call = Assert.Single(_interop.TouchRippleCoordsCalls);
        Assert.Equal(123, call.X);
        Assert.Equal(456, call.Y);
        Assert.StartsWith("touch-ripple-", call.HostId);
    }

    [Fact]
    public async Task ReducedMotion_Spawns_No_Ripple()
    {
        _interop.ReducedMotion = true;

        var cut = _ctx.Render<Lumeo.TouchRipple>();

        await cut.Find("div").PointerDownAsync(new PointerEventArgs { ClientX = 10, ClientY = 10 });

        Assert.Empty(cut.FindAll("span.lumeo-touch-ripple-span"));
        // Should not even bother resolving coordinates when motion is suppressed.
        Assert.Empty(_interop.TouchRippleCoordsCalls);
    }

    [Fact]
    public async Task Motion_Allowed_Spawns_A_Ripple()
    {
        _interop.ReducedMotion = false;

        var cut = _ctx.Render<Lumeo.TouchRipple>();

        await cut.Find("div").PointerDownAsync(new PointerEventArgs { ClientX = 10, ClientY = 10 });

        Assert.Single(cut.FindAll("span.lumeo-touch-ripple-span"));
    }

    [Fact]
    public async Task Consumer_Id_Is_Used_As_Host_For_Coord_Lookup()
    {
        var cut = _ctx.Render<Lumeo.TouchRipple>(p => p
            .Add(x => x.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "my-ripple" }));

        await cut.Find("div").PointerDownAsync(new PointerEventArgs { ClientX = 1, ClientY = 1 });

        var call = Assert.Single(_interop.TouchRippleCoordsCalls);
        Assert.Equal("my-ripple", call.HostId);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.TouchRipple>(p => p.Add(x => x.Class, "tr-x"));

        Assert.Contains("tr-x", cut.Find("div").GetAttribute("class"));
    }
}
