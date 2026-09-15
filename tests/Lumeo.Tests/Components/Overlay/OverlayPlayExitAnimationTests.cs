using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Overlay;

/// <summary>
/// Field report #464, finding 4 — <c>PlayExitAnimation</c> already existed on
/// <c>DialogContent</c> / <c>DrawerContent</c> / <c>AlertDialogContent</c> (and,
/// after this fix, <c>SheetContent</c>) but not on <see cref="OverlayOptions"/>:
/// a service-opened overlay (<see cref="OverlayService.ShowDialogAsync{T}"/> /
/// <c>ShowSheetAsync</c> / <c>ShowDrawerAsync</c>) had no lever to opt OUT of
/// the exit animation. <see cref="OverlayOptions.PlayExitAnimation"/> now
/// threads through <see cref="Lumeo.OverlayProvider"/> to the same parameter
/// the declarative components already read.
/// </summary>
public class OverlayPlayExitAnimationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly OverlayService _overlay = new();
    private readonly TrackingInteropService _interop = new();

    public OverlayPlayExitAnimationTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<OverlayService>(_ => _overlay);
        _ctx.Services.AddScoped<IOverlayService>(_ => _overlay);
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class Body : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "BODY");
    }

    private string Open(string type, bool playExitAnimation)
    {
        OverlayInstance? shown = null;
        _overlay.OnShow += i => shown = i;
        var options = new OverlayOptions { PlayExitAnimation = playExitAnimation };
        _ = type switch
        {
            "Sheet" => _overlay.ShowSheetAsync<Body>(title: "S", side: Lumeo.Side.Right, options: options),
            "Dialog" => _overlay.ShowDialogAsync<Body>(title: "D", options: options),
            "Drawer" => _overlay.ShowDrawerAsync<Body>(title: "Dr", options: options),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        return shown!.Id;
    }

    private static string Role(string type) => type switch
    {
        "Sheet" or "Dialog" or "Drawer" => "dialog",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    [Theory]
    [InlineData("Sheet")]
    [InlineData("Dialog")]
    [InlineData("Drawer")]
    public async Task PlayExitAnimation_False_Removes_The_Panel_Immediately(string type)
    {
        var role = Role(type);
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        var id = Open(type, playExitAnimation: false);
        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        await cut.InvokeAsync(() => _overlay.Cancel(id));

        // No exit window at all: the panel is gone on the very next render commit
        // (synchronous within the Cancel dispatch), no animate-*-out class, no
        // animationend wiring — unlike the PlayExitAnimation=true family covered
        // by OverlayExitAnimationEndTests.
        Assert.Empty(cut.FindAll($"[role='{role}']"));
        Assert.Empty(_interop.OverlayExitEndWirings);
    }

    [Theory]
    [InlineData("Sheet")]
    [InlineData("Dialog")]
    [InlineData("Drawer")]
    public async Task PlayExitAnimation_True_Still_Plays_The_Exit_Animation(string type)
    {
        var role = Role(type);
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        var id = Open(type, playExitAnimation: true);
        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        await cut.InvokeAsync(() => _overlay.Cancel(id));

        // Default/true behaviour is unchanged: the panel stays mounted through
        // the exit window (regression guard against PlayExitAnimation flipping
        // the DEFAULT rather than just adding an opt-out).
        Assert.NotEmpty(cut.FindAll($"[role='{role}']"));
        Assert.Contains("BODY", cut.Markup);
    }
}
