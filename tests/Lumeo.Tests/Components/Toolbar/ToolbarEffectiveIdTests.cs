using System.Linq;
using System.Reflection;
using Bunit;
using Lumeo.Services;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Toolbar;

/// <summary>
/// PR #356 round-6 (Codex P2) — for <c>&lt;Toolbar id="actions"&gt;</c>, the
/// consumer-splatted <c>id</c> in AdditionalAttributes renders AFTER the
/// wrapper's explicit <c>id="@_toolbarId"</c> and wins in the DOM, but every
/// keyboard interop call (InitToolbarRoving, RegisterPreventDefaultKeys,
/// MoveToolbarFocus, FocusToolbarEdge, RegisterToolbarOverflow) used to
/// always target the raw internal <c>_toolbarId</c> — leaving them pointed
/// at an id no element in the document carries, so Arrow/Home/End silently
/// fell through to the browser default for any custom-id toolbar. Fixed via
/// EffectiveToolbarId (mirrors Dock's EffectiveElementId / PopoverTrigger's
/// EffectiveTriggerId).
/// </summary>
public class ToolbarEffectiveIdTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public ToolbarEffectiveIdTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        _module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        _module.Mode = JSRuntimeMode.Loose;
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string ChildButtons = "<button>A</button><button>B</button>";

    private IRenderedComponent<L.Toolbar> RenderToolbar(string? consumerId = null) =>
        consumerId is null
            ? _ctx.Render<L.Toolbar>(p => p.AddChildContent(ChildButtons))
            : _ctx.Render<L.Toolbar>(p => p
                .AddChildContent(ChildButtons)
                .Add(t => t.AdditionalAttributes, new Dictionary<string, object> { ["id"] = consumerId }));

    private IEnumerable<string> ComponentsJsCallsFor(string identifier) =>
        _ctx.JSInterop.Invocations
            .Where(i => i.Identifier == identifier)
            .Select(i => (string)i.Arguments[0]!);

    [Fact]
    public void Consumer_Splatted_Id_Wins_In_The_Dom()
    {
        var cut = RenderToolbar(consumerId: "actions");
        Assert.Equal("actions", cut.Find("[role='toolbar']").GetAttribute("id"));
    }

    [Fact]
    public void InitToolbarRoving_Targets_The_Consumer_Splatted_Id()
    {
        var cut = RenderToolbar(consumerId: "actions");
        cut.WaitForAssertion(() => Assert.Contains("actions", ComponentsJsCallsFor("initToolbarRoving")));
    }

    [Fact]
    public void RegisterPreventDefaultKeys_Targets_The_Consumer_Splatted_Id()
    {
        var cut = RenderToolbar(consumerId: "actions");
        cut.WaitForAssertion(() => Assert.Contains("actions", ComponentsJsCallsFor("registerPreventDefaultKeys")));
    }

    [Fact]
    public void RegisterPreventDefaultKeys_Never_Targets_The_Raw_Internal_Fallback_Id_When_A_Consumer_Id_Is_Set()
    {
        var cut = RenderToolbar(consumerId: "actions");
        cut.WaitForAssertion(() => Assert.NotEmpty(ComponentsJsCallsFor("registerPreventDefaultKeys")));

        Assert.DoesNotContain(
            ComponentsJsCallsFor("registerPreventDefaultKeys"),
            id => id.StartsWith("lumeo-toolbar-", StringComparison.Ordinal));
    }

    [Fact]
    public void ArrowKey_MoveToolbarFocus_Targets_The_Consumer_Splatted_Id()
    {
        var cut = RenderToolbar(consumerId: "actions");
        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        var invoke = _ctx.JSInterop.Invocations.Last(i => i.Identifier == "moveToolbarFocus");
        Assert.Equal("actions", invoke.Arguments[0]);
    }

    [Fact]
    public void HomeKey_FocusToolbarEdge_Targets_The_Consumer_Splatted_Id()
    {
        var cut = RenderToolbar(consumerId: "actions");
        cut.Find("[role='toolbar']").KeyDown(new KeyboardEventArgs { Key = "Home" });

        var invoke = _ctx.JSInterop.Invocations.Last(i => i.Identifier == "focusToolbarEdge");
        Assert.Equal("actions", invoke.Arguments[0]);
    }

    [Fact]
    public void Consumer_Bound_Id_Changing_At_Runtime_Re_Registers_Prevent_Default_Keys_Against_The_New_Id()
    {
        var cut = RenderToolbar(consumerId: "actions-before");
        cut.WaitForAssertion(() => Assert.Contains("actions-before", ComponentsJsCallsFor("registerPreventDefaultKeys")));

        cut.Render(p => p
            .AddChildContent(ChildButtons)
            .Add(t => t.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "actions-after" }));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("actions-after", ComponentsJsCallsFor("registerPreventDefaultKeys"));
            Assert.Contains("actions-before", ComponentsJsCallsFor("unregisterPreventDefaultKeys"));
        });
    }

    [Fact]
    public void Without_A_Consumer_Id_Every_Interop_Call_Targets_The_Rendered_Fallback_Id()
    {
        var cut = RenderToolbar();
        var renderedId = cut.Find("[role='toolbar']").GetAttribute("id")!;
        Assert.False(string.IsNullOrEmpty(renderedId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(renderedId, ComponentsJsCallsFor("initToolbarRoving"));
            Assert.Contains(renderedId, ComponentsJsCallsFor("registerPreventDefaultKeys"));
        });
    }
}
