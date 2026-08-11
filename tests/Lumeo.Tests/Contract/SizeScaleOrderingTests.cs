using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Contract;

/// <summary>
/// PR #388 task-brief structural gap: every per-component Size test pins each
/// rung INDIVIDUALLY. Nothing asserted the RELATIONSHIP between rungs, which is
/// why Switch's Loading-spinner Size (never mapped past a hardcoded Size="Sm")
/// and FileUpload's Xl/Xxl padding (narrower than Lg, not wider) both shipped
/// behind a fully green per-rung suite.
///
/// This file renders every Size-driven component in this PR at all 7 rungs, in
/// <see cref="SizeScaleAssert.VisualOrder"/> (Xxs &lt; Xs &lt; Sm &lt; Md &lt; Lg
/// &lt; Xl &lt; Xxl — NOT enum declaration order, where Xxs=6 is declared last),
/// parses the RENDERED class attribute for each Size-driven dimension into a
/// pixel value, and asserts the sequence is monotonically non-decreasing.
///
/// Where a component legitimately TIES two rungs, the tie is asserted explicitly
/// via a companion `_Tie` fact (Assert.Equal on both rendered values), not just
/// tolerated by the non-strict `&gt;=` in the ordering check — a deliberate tie
/// is documentation, an accidental one is a bug, and only an explicit assertion
/// tells them apart. Every tie below already has prior art in this exact PR: the
/// same rung(s) tie in an existing per-component test with an explanatory
/// comment (Kbd Sm/Md, Spinner Xxs/Xs and Xxs/Xs/Sm, SparkCard/Statistic Xxs/Xs,
/// ReasoningDisplay Xxs/Xs), or the tie predates this PR entirely and is
/// protected by the campaign's "never change an already-implemented rung" rule
/// (Alert.DescriptionClass, Chip.SizeClass/CloseIconSize Md/Lg — confirmed via
/// `git show origin/master:...` before this file was written).
///
/// Coverage note: not every dimension of every component is parseable this way.
/// Alert.TitleClass's Md rung has NO explicit text-* token (it inherits the
/// alert box's own ambient "text-sm") — there is nothing in the rendered class
/// attribute to parse, so that one rung is skipped for the text-size series
/// (the fully-parseable `mb-*` margin series on the same element is asserted
/// instead, and Alert's own per-component test pins the inheritance directly).
/// Rating's icon size is expressed as a `[&amp;_svg]:h-N` arbitrary-variant
/// class on the BUTTON (not a plain `h-N` token on the svg itself), so it gets
/// its own small regex extractor rather than <see cref="SizeScaleAssert.SpacingPx"/>.
/// </summary>
public class SizeScaleOrderingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SizeScaleOrderingTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<(L.Size Size, double Value)> Series(Func<L.Size, double?> render, string dimensionLabel)
    {
        var list = new List<(L.Size, double)>();
        foreach (var size in SizeScaleAssert.VisualOrder)
        {
            var value = render(size);
            Assert.True(value.HasValue, $"{dimensionLabel}: could not parse a value for {size} — see test for the expected class shape.");
            list.Add((size, value!.Value));
        }
        return list;
    }

    // ============================== Alert ==============================

    [Fact]
    public void Alert_Padding_Px_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, size).AddChildContent("m"));
            return SizeScaleAssert.SpacingPx(cut.Find("[role='alert']").GetAttribute("class"), "px");
        }, "Alert padding px");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Alert padding px");
    }

    [Fact]
    public void Alert_Padding_Py_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, size).AddChildContent("m"));
            return SizeScaleAssert.SpacingPx(cut.Find("[role='alert']").GetAttribute("class"), "py");
        }, "Alert padding py");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Alert padding py");
    }

    [Fact]
    public void Alert_Icon_Size_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, size).AddChildContent("m"));
            return SizeScaleAssert.SpacingPx(cut.Find("svg").GetAttribute("class"), "h");
        }, "Alert icon h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Alert icon h");
    }

    [Fact]
    public void Alert_Title_Margin_Is_Monotonic()
    {
        // TitleClass's mb-* token is explicit at EVERY rung (unlike its text-*
        // token, which Md omits — see class doc). Xxs/Xs tie at mb-0, documented below.
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, size).Add(a => a.Title, "T"));
            return SizeScaleAssert.SpacingPx(cut.Find("p").GetAttribute("class"), "mb");
        }, "Alert title mb");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Alert title mb");
    }

    [Fact]
    public void Alert_Title_Margin_Xxs_And_Xs_Tie_At_Mb0()
    {
        var xxs = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, L.Size.Xxs).Add(a => a.Title, "T"));
        var xs = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, L.Size.Xs).Add(a => a.Title, "T"));
        Assert.Equal(
            SizeScaleAssert.SpacingPx(xxs.Find("p").GetAttribute("class"), "mb"),
            SizeScaleAssert.SpacingPx(xs.Find("p").GetAttribute("class"), "mb"));
    }

    [Fact]
    public void Alert_Description_Text_Size_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, size).Add(a => a.Description, "D"));
            return SizeScaleAssert.TextSizePx(cut.Find("div.flex-1 > div").GetAttribute("class"));
        }, "Alert description text");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Alert description text");
    }

    [Fact]
    public void Alert_Description_Text_Md_And_Lg_Tie_At_TextSm_PreExisting()
    {
        // Pre-existing (predates this PR — see DescriptionClass's comment): Lg
        // stays at text-sm instead of stepping to text-base like the sibling
        // TitleClass does. Protected by "never change an already-implemented rung".
        var md = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, L.Size.Md).Add(a => a.Description, "D"));
        var lg = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, L.Size.Lg).Add(a => a.Description, "D"));
        Assert.Equal(
            SizeScaleAssert.TextSizePx(md.Find("div.flex-1 > div").GetAttribute("class")),
            SizeScaleAssert.TextSizePx(lg.Find("div.flex-1 > div").GetAttribute("class")));
    }

    // ============================== Avatar ==============================

    [Fact]
    public void Avatar_Size_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Avatar>(p => p.Add(a => a.Size, size));
            return SizeScaleAssert.SpacingPx(cut.Find("div").GetAttribute("class"), "h");
        }, "Avatar h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Avatar h");
    }

    // ============================== Chip ==============================

    [Fact]
    public void Chip_Text_Size_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Chip>(p => p.Add(c => c.Size, size).AddChildContent("X"));
            return SizeScaleAssert.TextSizePx(cut.Find("div").GetAttribute("class"));
        }, "Chip text");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Chip text");
    }

    [Fact]
    public void Chip_Text_Size_Md_And_Lg_Tie_At_TextSm_PreExisting()
    {
        // Pre-existing (predates this PR): SizeClass's Lg case is "text-sm",
        // identical to the Md fallback. Same shape as Alert's Description tie.
        var md = _ctx.Render<L.Chip>(p => p.Add(c => c.Size, L.Size.Md).AddChildContent("X"));
        var lg = _ctx.Render<L.Chip>(p => p.Add(c => c.Size, L.Size.Lg).AddChildContent("X"));
        Assert.Equal(
            SizeScaleAssert.TextSizePx(md.Find("div").GetAttribute("class")),
            SizeScaleAssert.TextSizePx(lg.Find("div").GetAttribute("class")));
    }

    [Fact]
    public void Chip_Padding_Px_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Chip>(p => p.Add(c => c.Size, size).AddChildContent("X"));
            return SizeScaleAssert.SpacingPx(cut.Find("div").GetAttribute("class"), "px");
        }, "Chip padding px");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Chip padding px");
    }

    [Fact]
    public void Chip_Padding_Py_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Chip>(p => p.Add(c => c.Size, size).AddChildContent("X"));
            return SizeScaleAssert.SpacingPx(cut.Find("div").GetAttribute("class"), "py");
        }, "Chip padding py");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Chip padding py");
    }

    [Fact]
    public void Chip_Avatar_Icon_Size_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Chip>(p => p.Add(c => c.Size, size).Add(c => c.Avatar, "/x.png").AddChildContent("X"));
            return SizeScaleAssert.SpacingPx(cut.Find("img").GetAttribute("class"), "h");
        }, "Chip avatar h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Chip avatar h");
    }

    [Fact]
    public void Chip_Close_Icon_Size_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Chip>(p => p.Add(c => c.Size, size).Add(c => c.Closable, true).AddChildContent("X"));
            return SizeScaleAssert.SpacingPx(cut.Find("button svg").GetAttribute("class"), "h");
        }, "Chip close icon h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Chip close icon h");
    }

    [Fact]
    public void Chip_Close_Icon_Size_Md_And_Lg_Tie_PreExisting()
    {
        // Pre-existing (comment on CloseIconSize in Chip.razor): Lg has no
        // explicit case and falls through to the same value as Md.
        var md = _ctx.Render<L.Chip>(p => p.Add(c => c.Size, L.Size.Md).Add(c => c.Closable, true).AddChildContent("X"));
        var lg = _ctx.Render<L.Chip>(p => p.Add(c => c.Size, L.Size.Lg).Add(c => c.Closable, true).AddChildContent("X"));
        Assert.Equal(
            SizeScaleAssert.SpacingPx(md.Find("button svg").GetAttribute("class"), "h"),
            SizeScaleAssert.SpacingPx(lg.Find("button svg").GetAttribute("class"), "h"));
    }

    // ============================== FileUpload ==============================

    [Fact]
    public void FileUpload_Button_Height_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.FileUpload>(p => p.Add(f => f.Variant, L.FileUpload.FileUploadVariant.Button).Add(f => f.Size, size));
            return SizeScaleAssert.SpacingPx(cut.Find("label").GetAttribute("class"), "h");
        }, "FileUpload button h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "FileUpload button h");
    }

    [Fact]
    public void FileUpload_Button_Padding_Is_Monotonic()
    {
        // The exact ramp Codex flagged (finding 2): Xl/Xxl were px-5/px-6,
        // BELOW Lg's own px-8 — a strict decrease, not just an inelegant tie.
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.FileUpload>(p => p.Add(f => f.Variant, L.FileUpload.FileUploadVariant.Button).Add(f => f.Size, size));
            return SizeScaleAssert.SpacingPx(cut.Find("label").GetAttribute("class"), "px");
        }, "FileUpload button px");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "FileUpload button px");
    }

    // ============================== Icon ==============================

    [Fact]
    public void Icon_Size_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Icon>(p => p.Add(i => i.Name, "Search").Add(i => i.Size, size));
            return SizeScaleAssert.SpacingPx(cut.Find("svg").GetAttribute("class"), "h");
        }, "Icon h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Icon h");
    }

    // ============================== Input ==============================

    // Input's mobile font-size class is deliberately the LITERAL "text-base" at every
    // rung (the iOS-zoom guard — see Input.razor's SizeClasses remarks), so SizeScaleAssert
    // .TextSizePx would just find "text-base" at every rung and report a flat, uninformative
    // series; text-size ordering for Input is instead exercised by the E2E suite (computed
    // desktop font-size), not here. Height and horizontal padding — the two dimensions whose
    // rendered class token actually varies by Size — are checked in all THREE size tables
    // (plain input / wrapper div / wrapped input), since a rung landing on only one of them
    // is exactly the wrapper-vs-inner-element defect this campaign exists to close.

    [Fact]
    public void Input_Plain_Height_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Input>(p => p.Add(i => i.Size, size));
            return SizeScaleAssert.SpacingPx(cut.Find("input").GetAttribute("class"), "h");
        }, "Input plain h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Input plain h");
    }

    [Fact]
    public void Input_Plain_Padding_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Input>(p => p.Add(i => i.Size, size));
            return SizeScaleAssert.SpacingPx(cut.Find("input").GetAttribute("class"), "px");
        }, "Input plain px");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Input plain px");
    }

    [Fact]
    public void Input_Wrapper_Height_Is_Monotonic()
    {
        // The <input>'s immediate parent is the WrapperClass div — the outer root div
        // (`flex flex-col items-start`) also matches a bare `div.flex` selector, so walk
        // up from the <input> instead of selecting by class (see InputTests.cs's
        // Wrapper_Branch_Also_Renders_ShadowXs for the same precedent).
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Input>(p => p.Add(i => i.Size, size).Add(i => i.Clearable, true).Add(i => i.Value, "x"));
            return SizeScaleAssert.SpacingPx(cut.Find("div input").ParentElement!.GetAttribute("class"), "h");
        }, "Input wrapper h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Input wrapper h");
    }

    [Fact]
    public void Input_WrappedInput_Padding_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Input>(p => p.Add(i => i.Size, size).Add(i => i.Clearable, true).Add(i => i.Value, "x"));
            return SizeScaleAssert.SpacingPx(cut.Find("div input").GetAttribute("class"), "px");
        }, "Input wrapped-input px");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Input wrapped-input px");
    }

    // ============================== Kbd ==============================

    [Fact]
    public void Kbd_Height_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Kbd>(p => p.Add(k => k.Size, size).AddChildContent("K"));
            return SizeScaleAssert.SpacingPx(cut.Find("kbd").GetAttribute("class"), "h");
        }, "Kbd h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Kbd h");
    }

    [Fact]
    public void Kbd_Height_Sm_And_Md_Tie()
    {
        var sm = _ctx.Render<L.Kbd>(p => p.Add(k => k.Size, L.Size.Sm).AddChildContent("K"));
        var md = _ctx.Render<L.Kbd>(p => p.Add(k => k.Size, L.Size.Md).AddChildContent("K"));
        Assert.Equal(
            SizeScaleAssert.SpacingPx(sm.Find("kbd").GetAttribute("class"), "h"),
            SizeScaleAssert.SpacingPx(md.Find("kbd").GetAttribute("class"), "h"));
    }

    [Fact]
    public void Kbd_Padding_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Kbd>(p => p.Add(k => k.Size, size).AddChildContent("K"));
            return SizeScaleAssert.SpacingPx(cut.Find("kbd").GetAttribute("class"), "px");
        }, "Kbd px");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Kbd px");
    }

    [Fact]
    public void Kbd_Text_Size_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Kbd>(p => p.Add(k => k.Size, size).AddChildContent("K"));
            return SizeScaleAssert.TextSizePx(cut.Find("kbd").GetAttribute("class"));
        }, "Kbd text");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Kbd text");
    }

    // ============================== List / ListItem ==============================

    [Fact]
    public void ListItem_Padding_Px_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.List>(p => p.Add(l => l.Size, size).AddChildContent<L.ListItem>(i => i.Add(x => x.Title, "r")));
            return SizeScaleAssert.SpacingPx(cut.Find("li").GetAttribute("class"), "px");
        }, "ListItem padding px");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "ListItem padding px");
    }

    [Fact]
    public void ListItem_Padding_Py_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.List>(p => p.Add(l => l.Size, size).AddChildContent<L.ListItem>(i => i.Add(x => x.Title, "r")));
            return SizeScaleAssert.SpacingPx(cut.Find("li").GetAttribute("class"), "py");
        }, "ListItem padding py");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "ListItem padding py");
    }

    // ============================== Rating ==============================

    // Rating expresses its star size as a `[&_svg]:h-N` arbitrary-variant class
    // on the <button> (targets the descendant <svg> via CSS, not a plain h-N
    // token on the svg itself), so it needs its own tiny extractor rather than
    // SizeScaleAssert.SpacingPx.
    private static double? RatingStarSizePx(string? buttonClass, string dim)
    {
        var m = Regex.Match(buttonClass ?? string.Empty, $@"\[&_svg\]:{dim}-(?<n>[0-9.]+)");
        return m.Success ? double.Parse(m.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture) * 4.0 : null;
    }

    [Fact]
    public void Rating_Star_Size_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Rating>(p => p.Add(r => r.Size, size));
            return RatingStarSizePx(cut.Find("button").GetAttribute("class"), "h");
        }, "Rating star h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Rating star h");
    }

    // ============================== ReasoningDisplay ==============================

    [Fact]
    public void ReasoningDisplay_Chevron_Icon_Size_Is_Monotonic()
    {
        // Regression guard for the wrapper-vs-inner sweep fix: the chevron used
        // to hardcode Size="Sm" regardless of the component's own Size.
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.ReasoningDisplay>(p => p.Add(r => r.Size, size));
            return SizeScaleAssert.SpacingPx(cut.Find("summary svg").GetAttribute("class"), "h");
        }, "ReasoningDisplay chevron h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "ReasoningDisplay chevron h");
    }

    [Fact]
    public void ReasoningDisplay_Summary_Gap_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.ReasoningDisplay>(p => p.Add(r => r.Size, size));
            return SizeScaleAssert.SpacingPx(cut.Find("summary").GetAttribute("class"), "gap");
        }, "ReasoningDisplay summary gap");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "ReasoningDisplay summary gap");
    }

    [Fact]
    public void ReasoningDisplay_Summary_Text_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.ReasoningDisplay>(p => p.Add(r => r.Size, size));
            return SizeScaleAssert.TextSizePx(cut.Find("summary").GetAttribute("class"));
        }, "ReasoningDisplay summary text");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "ReasoningDisplay summary text");
    }

    [Fact]
    public void ReasoningDisplay_Body_Margin_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.ReasoningDisplay>(p => p.Add(r => r.Size, size).Add(r => r.Text, "step"));
            return SizeScaleAssert.SpacingPx(cut.Find("div.leading-relaxed").GetAttribute("class"), "mt");
        }, "ReasoningDisplay body mt");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "ReasoningDisplay body mt");
    }

    [Fact]
    public void ReasoningDisplay_Body_VerticalPadding_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.ReasoningDisplay>(p => p.Add(r => r.Size, size).Add(r => r.Text, "step"));
            return SizeScaleAssert.SpacingPx(cut.Find("div.leading-relaxed").GetAttribute("class"), "py");
        }, "ReasoningDisplay body py");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "ReasoningDisplay body py");
    }

    [Fact]
    public void ReasoningDisplay_Body_VerticalPadding_Xxs_And_Xs_Tie_At_Py0()
    {
        var xxs = _ctx.Render<L.ReasoningDisplay>(p => p.Add(r => r.Size, L.Size.Xxs).Add(r => r.Text, "x"));
        var xs = _ctx.Render<L.ReasoningDisplay>(p => p.Add(r => r.Size, L.Size.Xs).Add(r => r.Text, "x"));
        Assert.Equal(
            SizeScaleAssert.SpacingPx(xxs.Find("div.leading-relaxed").GetAttribute("class"), "py"),
            SizeScaleAssert.SpacingPx(xs.Find("div.leading-relaxed").GetAttribute("class"), "py"));
    }

    [Fact]
    public void ReasoningDisplay_Body_Text_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.ReasoningDisplay>(p => p.Add(r => r.Size, size).Add(r => r.Text, "step"));
            return SizeScaleAssert.TextSizePx(cut.Find("div.leading-relaxed").GetAttribute("class"));
        }, "ReasoningDisplay body text");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "ReasoningDisplay body text");
    }

    // ============================== Result ==============================

    [Fact]
    public void Result_Padding_Py_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Result>(p => p.Add(r => r.Size, size).Add(r => r.Title, "Done"));
            return SizeScaleAssert.SpacingPx(cut.Find("div").GetAttribute("class"), "py");
        }, "Result padding py");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Result padding py");
    }

    [Fact]
    public void Result_Icon_Badge_Size_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Result>(p => p.Add(r => r.Size, size).Add(r => r.Title, "Done"));
            var badge = cut.Find("svg").ParentElement!;
            return SizeScaleAssert.SpacingPx(badge.GetAttribute("class"), "h");
        }, "Result icon badge h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Result icon badge h");
    }

    [Fact]
    public void Result_Inner_Glyph_Size_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Result>(p => p.Add(r => r.Size, size).Add(r => r.Title, "Done"));
            return SizeScaleAssert.SpacingPx(cut.Find("svg").GetAttribute("class"), "h");
        }, "Result inner glyph h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Result inner glyph h");
    }

    [Fact]
    public void Result_Title_Text_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Result>(p => p.Add(r => r.Size, size).Add(r => r.Title, "Done"));
            return SizeScaleAssert.TextSizePx(cut.Find("h3").GetAttribute("class"));
        }, "Result title text");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Result title text");
    }

    [Fact]
    public void Result_SubTitle_Text_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Result>(p => p.Add(r => r.Size, size).Add(r => r.SubTitle, "Details"));
            return SizeScaleAssert.TextSizePx(cut.Find("p").GetAttribute("class"));
        }, "Result subtitle text");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Result subtitle text");
    }

    // ============================== SparkCard ==============================

    private static readonly double[] TwoPoints = [1, 2];

    [Fact]
    public void SparkCard_SparkHeight_Is_Monotonic()
    {
        // SparkHeight is a plain int (forwarded to the child Sparkline's viewBox),
        // no class parsing needed — the cleanest dimension in this whole file.
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.SparkCard>(p => p.Add(s => s.Size, size).Add(s => s.Data, TwoPoints));
            var viewBox = cut.Find("svg").GetAttribute("viewBox");
            var parts = viewBox!.Split(' ');
            return double.Parse(parts[3], CultureInfo.InvariantCulture);
        }, "SparkCard SparkHeight");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "SparkCard SparkHeight");
    }

    [Fact]
    public void SparkCard_Padding_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.SparkCard>(p => p.Add(s => s.Size, size));
            return SizeScaleAssert.SpacingPx(cut.Find("div").GetAttribute("class"), "p");
        }, "SparkCard padding");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "SparkCard padding");
    }

    [Fact]
    public void SparkCard_Label_Text_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.SparkCard>(p => p.Add(s => s.Size, size).Add(s => s.Label, "Revenue"));
            return SizeScaleAssert.TextSizePx(cut.Find("div.truncate").GetAttribute("class"));
        }, "SparkCard label text");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "SparkCard label text");
    }

    [Fact]
    public void SparkCard_Value_Text_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.SparkCard>(p => p.Add(s => s.Size, size).Add(s => s.Value, "1,024"));
            return SizeScaleAssert.TextSizePx(cut.Find("div.font-semibold").GetAttribute("class"));
        }, "SparkCard value text");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "SparkCard value text");
    }

    [Fact]
    public void SparkCard_Value_Margin_Xxs_And_Xs_Tie_At_Mt0()
    {
        var xxs = _ctx.Render<L.SparkCard>(p => p.Add(s => s.Size, L.Size.Xxs).Add(s => s.Value, "1"));
        var xs = _ctx.Render<L.SparkCard>(p => p.Add(s => s.Size, L.Size.Xs).Add(s => s.Value, "1"));
        Assert.Equal(
            SizeScaleAssert.SpacingPx(xxs.Find("div.font-semibold").GetAttribute("class"), "mt"),
            SizeScaleAssert.SpacingPx(xs.Find("div.font-semibold").GetAttribute("class"), "mt"));
    }

    // ============================== Spinner ==============================

    [Fact]
    public void Spinner_Ring_Size_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Spinner>(p => p.Add(s => s.Size, size));
            return SizeScaleAssert.SpacingPx(cut.Find("svg").GetAttribute("class"), "h");
        }, "Spinner ring h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Spinner ring h");
    }

    [Fact]
    public void Spinner_Dot_Gap_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Spinner>(p => p.Add(s => s.Size, size).Add(s => s.Variant, L.Spinner.SpinnerVariant.Dots));
            return SizeScaleAssert.SpacingPx(cut.Find("div[aria-hidden='true']").GetAttribute("class"), "gap");
        }, "Spinner dot gap");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Spinner dot gap");
    }

    [Fact]
    public void Spinner_Dot_Gap_Xxs_And_Xs_Tie_At_Gap0()
    {
        var xxs = _ctx.Render<L.Spinner>(p => p.Add(s => s.Size, L.Size.Xxs).Add(s => s.Variant, L.Spinner.SpinnerVariant.Dots));
        var xs = _ctx.Render<L.Spinner>(p => p.Add(s => s.Size, L.Size.Xs).Add(s => s.Variant, L.Spinner.SpinnerVariant.Dots));
        Assert.Equal(
            SizeScaleAssert.SpacingPx(xxs.Find("div[aria-hidden='true']").GetAttribute("class"), "gap"),
            SizeScaleAssert.SpacingPx(xs.Find("div[aria-hidden='true']").GetAttribute("class"), "gap"));
    }

    [Fact]
    public void Spinner_Dot_Diameter_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Spinner>(p => p.Add(s => s.Size, size).Add(s => s.Variant, L.Spinner.SpinnerVariant.Dots));
            var dot = cut.FindAll("div.animate-bounce")[0];
            return SizeScaleAssert.SpacingPx(dot.GetAttribute("class"), "h");
        }, "Spinner dot h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Spinner dot h");
    }

    [Fact]
    public void Spinner_Bars_Container_Height_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Spinner>(p => p.Add(s => s.Size, size).Add(s => s.Variant, L.Spinner.SpinnerVariant.Bars));
            return SizeScaleAssert.SpacingPx(cut.Find("div[aria-hidden='true']").GetAttribute("class"), "h");
        }, "Spinner bars container h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Spinner bars container h");
    }

    [Fact]
    public void Spinner_Bar_Width_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Spinner>(p => p.Add(s => s.Size, size).Add(s => s.Variant, L.Spinner.SpinnerVariant.Bars));
            var bar = cut.FindAll("div.animate-pulse")[0];
            return SizeScaleAssert.SpacingPx(bar.GetAttribute("class"), "w");
        }, "Spinner bar w");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Spinner bar w");
    }

    [Fact]
    public void Spinner_Bar_Width_Xxs_Xs_Sm_Three_Way_Tie()
    {
        double? WidthAt(L.Size size)
        {
            var cut = _ctx.Render<L.Spinner>(p => p.Add(s => s.Size, size).Add(s => s.Variant, L.Spinner.SpinnerVariant.Bars));
            var bar = cut.FindAll("div.animate-pulse")[0];
            return SizeScaleAssert.SpacingPx(bar.GetAttribute("class"), "w");
        }

        var xxs = WidthAt(L.Size.Xxs);
        var xs = WidthAt(L.Size.Xs);
        var sm = WidthAt(L.Size.Sm);
        Assert.Equal(xxs, xs);
        Assert.Equal(xs, sm);
    }

    // ============================== Statistic ==============================

    [Fact]
    public void Statistic_Title_Text_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Statistic>(p => p.Add(s => s.Size, size).Add(s => s.Title, "Revenue"));
            return SizeScaleAssert.TextSizePx(cut.Find("p").GetAttribute("class"));
        }, "Statistic title text");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Statistic title text");
    }

    [Fact]
    public void Statistic_Value_Text_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Statistic>(p => p.Add(s => s.Size, size).Add(s => s.Value, "42"));
            var span = cut.FindAll("span")[0];
            return SizeScaleAssert.TextSizePx(span.GetAttribute("class"));
        }, "Statistic value text");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Statistic value text");
    }

    [Fact]
    public void Statistic_Value_Text_Xxs_And_Xs_Tie_At_TextXs()
    {
        var xxs = _ctx.Render<L.Statistic>(p => p.Add(s => s.Size, L.Size.Xxs).Add(s => s.Value, "1"));
        var xs = _ctx.Render<L.Statistic>(p => p.Add(s => s.Size, L.Size.Xs).Add(s => s.Value, "1"));
        Assert.Equal(
            SizeScaleAssert.TextSizePx(xxs.FindAll("span")[0].GetAttribute("class")),
            SizeScaleAssert.TextSizePx(xs.FindAll("span")[0].GetAttribute("class")));
    }

    [Fact]
    public void Statistic_Suffix_Text_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Statistic>(p => p
                .Add(s => s.Size, size)
                .Add(s => s.Value, "42")
                .Add(s => s.Suffix, (RenderFragment)(b => b.AddContent(0, "kg"))));
            var suffixSpan = cut.FindAll("span").Last(sp => sp.TextContent == "kg");
            return SizeScaleAssert.TextSizePx(suffixSpan.GetAttribute("class"));
        }, "Statistic suffix text");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Statistic suffix text");
    }

    // ============================== Switch ==============================

    [Fact]
    public void Switch_Track_Height_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Switch>(p => p.Add(s => s.Size, size));
            return SizeScaleAssert.SpacingPx(cut.Find("button").GetAttribute("class"), "h");
        }, "Switch track h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Switch track h");
    }

    [Fact]
    public void Switch_Thumb_Height_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Switch>(p => p.Add(s => s.Size, size));
            return SizeScaleAssert.SpacingPx(cut.Find("span").GetAttribute("class"), "h");
        }, "Switch thumb h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Switch thumb h");
    }

    [Fact]
    public void Switch_Loading_Spinner_Ring_Size_Is_Monotonic()
    {
        // Regression guard for finding 1: the ring used to be a fixed
        // Size="Sm" (h-4 w-4) at every rung, invisible to a per-rung-only test
        // because each rung individually "passed" against the wrong constant.
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Switch>(p => p.Add(s => s.Size, size).Add(s => s.Loading, true));
            var ring = cut.Find("button [role=\"status\"] svg");
            return SizeScaleAssert.SpacingPx(ring.GetAttribute("class"), "h");
        }, "Switch loading spinner ring h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Switch loading spinner ring h");
    }

    // ============================== Toggle ==============================

    [Fact]
    public void Toggle_Height_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Toggle>(p => p.Add(t => t.Size, size).AddChildContent("B"));
            return SizeScaleAssert.SpacingPx(cut.Find("button").GetAttribute("class"), "h");
        }, "Toggle h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Toggle h");
    }

    [Fact]
    public void Toggle_Padding_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = _ctx.Render<L.Toggle>(p => p.Add(t => t.Size, size).AddChildContent("B"));
            return SizeScaleAssert.SpacingPx(cut.Find("button").GetAttribute("class"), "px");
        }, "Toggle px");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "Toggle px");
    }

    // ============================== ToggleGroupItem ==============================

    private IRenderedComponent<IComponent> RenderToggleGroup(L.Size size)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ToggleGroup>(0);
            builder.AddAttribute(1, "Type", L.ToggleGroup.ToggleGroupType.Single);
            builder.AddAttribute(2, "Size", size);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ToggleGroupItem>(0);
                b.AddAttribute(1, "Value", "a");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void ToggleGroupItem_Height_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = RenderToggleGroup(size);
            return SizeScaleAssert.SpacingPx(cut.Find("button").GetAttribute("class"), "h");
        }, "ToggleGroupItem h");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "ToggleGroupItem h");
    }

    [Fact]
    public void ToggleGroupItem_Padding_Is_Monotonic()
    {
        var series = Series(size =>
        {
            var cut = RenderToggleGroup(size);
            return SizeScaleAssert.SpacingPx(cut.Find("button").GetAttribute("class"), "px");
        }, "ToggleGroupItem px");
        SizeScaleAssert.AssertMonotonicNonDecreasing(series, "ToggleGroupItem px");
    }
}
