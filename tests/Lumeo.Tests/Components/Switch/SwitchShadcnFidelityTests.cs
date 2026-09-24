using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Switch;

/// <summary>
/// Pins the default (Md, Comfortable density) Switch geometry against verified shadcn
/// new-york v4 <c>switch.tsx</c> (fetched live from <c>shadcn-ui/ui</c>,
/// <c>apps/v4/registry/new-york-v4/ui/switch.tsx</c>):
///
/// <code>
/// data-[size=default]:h-[1.15rem] data-[size=default]:w-8 ... border border-transparent
/// group-data-[size=default]/switch:size-4 ...
/// data-[state=checked]:translate-x-[calc(100%-2px)] data-[state=unchecked]:translate-x-0
/// </code>
///
/// Lumeo expresses the size as a discrete <see cref="Lumeo.Size"/> rung (not a
/// <c>data-size</c> attribute) and dark mode via CSS-variable token swaps instead of
/// <c>dark:</c> classes (see <c>src/Lumeo/wwwroot/css/lumeo.css</c>), so this test
/// compares the RENDERED geometry, not the class string verbatim: track 18.4x32px
/// (h-[1.15rem] w-8), 1px border, thumb 16x16px (h-4 w-4, shadcn's size-4), checked
/// translate 14px (translate-x-3.5 == shadcn's calc(100%-2px) of a 16px thumb).
/// </summary>
public class SwitchShadcnFidelityTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SwitchShadcnFidelityTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static string[] Tokens(string? cls) =>
        (cls ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Default_Md_Track_Matches_Shadcn_Track_Size_Exactly()
    {
        // shadcn: data-[size=default]:h-[1.15rem] data-[size=default]:w-8 = 18.4x32px.
        var cut = _ctx.Render<Lumeo.Switch>();
        var track = Tokens(cut.Find("button").GetAttribute("class"));

        Assert.Contains("h-[1.15rem]", track);
        Assert.Contains("w-8", track);
    }

    [Fact]
    public void Default_Md_Track_Has_A_1px_Border_Not_2px()
    {
        // shadcn: `border border-transparent` (1px). Lumeo used to render `border-2`
        // (2px, the old pre-v4 shadcn/Radix convention) — asserting the exact bare
        // token so a future `border-2`/`border-4` regression fails loudly, and that no
        // stray `border-2` token survives anywhere in the class list.
        var cut = _ctx.Render<Lumeo.Switch>();
        var track = Tokens(cut.Find("button").GetAttribute("class"));

        Assert.Contains("border", track);
        Assert.Contains("border-transparent", track);
        Assert.DoesNotContain(track, t => t is "border-2" or "border-4");
    }

    [Fact]
    public void Default_Md_Thumb_Matches_Shadcn_Thumb_Size_Exactly()
    {
        // shadcn: group-data-[size=default]/switch:size-4 = 16x16px == Lumeo's h-4 w-4.
        var cut = _ctx.Render<Lumeo.Switch>();
        var thumb = Tokens(cut.FindAll("button span")
            .First(s => s.ClassList.Contains("transition-transform"))
            .GetAttribute("class"));

        Assert.Contains("h-4", thumb);
        Assert.Contains("w-4", thumb);
    }

    [Fact]
    public void Default_Md_Checked_Translate_Matches_Shadcns_Calc_Exactly()
    {
        // shadcn: data-[state=checked]:translate-x-[calc(100%-2px)] — 100% of the 16px
        // thumb minus the 2px border inset = 14px = Tailwind's translate-x-3.5.
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(b => b.Checked, true));
        var thumb = Tokens(cut.Find("span").GetAttribute("class"));

        Assert.Contains("translate-x-3.5", thumb);
    }

    [Fact]
    public void Default_Md_Unchecked_Translate_Matches_Shadcn_Exactly()
    {
        // shadcn: data-[state=unchecked]:translate-x-0.
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(b => b.Checked, false));
        var thumb = Tokens(cut.Find("span").GetAttribute("class"));

        Assert.Contains("translate-x-0", thumb);
    }

    [Fact]
    public void Default_Md_Uses_Bg_Primary_When_Checked_And_Bg_Input_When_Unchecked()
    {
        // shadcn: data-[state=checked]:bg-primary data-[state=unchecked]:bg-input
        // (dark:data-[state=unchecked]:bg-input/80 — Lumeo has no dark: prefixes; dark
        // mode is a CSS-variable swap on the same bg-input token in lumeo.css).
        var unchecked_ = Tokens(_ctx.Render<Lumeo.Switch>(p => p.Add(b => b.Checked, false))
            .Find("button").GetAttribute("class"));
        var checked_ = Tokens(_ctx.Render<Lumeo.Switch>(p => p.Add(b => b.Checked, true))
            .Find("button").GetAttribute("class"));

        Assert.Contains("bg-input", unchecked_);
        Assert.DoesNotContain("bg-primary", unchecked_);
        Assert.Contains("bg-primary", checked_);
        Assert.DoesNotContain("bg-input", checked_);
    }

    [Fact]
    public void Default_Md_Disabled_Renders_Opacity_50_Matching_Shadcn()
    {
        // shadcn: disabled:cursor-not-allowed disabled:opacity-50.
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(b => b.Disabled, true));
        var track = Tokens(cut.Find("button").GetAttribute("class"));

        Assert.Contains("disabled:cursor-not-allowed", track);
        Assert.Contains("disabled:opacity-50", track);
    }
}
