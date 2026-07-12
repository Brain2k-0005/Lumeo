using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Dock;

/// <summary>
/// PR #356 round-5 (Codex P2) — a consumer-splatted <c>id</c> in
/// AdditionalAttributes renders AFTER the wrapper's explicit
/// <c>id="@_elementId"</c> and wins in the DOM, but every keyboard/magnify
/// interop call (MotionDock, InitToolbarRoving, RegisterPreventDefaultKeys,
/// MoveToolbarFocus, FocusToolbarEdge) used to always target the raw internal
/// <c>_elementId</c> — leaving them pointed at an id no element in the
/// document carries, so the Dock advertised <c>role="toolbar"</c> and
/// intercepted Arrow/Home/End while the actual focus movement and
/// prevent-default registration silently no-opped. Fixed via
/// EffectiveElementId (mirrors PopoverTrigger's EffectiveTriggerId /
/// PivotGrid's EffectiveWrapperId).
/// </summary>
public class DockEffectiveIdTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly BunitJSModuleInterop _motionModule;

    public DockEffectiveIdTests()
    {
        _ctx.AddLumeoServices();
        _motionModule = _ctx.JSInterop.SetupModule("./_content/Lumeo.Motion/js/motion.js");
        _motionModule.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string ChildButtons = "<button>App One</button><button>App Two</button>";

    private IRenderedComponent<L.Dock> RenderDock(string? consumerId = null) =>
        consumerId is null
            ? _ctx.Render<L.Dock>(p => p.AddChildContent(ChildButtons))
            : _ctx.Render<L.Dock>(p => p
                .AddChildContent(ChildButtons)
                .Add(d => d.AdditionalAttributes, new Dictionary<string, object> { ["id"] = consumerId }));

    private System.Collections.Generic.IEnumerable<string> ComponentsJsCallsFor(string identifier) =>
        _ctx.JSInterop.Invocations
            .Where(i => i.Identifier == identifier)
            .Select(i => (string)i.Arguments[0]!);

    [Fact]
    public void Consumer_Splatted_Id_Wins_In_The_Dom()
    {
        var cut = RenderDock(consumerId: "my-dock");
        Assert.Equal("my-dock", cut.Find("[role='toolbar']").GetAttribute("id"));
    }

    [Fact]
    public void MotionDock_Registers_Against_The_Consumer_Splatted_Id()
    {
        var cut = RenderDock(consumerId: "my-dock");
        var invoke = _motionModule.VerifyInvoke("motion.dock");
        Assert.Equal("my-dock", invoke.Arguments[0]);
    }

    [Fact]
    public void InitToolbarRoving_Targets_The_Consumer_Splatted_Id()
    {
        var cut = RenderDock(consumerId: "my-dock");
        cut.WaitForAssertion(() => Assert.Contains("my-dock", ComponentsJsCallsFor("initToolbarRoving")));
    }

    [Fact]
    public void RegisterPreventDefaultKeys_Targets_The_Consumer_Splatted_Id()
    {
        var cut = RenderDock(consumerId: "my-dock");
        cut.WaitForAssertion(() => Assert.Contains("my-dock", ComponentsJsCallsFor("registerPreventDefaultKeys")));
    }

    [Fact]
    public void ArrowKey_MoveToolbarFocus_Targets_The_Consumer_Splatted_Id()
    {
        var cut = RenderDock(consumerId: "my-dock");
        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        var invoke = _ctx.JSInterop.Invocations.Last(i => i.Identifier == "moveToolbarFocus");
        Assert.Equal("my-dock", invoke.Arguments[0]);
    }

    [Fact]
    public void Consumer_Bound_Id_Changing_At_Runtime_Re_Registers_Motion_Dock_Against_The_New_Id()
    {
        var cut = RenderDock(consumerId: "dock-before");
        cut.WaitForAssertion(() => Assert.Contains("dock-before", ComponentsJsCallsFor("initToolbarRoving")));

        cut.Render(p => p
            .AddChildContent(ChildButtons)
            .Add(d => d.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "dock-after" }));

        cut.WaitForAssertion(() =>
        {
            // Re-registered under the NEW id...
            Assert.Contains(
                _motionModule.Invocations.Where(i => i.Identifier == "motion.dock"),
                i => (string)i.Arguments[0]! == "dock-after");
            // ...and the OLD registration was torn down first (by the id it was
            // actually registered under), so the magnify listeners never
            // accumulate a stale second set.
            Assert.Contains(
                _motionModule.Invocations.Where(i => i.Identifier == "motion.disposeDock"),
                i => (string)i.Arguments[0]! == "dock-before");
        });
    }

    [Fact]
    public void Consumer_Bound_Id_Changing_At_Runtime_Re_Registers_Prevent_Default_Keys_Against_The_New_Id()
    {
        var cut = RenderDock(consumerId: "dock-before");
        cut.WaitForAssertion(() => Assert.Contains("dock-before", ComponentsJsCallsFor("registerPreventDefaultKeys")));

        cut.Render(p => p
            .AddChildContent(ChildButtons)
            .Add(d => d.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "dock-after" }));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("dock-after", ComponentsJsCallsFor("registerPreventDefaultKeys"));
            Assert.Contains("dock-before", ComponentsJsCallsFor("unregisterPreventDefaultKeys"));
        });
    }

    [Fact]
    public void Without_A_Consumer_Id_Every_Interop_Call_Targets_The_Rendered_Fallback_Id()
    {
        var cut = RenderDock();
        var renderedId = cut.Find("[role='toolbar']").GetAttribute("id")!;
        Assert.False(string.IsNullOrEmpty(renderedId));

        var invoke = _motionModule.VerifyInvoke("motion.dock");
        Assert.Equal(renderedId, invoke.Arguments[0]);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(renderedId, ComponentsJsCallsFor("initToolbarRoving"));
            Assert.Contains(renderedId, ComponentsJsCallsFor("registerPreventDefaultKeys"));
        });
    }
}
