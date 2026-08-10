using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Result;

// Pins all 7 Lumeo.Size rungs (Xxs, Xs, Sm, Md, Lg, Xl, Xxl) for Result's
// container padding, icon badge, inner glyph, title and subtitle typography.
// Asserts the RENDERED class on the live DOM node, never the source switch string.
//
// IMPORTANT: assertions use exact space-delimited token matching (AssertHasClass),
// never Assert.Contains(substring, cls) — Tailwind's own scale defeats naive
// substring checks (e.g. "h-1" is a substring of "h-10"/"h-12"/"h-14"/"h-16").
public class ResultSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ResultSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void AssertHasClass(string? cls, string token)
    {
        var tokens = (cls ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(token, tokens);
    }

    [Theory]
    [InlineData(L.Size.Xxs, "py-0")]
    [InlineData(L.Size.Xs, "py-4")]
    [InlineData(L.Size.Sm, "py-8")]
    [InlineData(L.Size.Md, "py-12")]
    [InlineData(L.Size.Lg, "py-16")]
    [InlineData(L.Size.Xl, "py-20")]
    [InlineData(L.Size.Xxl, "py-24")]
    public void Container_Vertical_Padding_Per_Rung(L.Size size, string pyClass)
    {
        var cut = _ctx.Render<L.Result>(p => p
            .Add(r => r.Size, size)
            .Add(r => r.Title, "Done"));

        var cls = cut.Find("div").GetAttribute("class");
        AssertHasClass(cls, pyClass);
        AssertHasClass(cls, "px-6");
    }

    [Theory]
    [InlineData(L.Size.Xxs, "h-4", "w-4")]
    [InlineData(L.Size.Xs, "h-8", "w-8")]
    [InlineData(L.Size.Sm, "h-12", "w-12")]
    [InlineData(L.Size.Md, "h-16", "w-16")]
    [InlineData(L.Size.Lg, "h-20", "w-20")]
    [InlineData(L.Size.Xl, "h-24", "w-24")]
    [InlineData(L.Size.Xxl, "h-28", "w-28")]
    public void Icon_Badge_Size_Per_Rung(L.Size size, string h, string w)
    {
        var cut = _ctx.Render<L.Result>(p => p
            .Add(r => r.Size, size)
            .Add(r => r.Title, "Done"));

        // Badge container is the div wrapping the default icon svg.
        var badge = cut.Find("svg").ParentElement!;
        var cls = badge.GetAttribute("class");
        AssertHasClass(cls, h);
        AssertHasClass(cls, w);
    }

    [Theory]
    [InlineData(L.Size.Xxs, "h-2", "w-2")]
    [InlineData(L.Size.Xs, "h-4", "w-4")]
    [InlineData(L.Size.Sm, "h-6", "w-6")]
    [InlineData(L.Size.Md, "h-8", "w-8")]
    [InlineData(L.Size.Lg, "h-10", "w-10")]
    [InlineData(L.Size.Xl, "h-12", "w-12")]
    [InlineData(L.Size.Xxl, "h-14", "w-14")]
    public void Inner_Glyph_Size_Per_Rung(L.Size size, string h, string w)
    {
        var cut = _ctx.Render<L.Result>(p => p
            .Add(r => r.Size, size)
            .Add(r => r.Title, "Done"));

        var cls = cut.Find("svg").GetAttribute("class");
        AssertHasClass(cls, h);
        AssertHasClass(cls, w);
    }

    [Theory]
    [InlineData(L.Size.Xxs, "text-sm")]
    [InlineData(L.Size.Xs, "text-base")]
    [InlineData(L.Size.Sm, "text-lg")]
    [InlineData(L.Size.Md, "text-xl")]
    [InlineData(L.Size.Lg, "text-2xl")]
    [InlineData(L.Size.Xl, "text-3xl")]
    [InlineData(L.Size.Xxl, "text-4xl")]
    public void Title_Text_Size_Per_Rung(L.Size size, string textClass)
    {
        var cut = _ctx.Render<L.Result>(p => p
            .Add(r => r.Size, size)
            .Add(r => r.Title, "Done"));

        var cls = cut.Find("h3").GetAttribute("class");
        AssertHasClass(cls, textClass);
    }

    [Theory]
    [InlineData(L.Size.Xxs, "text-[8px]")]
    [InlineData(L.Size.Xs, "text-[10px]")]
    [InlineData(L.Size.Sm, "text-xs")]
    [InlineData(L.Size.Md, "text-sm")]
    [InlineData(L.Size.Lg, "text-base")]
    [InlineData(L.Size.Xl, "text-lg")]
    [InlineData(L.Size.Xxl, "text-xl")]
    public void SubTitle_Text_Size_Per_Rung(L.Size size, string textClass)
    {
        var cut = _ctx.Render<L.Result>(p => p
            .Add(r => r.Size, size)
            .Add(r => r.SubTitle, "Details"));

        var cls = cut.Find("p").GetAttribute("class");
        AssertHasClass(cls, textClass);
    }

    [Fact]
    public void Existing_Sm_Md_Lg_Rungs_Unchanged()
    {
        var sm = _ctx.Render<L.Result>(p => p.Add(r => r.Size, L.Size.Sm).Add(r => r.Title, "T").Add(r => r.SubTitle, "S"));
        var md = _ctx.Render<L.Result>(p => p.Add(r => r.Size, L.Size.Md).Add(r => r.Title, "T").Add(r => r.SubTitle, "S"));
        var lg = _ctx.Render<L.Result>(p => p.Add(r => r.Size, L.Size.Lg).Add(r => r.Title, "T").Add(r => r.SubTitle, "S"));

        AssertHasClass(sm.Find("div").GetAttribute("class"), "py-8");
        AssertHasClass(md.Find("div").GetAttribute("class"), "py-12");
        AssertHasClass(lg.Find("div").GetAttribute("class"), "py-16");

        AssertHasClass(sm.Find("h3").GetAttribute("class"), "text-lg");
        AssertHasClass(md.Find("h3").GetAttribute("class"), "text-xl");
        AssertHasClass(lg.Find("h3").GetAttribute("class"), "text-2xl");
    }
}
