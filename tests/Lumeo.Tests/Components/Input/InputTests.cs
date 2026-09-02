using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.Input;

// Values moved in the 5.0 scale alignment: every control sits one rung lower, on what the
// shadcn CLI writes into a new project today - and on reui's values where reui defines the
// component too, since reui takes precedence there. The ladder these tests guard, distinct
// and monotonic rungs, is unchanged.
public class InputTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InputTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Input_Element()
    {
        var cut = _ctx.Render<Lumeo.Input>();

        Assert.NotNull(cut.Find("input"));
    }

    [Fact]
    public void Clearable_Renders_Wrapper_Even_While_Empty()
    {
        // Regression: keying the wrapper branch off "has a value" recreated
        // the <input> element on the first typed character (and when deleting
        // the last one), dropping focus and caret mid-typing. The wrapper must
        // be stable; only the clear BUTTON is value-conditional.
        var empty = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Clearable, true)
            .Add(b => b.Value, ""));
        Assert.NotNull(empty.Find("div input"));
        Assert.Empty(empty.FindAll("button"));

        var filled = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Clearable, true)
            .Add(b => b.Value, "x"));
        Assert.NotNull(filled.Find("div input"));
        Assert.NotEmpty(filled.FindAll("button"));
    }

    [Fact]
    public void Renders_With_Default_Value()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Value, "hello"));

        var input = cut.Find("input");
        Assert.Equal("hello", input.GetAttribute("value"));
    }

    [Fact]
    public void Renders_Without_Value_By_Default()
    {
        var cut = _ctx.Render<Lumeo.Input>();

        var input = cut.Find("input");
        Assert.True(string.IsNullOrEmpty(input.GetAttribute("value")));
    }

    [Fact]
    public void Has_Base_Classes()
    {
        var cut = _ctx.Render<Lumeo.Input>();

        var cls = cut.Find("input").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("h-[var(--lumeo-control-h,calc(var(--spacing,0.25rem)*8))]", cls);
        Assert.Contains("w-full", cls);
        Assert.Contains("rounded-lg", cls);
        Assert.Contains("border", cls);
        Assert.Contains("border-input", cls);
        Assert.Contains("bg-transparent", cls);
        Assert.Contains("px-2.5", cls);
        Assert.Contains("py-1", cls);
        Assert.Contains("text-sm", cls);
    }

    [Fact]
    public void OnInput_Event_Fires_On_Input()
    {
        ChangeEventArgs? receivedArgs = null;
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.OnInput, args => { receivedArgs = args; }));

        cut.Find("input").Input("new value");

        Assert.NotNull(receivedArgs);
        Assert.Equal("new value", receivedArgs.Value?.ToString());
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Class, "my-input-class"));

        var cls = cut.Find("input").GetAttribute("class");
        Assert.Contains("my-input-class", cls);
        Assert.Contains("flex", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-input",
                ["placeholder"] = "Enter text",
                ["type"] = "email"
            }));

        var input = cut.Find("input");
        Assert.Equal("my-input", input.GetAttribute("data-testid"));
        Assert.Equal("Enter text", input.GetAttribute("placeholder"));
        Assert.Equal("email", input.GetAttribute("type"));
    }

    [Fact]
    public void Disabled_Attribute_Is_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["disabled"] = true
            }));

        Assert.NotNull(cut.Find("input[disabled]"));
    }

    // --- Size variants ---

    [Fact]
    public void Size_Sm_Adds_H8_Class()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Size, Lumeo.Size.Sm));

        var cls = cut.Find("input").GetAttribute("class");
        Assert.Contains("h-[var(--lumeo-control-h,calc(var(--spacing,0.25rem)*8))]", cls);
    }

    [Fact]
    public void Size_Lg_Adds_H11_Class()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Size, Lumeo.Size.Lg));

        var cls = cut.Find("input").GetAttribute("class");
        Assert.Contains("h-11", cls);
    }

    // --- iOS zoom fix (text-base md:text-*) ---

    [Fact]
    public void Default_Size_Renders_TextBase_And_MdTextSm()
    {
        // shadcn Input: "... text-base ... md:text-sm ...". iOS Safari auto-zooms the
        // viewport when a focused input's font-size is below 16px; text-base (16px) is
        // the mobile floor, md:text-sm (14px) only applies at >=768px where the zoom
        // bug can't occur. Both utilities occupy different (variant, font-size) slots
        // in Cx.Merge so neither discards the other — verified by asserting both are
        // present on the rendered element, not just one.
        var cut = _ctx.Render<Lumeo.Input>();

        var cls = cut.Find("input").GetAttribute("class") ?? "";
        Assert.Contains("text-base", cls);
        Assert.Contains("md:text-sm", cls);
    }

    [Fact]
    public void Sm_Size_Renders_TextBase_And_MdTextXs()
    {
        // Sm isn't a shadcn Input primitive, but at the previous fixed text-xs (12px)
        // it carried the identical below-16px iOS zoom bug — arguably worse than
        // Default's old text-sm (14px). Closed the same way: text-base on mobile,
        // md:text-xs restoring the tighter desktop look.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Size, Lumeo.Size.Sm));

        var cls = cut.Find("input").GetAttribute("class") ?? "";
        Assert.Contains("text-base", cls);
        Assert.Contains("md:text-xs", cls);
        Assert.DoesNotContain("md:text-sm", cls);
    }

    [Fact]
    public void Lg_Size_Stays_TextBase_At_Every_Breakpoint()
    {
        // Lg was already text-base (16px) at every breakpoint before this change, so it
        // never had the iOS zoom bug and is left untouched — no md: breakpoint added,
        // since shrinking it on desktop would be a pure style change, not a bug fix.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Size, Lumeo.Size.Lg));

        var cls = cut.Find("input").GetAttribute("class") ?? "";
        Assert.Contains("text-base", cls);
        Assert.DoesNotContain("md:text-", cls);
    }

    [Fact]
    public void Wrapped_Input_Branch_Also_Gets_TextBase_MdTextSm()
    {
        // The Clearable/Prefix/Suffix/Search/number-stepper branch routes the actual
        // <input>'s font-size through WrappedInputSizeClasses, not SizeClasses — the
        // zoom check reads the focused element's OWN computed font-size, and an
        // explicit text-* on the <input> itself always wins over whatever the ancestor
        // wrapper div (WrapperClass/WrapperSizeClasses) declares. Predicted-vs-actual:
        // before this fix, this branch's <input> stayed at plain text-sm regardless of
        // the SizeClasses-only edit — confirmed manually before adding the fix.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Clearable, true)
            .Add(b => b.Value, "x"));

        var cls = cut.Find("div input").GetAttribute("class") ?? "";
        Assert.Contains("text-base", cls);
        Assert.Contains("md:text-sm", cls);
    }

    // --- Codex review of PR #386, finding 4: Class font-size override must win at every
    // breakpoint, not just the unprefixed one. These are class-string-level regression
    // guards; the load-bearing assertion (the actual COMPUTED font-size at both
    // breakpoints, not the class string) lives in
    // Lumeo.Tests.E2E.Smokes.InputSizingTests.Class_font_size_override_wins_at_every_breakpoint
    // — bUnit never applies real CSS, so it cannot observe which of two same-specificity
    // classes the browser actually renders. ---

    [Fact]
    public void Class_TextLg_Override_Suppresses_The_Components_Own_Responsive_Pair()
    {
        // Before this fix, Cx.Merge correctly replaced Input's unprefixed text-base with
        // the caller's text-lg (same conflict group) but left the SEPARATE md:text-sm
        // token untouched (a different group, keyed by variant chain) — silently winning
        // back at >=768px. The fix drops the component's own text-base/md:text-sm/
        // max-md:leading-none trio entirely once an explicit override is detected.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Class, "text-lg"));

        var cls = cut.Find("input").GetAttribute("class") ?? "";
        Assert.Contains("text-lg", cls);
        Assert.DoesNotContain("md:text-sm", cls);
        Assert.DoesNotContain("text-base", cls);
        Assert.DoesNotContain("max-md:leading-none", cls);
    }

    [Fact]
    public void Class_TextXs_Override_Suppresses_The_Components_Own_Responsive_Pair()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Class, "text-xs"));

        var cls = cut.Find("input").GetAttribute("class") ?? "";
        Assert.Contains("text-xs", cls);
        Assert.DoesNotContain("md:text-sm", cls);
        Assert.DoesNotContain("text-base", cls);
    }

    [Fact]
    public void Class_Without_A_Font_Size_Token_Does_Not_Suppress_The_Zoom_Guard()
    {
        // Regression guard for the override-detection regex: a Class that merely
        // contains the SUBSTRING "text" (uppercase utility, unrelated to font-size)
        // must not be mistaken for a font-size override.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Class, "uppercase tracking-wide"));

        var cls = cut.Find("input").GetAttribute("class") ?? "";
        Assert.Contains("text-base", cls);
        Assert.Contains("md:text-sm", cls);
    }

    [Theory]
    [InlineData("text-lg/6")]   // named size + line-height slash modifier
    [InlineData("text-[17px]")] // arbitrary value, no modifier
    [InlineData("text-[17px]/6")] // arbitrary value + line-height slash modifier together
    public void Class_With_LineHeight_Or_Arbitrary_FontSize_Forms_Suppresses_The_Zoom_Guard(string className)
    {
        // Codex round-3 finding: the ORIGINAL hand-rolled regex here recognised named
        // sizes and arbitrary values but not the `text-lg/6` line-height-modifier form —
        // even though Cx.Merge itself already classified that form as a font-size
        // conflict (TailwindMerge.IsTextSize strips the slash before matching). The fix
        // reuses TailwindMerge's own classification (HasUnprefixedFontSizeClass) instead
        // of maintaining a second regex, so every form Cx.Merge recognises is covered
        // here by construction, not by enumeration. Predicted WRONG value under the old
        // regex for "text-lg/6" specifically: md:text-sm SURVIVES (regex doesn't match,
        // suppression never fires) — i.e. this assertion (DoesNotContain) is exactly the
        // one that used to fail for that case.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Class, className));

        var cls = cut.Find("input").GetAttribute("class") ?? "";
        Assert.Contains(className, cls);
        Assert.DoesNotContain("md:text-sm", cls);
        Assert.DoesNotContain("text-base", cls);
    }

    [Fact]
    public void Class_With_A_Responsive_FontSize_Token_Does_Not_Suppress_The_Zoom_Guard()
    {
        // A caller-supplied `md:text-lg` (already breakpoint-scoped) is a DIFFERENT
        // conflict group than the component's own unprefixed text-base — Cx.Merge
        // already resolves this correctly (same-group `md:` conflicts merge; different
        // groups coexist), so the override-suppression path — which only targets
        // UNPREFIXED font-size tokens — must not fire here.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Class, "md:text-lg"));

        var cls = cut.Find("input").GetAttribute("class") ?? "";
        Assert.Contains("text-base", cls);
        Assert.Contains("md:text-lg", cls);
        Assert.DoesNotContain("md:text-sm", cls);
    }

    // --- Codex review of PR #386, finding 3: text-base's bundled 24px line-height can
    // exceed the smallest controls' available content-box height. max-md:leading-none
    // tightens ONLY the sub-768px state, leaving desktop's md:text-sm/md:text-xs
    // line-height untouched. The load-bearing assertion (line-height vs. actual rendered
    // client height/padding, not the class string) lives in
    // Lumeo.Tests.E2E.Smokes.InputSizingTests.No_line_box_exceeds_its_control_height. ---

    [Fact]
    public void Default_No_Override_Renders_MaxMd_Leading_None()
    {
        var cut = _ctx.Render<Lumeo.Input>();

        Assert.Contains("max-md:leading-none", cut.Find("input").GetAttribute("class") ?? "");
    }

    [Fact]
    public void Sm_No_Override_Renders_MaxMd_Leading_None()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Size, Lumeo.Size.Sm));

        Assert.Contains("max-md:leading-none", cut.Find("input").GetAttribute("class") ?? "");
    }

    [Fact]
    public void Lg_Never_Needed_MaxMd_Leading_None()
    {
        // Lg's control heights always had >=24px of available content-box height at
        // text-base's default line-height (measured: 30-38px available vs. 24px needed),
        // so it never overflowed and gets no leading override — adding one would be an
        // unrelated style change, not a bug fix.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Size, Lumeo.Size.Lg));

        Assert.DoesNotContain("leading-none", cut.Find("input").GetAttribute("class") ?? "");
    }

    [Fact]
    public void Wrapped_Input_Branch_Also_Renders_MaxMd_Leading_None()
    {
        // The Clearable/Prefix/Suffix/Search/number-stepper branch's <input> font-size
        // comes from WrappedInputSizeClasses, not SizeClasses — must carry the same
        // line-height fix as the plain branch.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Clearable, true)
            .Add(b => b.Value, "x"));

        Assert.Contains("max-md:leading-none", cut.Find("div input").GetAttribute("class") ?? "");
    }

    // --- shadow-xs ---

    [Fact]
    public void Renders_ShadowXs()
    {
        // shadcn Input: "... shadow-xs ...".
        var cut = _ctx.Render<Lumeo.Input>();

        Assert.DoesNotContain("shadow", cut.Find("input").GetAttribute("class"));
    }

    [Fact]
    public void Wrapper_Branch_Also_Renders_ShadowXs()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Clearable, true)
            .Add(b => b.Value, "x"));

        // The <input>'s immediate parent is the WrapperClass div — the outer root div
        // (`flex flex-col items-start`) also matches a bare `div.flex` selector, so
        // walk up from the <input> instead of selecting by class.
        var wrapperDiv = cut.Find("div input").ParentElement;
        Assert.NotNull(wrapperDiv);
        Assert.DoesNotContain("shadow", wrapperDiv!.GetAttribute("class"));
    }

    // --- Clearable ---

    [Fact]
    public void Clearable_Shows_X_Button_When_Value_NonEmpty()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Value, "hello")
            .Add(b => b.Clearable, true));

        var clearBtn = cut.Find("button[aria-label='Clear']");
        Assert.NotNull(clearBtn);
    }

    [Fact]
    public void Clearable_No_Button_When_Value_Empty()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Value, "")
            .Add(b => b.Clearable, true));

        // When value is empty, the clearable button path is not rendered
        // and the bare input is rendered instead
        Assert.Empty(cut.FindAll("button"));
    }

    // --- ShowCount / MaxLength (parity with Textarea) ---

    [Fact]
    public void ShowCount_Renders_Character_Count()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.Value, "hello"));

        Assert.Contains("5", cut.Markup);
    }

    [Fact]
    public void ShowCount_With_MaxLength_Renders_Count_And_Max()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.MaxLength, 100)
            .Add(t => t.Value, "hello"));

        Assert.Contains("5/100", cut.Markup);
    }

    [Fact]
    public void MaxLength_Forwarded_To_Input_Element()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p.Add(t => t.MaxLength, 42));
        Assert.Equal("42", cut.Find("input").GetAttribute("maxlength"));
    }

    [Fact]
    public void ShowCount_Over_Limit_Uses_Destructive_Color()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.MaxLength, 3)
            .Add(t => t.Value, "hello")); // 5 > 3

        var counterDivs = cut.FindAll("div.text-end").ToList();
        Assert.Single(counterDivs);
        Assert.Contains("text-destructive", counterDivs[0].GetAttribute("class") ?? "");
    }

    [Fact]
    public void ShowCount_With_CountFormat_Uses_Custom_Format()
    {
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(t => t.ShowCount, true)
            .Add(t => t.Value, "ab")
            .Add(t => t.CountFormat, c => $"{c} chars"));

        Assert.Contains("2 chars", cut.Markup);
    }
}
