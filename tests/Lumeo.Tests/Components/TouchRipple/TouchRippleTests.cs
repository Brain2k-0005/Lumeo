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

    // #67 — a negative DurationMs was emitted verbatim into the --ripple-duration
    // CSS var. Math.Max(0, DurationMs) now clamps it so the timing var is never
    // negative (paired with the Task.Delay clamp below).
    [Fact]
    public void Negative_DurationMs_Clamps_Css_Duration_Var_To_Zero()
    {
        var cut = _ctx.Render<Lumeo.TouchRipple>(p => p.Add(x => x.DurationMs, -100));

        var style = cut.Find("div").GetAttribute("style") ?? string.Empty;
        Assert.Contains("--ripple-duration: 0ms", style);
        Assert.DoesNotContain("-100ms", style);
    }

    // #67 — RemoveAfterDelayAsync called Task.Delay(DurationMs), which throws
    // ArgumentOutOfRangeException for any negative value other than -1. The
    // throw escaped the fire-and-forget continuation (the catch only handles
    // TaskCanceledException), so the cleanup never ran and the ripple entry
    // leaked permanently. With the Math.Max(0, DurationMs) clamp the delay
    // settles immediately and the span is removed.
    [Fact]
    public async Task Negative_DurationMs_Removes_Ripple_Without_Leaking()
    {
        var cut = _ctx.Render<Lumeo.TouchRipple>(p => p.Add(x => x.DurationMs, -100));

        await cut.Find("div").PointerDownAsync(new PointerEventArgs { ClientX = 5, ClientY = 5 });

        // Without the clamp, Task.Delay(-100) throws inside the fire-and-forget
        // cleanup, the entry is never removed, and the span leaks forever — so
        // this eventual-empty assertion times out. With the clamp the delay
        // settles and the span is removed.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("span.lumeo-touch-ripple-span")));
    }
}
