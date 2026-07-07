using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tooltip;

public class TooltipTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    // GetModuleAsync() imports "components.js?v={version}" as a cache-buster, and that
    // version string includes a "+<commit-sha>" SemVer build-metadata suffix (not just
    // "4.0.3") — mirrors ComponentInteropService's private _jsModuleVersion computation
    // EXACTLY. A stub registered against a bare/guessed path (no suffix, or the wrong
    // suffix) silently never matches the real "import" call, so the interop method falls
    // back to Loose mode's type default instead of the stub — scoped to this test class
    // only, since correcting the version in the suite-wide AddLumeoServices() helper
    // itself surfaced unrelated latent stub-path mismatches in ~14 other test files
    // (each with its own local, differently-broken SetupModule call) — out of scope for
    // this bug fix.
    private static readonly string TestJsModuleVersion =
        typeof(Lumeo.Services.ComponentInteropService).Assembly
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? typeof(Lumeo.Services.ComponentInteropService).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public TooltipTests()
    {
        _ctx.AddLumeoServices();
        // Tooltip.HandleFocusIn gates on IsActiveElementFocusVisible (Codex P2: a mouse
        // click also fires focusin, but must NOT open the tooltip — only real
        // keyboard/programmatic focus should). Every pre-existing focusin test in this
        // file simulates genuine keyboard navigation, so default the check to true here;
        // Loose mode's bool default (false) would otherwise make the tooltip never open
        // on focusin. Individual tests below override this to exercise the click case.
        _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={TestJsModuleVersion}")
            .Setup<bool>("isActiveElementFocusVisible")
            .SetResult(true);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // 3.0 — Tooltip switched from always-in-DOM + CSS group-hover to mounted-on-open
    // via position: fixed + IComponentInteropService.PositionFixed for collision flip.
    // TooltipContent renders nothing until the trigger opens the tooltip.

    private IRenderedComponent<IComponent> RenderTooltip(L.Side side = L.Side.Top)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();

                b.OpenComponent<L.TooltipContent>(2);
                b.AddAttribute(3, "Side", side);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Tooltip text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static void OpenTooltip(IRenderedComponent<IComponent> cut)
    {
        cut.Find("div").TriggerEvent("onmouseenter", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
    }

    // --- Closed by default ---

    [Fact]
    public void TooltipContent_Not_Rendered_When_Closed()
    {
        var cut = RenderTooltip();
        Assert.Empty(cut.FindAll("[role='tooltip']"));
    }

    [Fact]
    public void TooltipTrigger_Shows_Text()
    {
        var cut = RenderTooltip();
        Assert.Contains("Hover me", cut.Markup);
    }

    // --- Opens on mouseenter ---

    [Fact]
    public void Tooltip_Mounts_Content_On_MouseEnter()
    {
        var cut = RenderTooltip();
        OpenTooltip(cut);
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));
        Assert.Contains("Tooltip text", cut.Markup);
    }

    [Fact]
    public void TooltipContent_Has_Fixed_Position_When_Open()
    {
        var cut = RenderTooltip();
        OpenTooltip(cut);
        var tooltip = cut.Find("[role='tooltip']");
        var cls = tooltip.GetAttribute("class") ?? "";
        Assert.Contains("fixed", cls);
    }

    [Fact]
    public void TooltipContent_Visible_When_Open()
    {
        var cut = RenderTooltip();
        OpenTooltip(cut);
        var tooltip = cut.Find("[role='tooltip']");
        var cls = tooltip.GetAttribute("class") ?? "";
        Assert.Contains("visible", cls);
        Assert.Contains("opacity-100", cls);
    }

    // --- Wrapper / Trigger ---

    [Fact]
    public void Tooltip_Wrapper_Has_Relative_Inline_Flex_Class()
    {
        var cut = RenderTooltip();
        var elements = cut.FindAll("[class]");
        Assert.Contains(elements, e => (e.GetAttribute("class") ?? "").Contains("relative inline-flex"));
    }

    [Fact]
    public void TooltipTrigger_Has_Inline_Flex_Class()
    {
        var cut = RenderTooltip();
        var elements = cut.FindAll("[class]");
        Assert.Contains(elements, e => (e.GetAttribute("class") ?? "").StartsWith("inline-flex"));
    }

    private IRenderedComponent<IComponent> RenderTooltipWithWrapperClass(string wrapperClass)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "Class", wrapperClass);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Tooltip_Wrapper_Forwards_Custom_Class()
    {
        // The wrapper has no Class param before 3.20 — full-width nav triggers had
        // to be widened with an app-side CSS attribute-selector hack. Now Class flows
        // onto the wrapper so consumers control its box width directly.
        var cut = RenderTooltipWithWrapperClass("w-full custom-x");
        var cls = cut.Find("div").GetAttribute("class") ?? "";
        Assert.Contains("relative", cls);
        Assert.Contains("inline-flex", cls);
        Assert.Contains("w-full", cls);
        Assert.Contains("custom-x", cls);
    }

    [Fact]
    public void Tooltip_Wrapper_Class_Wins_Conflicts_Via_CxMerge()
    {
        // A consumer display utility must OVERRIDE the base inline-flex via
        // tailwind-merge (Cx.Merge), not merely append — proving the merge is wired,
        // so consumers never need !important to beat the base class.
        var cut = RenderTooltipWithWrapperClass("block");
        var cls = cut.Find("div").GetAttribute("class") ?? "";
        Assert.Contains("relative", cls);
        Assert.Contains("block", cls);
        Assert.DoesNotContain("inline-flex", cls);
    }

    // --- Custom class forwarded ---

    [Fact]
    public void Custom_Class_Forwarded_On_TooltipContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();

                b.OpenComponent<L.TooltipContent>(2);
                b.AddAttribute(3, "Class", "my-tooltip-class");
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("div").TriggerEvent("onmouseenter", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        var tooltip = cut.Find("[role='tooltip']");
        Assert.Contains("my-tooltip-class", tooltip.GetAttribute("class"));
    }

    // --- AdditionalAttributes forwarded ---

    [Fact]
    public void Additional_Attributes_Forwarded_On_TooltipContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();

                b.OpenComponent<L.TooltipContent>(2);
                b.AddAttribute(3, "AdditionalAttributes", new Dictionary<string, object>
                {
                    ["data-testid"] = "my-tooltip"
                });
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("div").TriggerEvent("onmouseenter", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        var tooltip = cut.Find("[role='tooltip']");
        Assert.Equal("my-tooltip", tooltip.GetAttribute("data-testid"));
    }

    // --- #221 a11y: aria-describedby, open-on-focus, Escape ---

    [Fact]
    public void Wrapper_AriaDescribedBy_Points_At_Tooltip_Content_When_Open()
    {
        var cut = RenderTooltip();
        OpenTooltip(cut);

        var wrapper = cut.Find("div");
        var describedBy = wrapper.GetAttribute("aria-describedby");
        Assert.False(string.IsNullOrEmpty(describedBy));
        // The role=tooltip content must carry that id.
        var tooltip = cut.Find("[role='tooltip']");
        Assert.Equal(describedBy, tooltip.GetAttribute("id"));
    }

    [Fact]
    public void Wrapper_Has_No_AriaDescribedBy_When_Closed()
    {
        var cut = RenderTooltip();
        var wrapper = cut.Find("div");
        Assert.True(string.IsNullOrEmpty(wrapper.GetAttribute("aria-describedby")));
    }

    [Fact]
    public async Task Tooltip_Opens_On_Focus()
    {
        var cut = RenderTooltip();
        await cut.Find("div").TriggerEventAsync("onfocusin", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));
    }

    [Fact]
    public async Task Tooltip_Closes_On_Blur()
    {
        var cut = RenderTooltip();
        await cut.Find("div").TriggerEventAsync("onfocusin", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));

        cut.Find("div").TriggerEvent("onfocusout", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());
        // Content stays mounted through its zoom-out exit window (B11 parity) — poll
        // for the unmount rather than asserting instant removal.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='tooltip']")), timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Escape_Dismisses_Focused_Tooltip()
    {
        var cut = RenderTooltip();
        await cut.Find("div").TriggerEventAsync("onfocusin", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));

        cut.Find("div").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='tooltip']")), timeout: TimeSpan.FromSeconds(5));
    }

    // ---------------------------------------------------------------------------
    // User-reported production bug: a mouse click on a Tooltip-wrapped clickable
    // element leaves it with plain DOM :focus (nothing clears it after a click —
    // browsers never do), so opening on ANY focusin kept the tooltip stuck open
    // after the click, long after the mouse moved away. Gated on
    // IsActiveElementFocusVisible (real browser :focus-visible semantics: false for
    // a mouse-click focus, true for keyboard navigation, in supporting browsers).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Click_Driven_Focus_Does_Not_Open_The_Tooltip()
    {
        // Simulate a mouse click leaving click-driven (non-focus-visible) DOM focus —
        // exactly what a real browser does after clicking a native <button>.
        _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={TestJsModuleVersion}")
            .Setup<bool>("isActiveElementFocusVisible")
            .SetResult(false);

        var cut = RenderTooltip();
        await cut.Find("div").TriggerEventAsync("onfocusin", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        Assert.Empty(cut.FindAll("[role='tooltip']"));
    }

    [Fact]
    public async Task Real_Keyboard_Focus_Still_Opens_The_Tooltip()
    {
        // This class's constructor already stubs this true by default (matching real
        // keyboard/programmatic focus), but assert it explicitly here too so this
        // specific contract has its own named, obviously-intentional test.
        _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={TestJsModuleVersion}")
            .Setup<bool>("isActiveElementFocusVisible")
            .SetResult(true);

        var cut = RenderTooltip();
        await cut.Find("div").TriggerEventAsync("onfocusin", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));
    }

    // --- G34b: AsChild puts aria-describedby on the focusable element ---

    private IRenderedComponent<IComponent> RenderTooltipAsChild()
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "AsChild", true);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.Button>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Hover me")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();

                b.OpenComponent<L.TooltipContent>(3);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Tooltip text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void AsChild_Renders_The_Button_As_Trigger_Without_The_Inner_Wrapper()
    {
        var cut = RenderTooltipAsChild();

        Assert.NotNull(cut.Find("button"));
        // The Tooltip ROOT keeps "relative inline-flex"; the TooltipTrigger no longer
        // adds its own bare inline-flex wrapper div.
        Assert.DoesNotContain(cut.FindAll("div"),
            d => (d.GetAttribute("class") ?? "") == "inline-flex");
    }

    [Fact]
    public void AsChild_Puts_AriaDescribedBy_On_The_Button_When_Open()
    {
        var cut = RenderTooltipAsChild();
        Assert.False(cut.Find("button").HasAttribute("aria-describedby")); // closed: none

        // Open via the Tooltip root wrapper (mirrors OpenTooltip; the root carries the
        // hover handler).
        cut.Find("div").TriggerEvent("onmouseenter", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        var content = cut.Find("[role='tooltip']");
        var button = cut.Find("button");
        // The describedby now sits on the FOCUSABLE button, so a screen reader
        // announces the tooltip on focus (an ancestor div could not).
        Assert.Equal(content.GetAttribute("id"), button.GetAttribute("aria-describedby"));
    }
}
