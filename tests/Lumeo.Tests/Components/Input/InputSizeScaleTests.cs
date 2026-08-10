using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Input;

// Pins all 7 Lumeo.Size rungs (Xxs, Xs, Sm, Md, Lg, Xl, Xxl) across Input's THREE
// parallel (Size, Density) tables: SizeClasses (plain <input>, no-wrapper path),
// WrapperSizeClasses (wrapper div — h + text only, no px) and WrappedInputSizeClasses
// (the <input> inside the wrapper — px + text only, no h). Missing a rung in any ONE
// of the three is exactly the wrapper-vs-inner-element defect this campaign exists to
// close, so every rung is asserted against all three tables, not just the plain path.
//
// IMPORTANT: assertions use exact space-delimited token matching (AssertHasClass),
// never Assert.Contains(substring, cls) — Tailwind's own scale defeats naive substring
// checks (e.g. "px-2" is a substring of "px-2.5", "h-1" is a prefix of "h-11").
public class InputSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InputSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void AssertHasClass(string? cls, string token)
    {
        var tokens = (cls ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(token, tokens);
    }

    private static void AssertDoesNotHaveClass(string? cls, string token)
    {
        var tokens = (cls ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.DoesNotContain(token, tokens);
    }

    // ============================== Plain <input> path (SizeClasses) ==============================

    [Theory]
    // Comfortable (default density)
    [InlineData(L.Size.Xxs, L.Density.Comfortable, "h-6", "px-1.5")]
    [InlineData(L.Size.Xs, L.Density.Comfortable, "h-7", "px-2")]
    [InlineData(L.Size.Sm, L.Density.Comfortable, "h-8", "px-2.5")]
    [InlineData(L.Size.Md, L.Density.Comfortable, "h-9", "px-3")]
    [InlineData(L.Size.Lg, L.Density.Comfortable, "h-11", "px-4")]
    [InlineData(L.Size.Xl, L.Density.Comfortable, "h-[52px]", "px-5")]
    [InlineData(L.Size.Xxl, L.Density.Comfortable, "h-[60px]", "px-6")]
    // Compact
    [InlineData(L.Size.Xxs, L.Density.Compact, "h-5", "px-1")]
    [InlineData(L.Size.Xs, L.Density.Compact, "h-6", "px-1.5")]
    [InlineData(L.Size.Sm, L.Density.Compact, "h-7", "px-2")]
    [InlineData(L.Size.Md, L.Density.Compact, "h-8", "px-2.5")]
    [InlineData(L.Size.Lg, L.Density.Compact, "h-10", "px-3")]
    [InlineData(L.Size.Xl, L.Density.Compact, "h-12", "px-3.5")]
    [InlineData(L.Size.Xxl, L.Density.Compact, "h-14", "px-4")]
    // Spacious
    [InlineData(L.Size.Xxs, L.Density.Spacious, "h-7", "px-2")]
    [InlineData(L.Size.Xs, L.Density.Spacious, "h-8", "px-2")]
    [InlineData(L.Size.Sm, L.Density.Spacious, "h-9", "px-3")]
    [InlineData(L.Size.Md, L.Density.Spacious, "h-10", "px-4")]
    [InlineData(L.Size.Lg, L.Density.Spacious, "h-12", "px-5")]
    [InlineData(L.Size.Xl, L.Density.Spacious, "h-14", "px-6")]
    [InlineData(L.Size.Xxl, L.Density.Spacious, "h-16", "px-7")]
    public void Plain_Input_Height_And_Padding_Per_Rung(L.Size size, L.Density density, string h, string px)
    {
        var cut = _ctx.Render<L.Input>(p => p.Add(i => i.Size, size).Add(i => i.Density, density));

        var cls = cut.Find("input").GetAttribute("class");
        AssertHasClass(cls, h);
        AssertHasClass(cls, px);
    }

    // Sm/Md/Xl/Xxl keep the iOS-zoom guard (16px mobile floor, md: breakpoint split) exactly
    // as shipped. Xxs/Xs do NOT — see Plain_Input_Xxs_Xs_Render_Small_Font_At_Every_Breakpoint
    // below for the owner-decided exception (PR #391).
    [Theory]
    [InlineData(L.Size.Sm, "md:text-xs")]
    [InlineData(L.Size.Md, "md:text-sm")]
    [InlineData(L.Size.Xl, "md:text-lg")]
    [InlineData(L.Size.Xxl, "md:text-xl")]
    public void Plain_Input_Desktop_Text_Size_Per_Rung(L.Size size, string mdTextClass)
    {
        var cut = _ctx.Render<L.Input>(p => p.Add(i => i.Size, size));

        var cls = cut.Find("input").GetAttribute("class");
        AssertHasClass(cls, "text-base"); // mobile floor, iOS-zoom guard
        AssertHasClass(cls, mdTextClass);
    }

    [Fact]
    public void Plain_Input_Lg_Has_No_Md_Override_And_No_LeadingNone()
    {
        // Lg was already text-base at every breakpoint before this campaign — untouched.
        var cut = _ctx.Render<L.Input>(p => p.Add(i => i.Size, L.Size.Lg));

        var cls = cut.Find("input").GetAttribute("class") ?? "";
        AssertHasClass(cls, "text-base");
        AssertDoesNotHaveClass(cls, "md:text-lg");
        Assert.DoesNotContain("md:text-", cls);
        Assert.DoesNotContain("leading-none", cls);
    }

    [Theory]
    [InlineData(L.Size.Sm)]
    [InlineData(L.Size.Md)]
    [InlineData(L.Size.Xl)]
    [InlineData(L.Size.Xxl)]
    public void Plain_Input_Renders_MaxMd_Leading_None_At_Every_Guarded_Rung(L.Size size)
    {
        // Xxs/Xs are excluded — they carry a bare (non-breakpoint-gated) leading-none
        // instead, asserted separately below.
        var cut = _ctx.Render<L.Input>(p => p.Add(i => i.Size, size));
        AssertHasClass(cut.Find("input").GetAttribute("class"), "max-md:leading-none");
    }

    // ============================== Xxs/Xs owner-decided exception (PR #391) ==============================
    // The owner chose: control size wins over the iOS-zoom guard at Xxs and Xs. Those two
    // rungs render their small font (8px / 10px) UNCONDITIONALLY — no text-base mobile floor,
    // no md: breakpoint split — paired with a permanent (non-gated) leading-none so the line
    // box fits inside the control at every density. Sm and up are untouched (asserted above).

    [Theory]
    [InlineData(L.Size.Xxs, "text-[8px]")]
    [InlineData(L.Size.Xs, "text-[10px]")]
    public void Plain_Input_Xxs_Xs_Render_Small_Font_At_Every_Breakpoint(L.Size size, string textClass)
    {
        var cut = _ctx.Render<L.Input>(p => p.Add(i => i.Size, size));

        var cls = cut.Find("input").GetAttribute("class") ?? "";
        AssertHasClass(cls, textClass);
        AssertHasClass(cls, "leading-none");
        AssertDoesNotHaveClass(cls, "text-base");
        AssertDoesNotHaveClass(cls, "max-md:leading-none");
        Assert.DoesNotContain("md:text-", cls);
    }

    // ============================== Wrapper div path (WrapperSizeClasses) ==============================

    [Theory]
    [InlineData(L.Size.Xxs, L.Density.Comfortable, "h-6")]
    [InlineData(L.Size.Xs, L.Density.Comfortable, "h-7")]
    [InlineData(L.Size.Sm, L.Density.Comfortable, "h-8")]
    [InlineData(L.Size.Md, L.Density.Comfortable, "h-9")]
    [InlineData(L.Size.Lg, L.Density.Comfortable, "h-11")]
    [InlineData(L.Size.Xl, L.Density.Comfortable, "h-[52px]")]
    [InlineData(L.Size.Xxl, L.Density.Comfortable, "h-[60px]")]
    [InlineData(L.Size.Xxs, L.Density.Compact, "h-5")]
    [InlineData(L.Size.Xs, L.Density.Compact, "h-6")]
    [InlineData(L.Size.Sm, L.Density.Compact, "h-7")]
    [InlineData(L.Size.Md, L.Density.Compact, "h-8")]
    [InlineData(L.Size.Lg, L.Density.Compact, "h-10")]
    [InlineData(L.Size.Xl, L.Density.Compact, "h-12")]
    [InlineData(L.Size.Xxl, L.Density.Compact, "h-14")]
    [InlineData(L.Size.Xxs, L.Density.Spacious, "h-7")]
    [InlineData(L.Size.Xs, L.Density.Spacious, "h-8")]
    [InlineData(L.Size.Sm, L.Density.Spacious, "h-9")]
    [InlineData(L.Size.Md, L.Density.Spacious, "h-10")]
    [InlineData(L.Size.Lg, L.Density.Spacious, "h-12")]
    [InlineData(L.Size.Xl, L.Density.Spacious, "h-14")]
    [InlineData(L.Size.Xxl, L.Density.Spacious, "h-16")]
    public void Wrapper_Div_Height_Per_Rung(L.Size size, L.Density density, string h)
    {
        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Size, size)
            .Add(i => i.Density, density)
            .Add(i => i.Clearable, true)
            .Add(i => i.Value, "x"));

        // Walk up from the <input> — the outer root div (`flex flex-col items-start`)
        // also matches a bare `div.flex` selector (see InputTests.cs's
        // Wrapper_Branch_Also_Renders_ShadowXs for the identical precedent).
        var wrapperDiv = cut.Find("div input").ParentElement;
        AssertHasClass(wrapperDiv!.GetAttribute("class"), h);
    }

    [Theory]
    [InlineData(L.Size.Xl, "md:text-lg")]
    [InlineData(L.Size.Xxl, "md:text-xl")]
    public void Wrapper_Div_Desktop_Text_Size_Per_New_Rung(L.Size size, string mdTextClass)
    {
        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Size, size)
            .Add(i => i.Clearable, true)
            .Add(i => i.Value, "x"));

        var wrapperDiv = cut.Find("div input").ParentElement;
        var cls = wrapperDiv!.GetAttribute("class");
        AssertHasClass(cls, "text-base");
        AssertHasClass(cls, mdTextClass);
        AssertHasClass(cls, "max-md:leading-none");
    }

    [Theory]
    [InlineData(L.Size.Xxs, "text-[8px]")]
    [InlineData(L.Size.Xs, "text-[10px]")]
    public void Wrapper_Div_Xxs_Xs_Render_Small_Font_At_Every_Breakpoint(L.Size size, string textClass)
    {
        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Size, size)
            .Add(i => i.Clearable, true)
            .Add(i => i.Value, "x"));

        var wrapperDiv = cut.Find("div input").ParentElement;
        var cls = wrapperDiv!.GetAttribute("class") ?? "";
        AssertHasClass(cls, textClass);
        AssertHasClass(cls, "leading-none");
        AssertDoesNotHaveClass(cls, "text-base");
        AssertDoesNotHaveClass(cls, "max-md:leading-none");
        Assert.DoesNotContain("md:text-", cls);
    }

    // ============================== Wrapped <input> path (WrappedInputSizeClasses) ==============================

    [Theory]
    [InlineData(L.Size.Xxs, L.Density.Comfortable, "px-1.5")]
    [InlineData(L.Size.Xs, L.Density.Comfortable, "px-2")]
    [InlineData(L.Size.Sm, L.Density.Comfortable, "px-2.5")]
    [InlineData(L.Size.Md, L.Density.Comfortable, "px-3")]
    [InlineData(L.Size.Lg, L.Density.Comfortable, "px-4")]
    [InlineData(L.Size.Xl, L.Density.Comfortable, "px-5")]
    [InlineData(L.Size.Xxl, L.Density.Comfortable, "px-6")]
    [InlineData(L.Size.Xxs, L.Density.Compact, "px-1")]
    [InlineData(L.Size.Xs, L.Density.Compact, "px-1.5")]
    [InlineData(L.Size.Sm, L.Density.Compact, "px-2")]
    [InlineData(L.Size.Md, L.Density.Compact, "px-2.5")]
    [InlineData(L.Size.Lg, L.Density.Compact, "px-3")]
    [InlineData(L.Size.Xl, L.Density.Compact, "px-3.5")]
    [InlineData(L.Size.Xxl, L.Density.Compact, "px-4")]
    [InlineData(L.Size.Xxs, L.Density.Spacious, "px-2")]
    [InlineData(L.Size.Xs, L.Density.Spacious, "px-2")]
    [InlineData(L.Size.Sm, L.Density.Spacious, "px-3")]
    [InlineData(L.Size.Md, L.Density.Spacious, "px-4")]
    [InlineData(L.Size.Lg, L.Density.Spacious, "px-5")]
    [InlineData(L.Size.Xl, L.Density.Spacious, "px-6")]
    [InlineData(L.Size.Xxl, L.Density.Spacious, "px-7")]
    public void Wrapped_Input_Padding_Per_Rung(L.Size size, L.Density density, string px)
    {
        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Size, size)
            .Add(i => i.Density, density)
            .Add(i => i.Clearable, true)
            .Add(i => i.Value, "x"));

        var cls = cut.Find("div input").GetAttribute("class");
        AssertHasClass(cls, px);
    }

    [Theory]
    [InlineData(L.Size.Xl, "md:text-lg")]
    [InlineData(L.Size.Xxl, "md:text-xl")]
    public void Wrapped_Input_Desktop_Text_Size_Per_New_Rung(L.Size size, string mdTextClass)
    {
        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Size, size)
            .Add(i => i.Clearable, true)
            .Add(i => i.Value, "x"));

        var cls = cut.Find("div input").GetAttribute("class");
        AssertHasClass(cls, "text-base");
        AssertHasClass(cls, mdTextClass);
        AssertHasClass(cls, "max-md:leading-none");
    }

    [Theory]
    [InlineData(L.Size.Xxs, "text-[8px]")]
    [InlineData(L.Size.Xs, "text-[10px]")]
    public void Wrapped_Input_Xxs_Xs_Render_Small_Font_At_Every_Breakpoint(L.Size size, string textClass)
    {
        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Size, size)
            .Add(i => i.Clearable, true)
            .Add(i => i.Value, "x"));

        var cls = cut.Find("div input").GetAttribute("class") ?? "";
        AssertHasClass(cls, textClass);
        AssertHasClass(cls, "leading-none");
        AssertDoesNotHaveClass(cls, "text-base");
        AssertDoesNotHaveClass(cls, "max-md:leading-none");
        Assert.DoesNotContain("md:text-", cls);
    }

    // ============================== Deliberate ties (flagged, not bugs) ==============================

    // Spec's derivation methodology (S1/S2 linear extrapolation per density arm) would
    // put Xxs/Spacious padding BELOW Xxs/Comfortable's (a same-row density inversion —
    // padding must never DECREASE as density increases within the same rung, and every
    // other rung's row strictly increases Compact -> Comfortable -> Spacious). The spec
    // deliberately ties Xxs/Spacious with Xs/Spacious at px-2 instead of following the
    // naive formula down to px-1, avoiding that inversion at the cost of a same-density
    // tie between two adjacent Size rungs. Documented explicitly so a future "fix" that
    // reintroduces the naive px-1 value is caught here rather than silently regressing
    // Xxs's own Compact -> Comfortable -> Spacious padding order.
    [Fact]
    public void Xxs_And_Xs_Spacious_Padding_Tie_At_Px2()
    {
        var xxs = _ctx.Render<L.Input>(p => p.Add(i => i.Size, L.Size.Xxs).Add(i => i.Density, L.Density.Spacious));
        var xs = _ctx.Render<L.Input>(p => p.Add(i => i.Size, L.Size.Xs).Add(i => i.Density, L.Density.Spacious));

        AssertHasClass(xxs.Find("input").GetAttribute("class"), "px-2");
        AssertHasClass(xs.Find("input").GetAttribute("class"), "px-2");
    }

    [Fact]
    public void Xxs_Padding_Strictly_Increases_Compact_To_Comfortable_To_Spacious()
    {
        // Regression guard for the tie above: within the Xxs ROW itself (fixed Size,
        // varying Density) padding must never decrease as density increases, even
        // though it ties with the adjacent Xs rung at Spacious.
        var compact = _ctx.Render<L.Input>(p => p.Add(i => i.Size, L.Size.Xxs).Add(i => i.Density, L.Density.Compact));
        var comfortable = _ctx.Render<L.Input>(p => p.Add(i => i.Size, L.Size.Xxs).Add(i => i.Density, L.Density.Comfortable));
        var spacious = _ctx.Render<L.Input>(p => p.Add(i => i.Size, L.Size.Xxs).Add(i => i.Density, L.Density.Spacious));

        AssertHasClass(compact.Find("input").GetAttribute("class"), "px-1");
        AssertHasClass(comfortable.Find("input").GetAttribute("class"), "px-1.5");
        AssertHasClass(spacious.Find("input").GetAttribute("class"), "px-2");
    }

    // ============================== HasFontSizeOverride still wins at new rungs ==============================

    [Theory]
    [InlineData(L.Size.Xxs)]
    [InlineData(L.Size.Xs)]
    [InlineData(L.Size.Xl)]
    [InlineData(L.Size.Xxl)]
    public void Class_TextLg_Override_Suppresses_The_Responsive_Pair_At_New_Rungs(L.Size size)
    {
        // Regression guard mirroring InputTests.cs's Class_TextLg_Override_... but for
        // the NEW rungs specifically — the suppression mechanism (HasFontSizeOverride)
        // must keep working once XxsFontClasses/XsFontClasses/XlFontClasses/
        // XxlFontClasses exist, not just for the original SmFontClasses/DefaultFontClasses.
        // Xxs/Xs are included here too: HasFontSizeOverride suppresses their WHOLE font
        // string (own small font + leading-none), not just the guard's text-base/md: pair —
        // a caller who supplies their own font-size class opts out of everything, including
        // the leading-none that's otherwise load-bearing for those two rungs.
        var cut = _ctx.Render<L.Input>(p => p.Add(i => i.Size, size).Add(i => i.Class, "text-lg"));

        var cls = cut.Find("input").GetAttribute("class") ?? "";
        AssertHasClass(cls, "text-lg");
        Assert.DoesNotContain("text-base", cls);
        Assert.DoesNotContain("md:text-", cls);
        Assert.DoesNotContain("max-md:leading-none", cls);
        Assert.DoesNotContain("leading-none", cls);
    }
}
