using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.List;

// Pins all 7 Lumeo.Size rungs (Xxs, Xs, Sm, Md, Lg, Xl, Xxl) for ListItem's row
// padding, cascaded from List.Size via ListContext. Padding numbers are
// deliberately identical to Alert's (see spec §1/§8) — kept in lockstep.
// Asserts the RENDERED class on the live <li>, never the source switch string.
//
// IMPORTANT: assertions use exact space-delimited token matching (AssertHasClass),
// never Assert.Contains(substring, cls) — Tailwind's own scale defeats naive
// substring checks (e.g. "px-2" is a substring of "px-2.5"). Caught live during
// this batch's disable-check: with Assert.Contains, a deliberately-disabled
// (Xs, Compact) rung fell back to (Xs, _) => "px-2.5 py-1.5" and the substring
// check on expected "px-2"/"py-1" still spuriously passed against "px-2.5"/"py-1.5".
public class ListSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ListSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void AssertHasClass(string? cls, string token)
    {
        var tokens = (cls ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(token, tokens);
    }

    [Theory]
    [InlineData(L.Size.Xxs, "px-2", "py-1")]
    [InlineData(L.Size.Xs, "px-2.5", "py-1.5")]
    [InlineData(L.Size.Sm, "px-3", "py-2")]
    [InlineData(L.Size.Md, "px-4", "py-3")]
    [InlineData(L.Size.Lg, "px-5", "py-4")]
    [InlineData(L.Size.Xl, "px-6", "py-5")]
    [InlineData(L.Size.Xxl, "px-7", "py-6")]
    public void Comfortable_Density_Row_Padding_Per_Rung(L.Size size, string px, string py)
    {
        var cut = _ctx.Render<L.List>(p => p
            .Add(l => l.Size, size)
            .AddChildContent<L.ListItem>(item => item.Add(i => i.Title, "Row")));

        var cls = cut.Find("li").GetAttribute("class");
        AssertHasClass(cls, px);
        AssertHasClass(cls, py);
    }

    [Theory]
    [InlineData(L.Size.Xxs, "px-1.5", "py-0.5")]
    [InlineData(L.Size.Xs, "px-2", "py-1")]
    [InlineData(L.Size.Sm, "px-2.5", "py-1.5")]
    [InlineData(L.Size.Md, "px-3", "py-2")]
    [InlineData(L.Size.Lg, "px-4", "py-3")]
    [InlineData(L.Size.Xl, "px-5", "py-4")]
    [InlineData(L.Size.Xxl, "px-6", "py-5")]
    public void Compact_Density_Row_Padding_Per_Rung(L.Size size, string px, string py)
    {
        var cut = _ctx.Render<CascadingDensityHost>(p => p
            .Add(h => h.Density, L.Density.Compact)
            .Add(h => h.Size, size));

        var cls = cut.Find("li").GetAttribute("class");
        AssertHasClass(cls, px);
        AssertHasClass(cls, py);
    }

    [Theory]
    [InlineData(L.Size.Xxs, "px-3", "py-2")]
    [InlineData(L.Size.Xs, "px-3.5", "py-2.5")]
    [InlineData(L.Size.Sm, "px-4", "py-3")]
    [InlineData(L.Size.Md, "px-5", "py-4")]
    [InlineData(L.Size.Lg, "px-6", "py-5")]
    [InlineData(L.Size.Xl, "px-7", "py-6")]
    [InlineData(L.Size.Xxl, "px-8", "py-7")]
    public void Spacious_Density_Row_Padding_Per_Rung(L.Size size, string px, string py)
    {
        var cut = _ctx.Render<CascadingDensityHost>(p => p
            .Add(h => h.Density, L.Density.Spacious)
            .Add(h => h.Size, size));

        var cls = cut.Find("li").GetAttribute("class");
        AssertHasClass(cls, px);
        AssertHasClass(cls, py);
    }

    [Fact]
    public void Existing_Sm_Md_Lg_Rungs_Unchanged_At_Comfortable_Density()
    {
        var sm = _ctx.Render<L.List>(p => p.Add(l => l.Size, L.Size.Sm).AddChildContent<L.ListItem>(i => i.Add(x => x.Title, "r")));
        var md = _ctx.Render<L.List>(p => p.Add(l => l.Size, L.Size.Md).AddChildContent<L.ListItem>(i => i.Add(x => x.Title, "r")));
        var lg = _ctx.Render<L.List>(p => p.Add(l => l.Size, L.Size.Lg).AddChildContent<L.ListItem>(i => i.Add(x => x.Title, "r")));

        AssertHasClass(sm.Find("li").GetAttribute("class"), "px-3");
        AssertHasClass(sm.Find("li").GetAttribute("class"), "py-2");
        AssertHasClass(md.Find("li").GetAttribute("class"), "px-4");
        AssertHasClass(md.Find("li").GetAttribute("class"), "py-3");
        AssertHasClass(lg.Find("li").GetAttribute("class"), "px-5");
        AssertHasClass(lg.Find("li").GetAttribute("class"), "py-4");
    }

    // Helper host: cascades an explicit Density down to a List/ListItem pair so
    // the Compact/Spacious-density theories above can drive both Size and Density
    // together (List/ListItem alone only cascade Size, not an ambient Density).
    private sealed class CascadingDensityHost : Microsoft.AspNetCore.Components.ComponentBase
    {
        [Microsoft.AspNetCore.Components.Parameter] public L.Density Density { get; set; }
        [Microsoft.AspNetCore.Components.Parameter] public L.Size Size { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<Microsoft.AspNetCore.Components.CascadingValue<L.Density>>(0);
            builder.AddAttribute(1, "Value", Density);
            builder.AddAttribute(2, "ChildContent", (Microsoft.AspNetCore.Components.RenderFragment)(inner =>
            {
                inner.OpenComponent<L.List>(0);
                inner.AddAttribute(1, "Size", Size);
                inner.AddAttribute(2, "ChildContent", (Microsoft.AspNetCore.Components.RenderFragment)(itemBuilder =>
                {
                    itemBuilder.OpenComponent<L.ListItem>(0);
                    itemBuilder.AddAttribute(1, "Title", "Row");
                    itemBuilder.CloseComponent();
                }));
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }
}
