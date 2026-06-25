using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InplaceEditor;

/// <summary>
/// Battle-test n=36 (keyboard-a11y) — the idle InplaceEditor display trigger is a
/// <c>div[role=button]</c>, which has no native key synthesis: pressing Space both
/// entered edit mode (via <c>HandleDisplayKeyDown</c>) AND fired the browser's default
/// Space action, scroll-jumping the page. The fix suppresses Space's default action
/// through the library's <c>RegisterPreventDefaultKeys</c> interop — the same idiom as
/// CollapsibleTrigger / DialogTrigger — which requires the trigger to carry a stable
/// <c>id</c> so the JS keydown handler can bind to it.
///
/// bUnit cannot observe a JS-level <c>preventDefault</c> nor real focus/scroll, so this
/// test asserts the OBSERVABLE precondition the fix introduces: the display trigger now
/// renders a non-empty <c>id</c> attribute (it had NONE before), which is the element the
/// key-suppression registration targets. It also pins the existing behaviour (Space still
/// enters edit mode) and that the id survives an edit round-trip so the re-registration
/// after returning to display mode has a stable target.
/// </summary>
public class InplaceEditorSpacePreventDefaultTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InplaceEditorSpacePreventDefaultTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        var module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        module.Mode = JSRuntimeMode.Loose;
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ---- the fix: the role=button display trigger carries a stable id (Space-suppression target) ----

    [Fact]
    public void Display_Trigger_Has_Stable_Id()
    {
        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(e => e.Value, "hello"));

        var trigger = cut.Find("[role='button']");
        var id = trigger.GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(id),
            "InplaceEditor's display trigger must expose an id so Space preventDefault can be registered against it.");
    }

    // ---- existing behaviour preserved: Space still enters edit mode ----

    [Fact]
    public void Space_On_Display_Still_Enters_Edit_Mode()
    {
        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(e => e.Value, "hello"));

        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = " " });

        Assert.Single(cut.FindAll("input"));
    }

    // ---- the id is stable across an edit round-trip (the re-registration target) ----

    [Fact]
    public void Display_Trigger_Id_Survives_Edit_Round_Trip()
    {
        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(e => e.Value, "hello"));

        var idBefore = cut.Find("[role='button']").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(idBefore));

        // Enter edit mode (display trigger is torn down) then cancel back to display.
        cut.Find("[role='button']").KeyDown(new KeyboardEventArgs { Key = " " });
        Assert.Single(cut.FindAll("input"));
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        // Back in display mode with the SAME stable id, so the re-registered
        // Space-suppression handler binds to the freshly created node.
        var idAfter = cut.Find("[role='button']").GetAttribute("id");
        Assert.Equal(idBefore, idAfter);
    }
}
