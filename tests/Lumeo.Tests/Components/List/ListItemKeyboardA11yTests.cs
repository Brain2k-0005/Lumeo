using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.List;

/// <summary>
/// Battle-test wave 1 — List / keyboard-a11y regressions (n=15, n=16, n=17).
///
/// n=15 (Space-scroll): a clickable, non-href ListItem renders as
/// <c>&lt;li role="button"&gt;</c>, which has no native key synthesis — pressing Space
/// both activated the item AND fired the browser's default Space action,
/// scroll-jumping the page. The fix suppresses Space's default through the
/// library's <c>RegisterPreventDefaultKeys</c> interop (same idiom as
/// InplaceEditor / DialogTrigger), which requires the role=button element to carry
/// a stable <c>id</c>. bUnit cannot observe a JS-level preventDefault nor real
/// scroll, so the OBSERVABLE precondition the fix introduces is asserted: the
/// role=button now renders a non-empty id (it had NONE before), and Space still
/// activates the item.
///
/// n=16 (aria-disabled, non-href): a disabled clickable ListItem was only
/// visually/pointer-disabled — it carried no <c>aria-disabled</c>, so assistive
/// tech announced it as an enabled button. The fix emits
/// <c>aria-disabled="true"</c> on the role=button li when Disabled.
///
/// n=17 (disabled href stayed keyboard-navigable): a disabled ListItem with an
/// Href kept a live, focusable anchor (Enter still navigated) — Disabled was only
/// honoured for the pointer via pointer-events-none. The fix drops the navigable
/// href, sets <c>tabindex="-1"</c> and <c>aria-disabled="true"</c> on the anchor so
/// keyboard + AT honour Disabled too.
/// </summary>
public class ListItemKeyboardA11yTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ListItemKeyboardA11yTests()
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

    // ---- n=15: the role=button trigger carries a stable id (Space-suppression target) ----

    [Fact]
    public void Clickable_Item_Has_Stable_Id_For_Space_Suppression()
    {
        var cut = _ctx.Render<L.ListItem>(p => p
            .Add(i => i.Title, "Item")
            .Add(i => i.OnClick, () => { }));

        var li = cut.Find("li[role='button']");
        var id = li.GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(id),
            "A clickable ListItem must expose an id so Space preventDefault can be registered against it.");
    }

    [Fact]
    public void Non_Clickable_Item_Has_No_Button_Id()
    {
        // A plain (non-clickable, non-href) ListItem is not a role=button, so it
        // must not leak the preventDefault-target id.
        var cut = _ctx.Render<L.ListItem>(p => p
            .Add(i => i.Title, "Item"));

        Assert.Null(cut.Find("li").GetAttribute("id"));
    }

    [Fact]
    public void Space_On_Clickable_Item_Still_Activates()
    {
        // Existing behaviour preserved: Space still fires OnClick (the
        // preventDefault only suppresses the page-scroll default, not activation).
        var clicked = false;
        var cut = _ctx.Render<L.ListItem>(p => p
            .Add(i => i.Title, "Item")
            .Add(i => i.OnClick, () => clicked = true));

        cut.Find("li[role='button']").KeyDown(new KeyboardEventArgs { Key = " " });

        Assert.True(clicked);
    }

    // ---- n=16: a disabled clickable (non-href) item announces aria-disabled ----

    [Fact]
    public void Disabled_Clickable_Item_Emits_Aria_Disabled()
    {
        var cut = _ctx.Render<L.ListItem>(p => p
            .Add(i => i.Title, "Item")
            .Add(i => i.Disabled, true)
            .Add(i => i.OnClick, () => { }));

        // The role=button li must announce aria-disabled=true to assistive tech,
        // not rely solely on the visual pointer-events-none class.
        Assert.Equal("true", cut.Find("li[role='button']").GetAttribute("aria-disabled"));
    }

    [Fact]
    public void Enabled_Clickable_Item_Has_No_Aria_Disabled()
    {
        var cut = _ctx.Render<L.ListItem>(p => p
            .Add(i => i.Title, "Item")
            .Add(i => i.OnClick, () => { }));

        Assert.Null(cut.Find("li[role='button']").GetAttribute("aria-disabled"));
    }

    // ---- n=17: a disabled href item is removed from the keyboard tab order + AT ----

    [Fact]
    public void Disabled_Href_Item_Drops_Navigable_Href_And_Honours_Disabled()
    {
        var cut = _ctx.Render<L.ListItem>(p => p
            .Add(i => i.Href, "/details")
            .Add(i => i.Title, "Item")
            .Add(i => i.Disabled, true));

        var anchor = cut.Find("a");
        // No navigable href while disabled — Enter can no longer navigate.
        Assert.True(string.IsNullOrEmpty(anchor.GetAttribute("href")));
        // Pulled out of the tab order for keyboard users.
        Assert.Equal("-1", anchor.GetAttribute("tabindex"));
        // Announced as disabled to assistive tech.
        Assert.Equal("true", anchor.GetAttribute("aria-disabled"));
    }

    [Fact]
    public void Enabled_Href_Item_Keeps_Live_Href_And_No_Disabled_Markup()
    {
        // The normal (enabled) href path is unchanged: real href, in the tab order,
        // no aria-disabled.
        var cut = _ctx.Render<L.ListItem>(p => p
            .Add(i => i.Href, "/details")
            .Add(i => i.Title, "Item"));

        var anchor = cut.Find("a");
        Assert.Equal("/details", anchor.GetAttribute("href"));
        Assert.Null(anchor.GetAttribute("tabindex"));
        Assert.Null(anchor.GetAttribute("aria-disabled"));
    }
}
