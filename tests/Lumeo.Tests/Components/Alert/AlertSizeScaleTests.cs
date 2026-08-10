using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Alert;

// Pins all 7 Lumeo.Size rungs (Xxs, Xs, Sm, Md, Lg, Xl, Xxl) for Alert's padding,
// icon size and title/description typography. Asserts the RENDERED class on the
// live DOM node (never the source switch-expression string) — Cx.Merge's argument
// order can silently discard a size class merged before the base class.
//
// IMPORTANT: assertions use exact space-delimited token matching (AssertHasClass),
// never Assert.Contains(substring, cls) — Tailwind's own scale defeats naive
// substring checks (e.g. "px-2" is a substring of "px-2.5"), which was caught live
// during this batch's required disable-check: a deliberately-broken rung produced
// a false PASS under Assert.Contains before this helper was introduced.
public class AlertSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AlertSizeScaleTests() => _ctx.AddLumeoServices();
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
    public void Comfortable_Density_Padding_Per_Rung(L.Size size, string px, string py)
    {
        var cut = _ctx.Render<L.Alert>(p => p
            .Add(a => a.Size, size)
            .AddChildContent("msg"));

        var cls = cut.Find("[role='alert']").GetAttribute("class");
        AssertHasClass(cls, px);
        AssertHasClass(cls, py);
    }

    [Theory]
    [InlineData(L.Size.Xxs, "h-2.5", "w-2.5")]
    [InlineData(L.Size.Xs, "h-3", "w-3")]
    [InlineData(L.Size.Sm, "h-3.5", "w-3.5")]
    [InlineData(L.Size.Md, "h-4", "w-4")]
    [InlineData(L.Size.Lg, "h-5", "w-5")]
    [InlineData(L.Size.Xl, "h-6", "w-6")]
    [InlineData(L.Size.Xxl, "h-7", "w-7")]
    public void Icon_Size_Per_Rung(L.Size size, string h, string w)
    {
        var cut = _ctx.Render<L.Alert>(p => p
            .Add(a => a.Size, size)
            .AddChildContent("msg"));

        var cls = cut.Find("svg").GetAttribute("class");
        AssertHasClass(cls, h);
        AssertHasClass(cls, w);
    }

    [Theory]
    [InlineData(L.Size.Xxs, "text-[8px]")]
    [InlineData(L.Size.Xs, "text-[10px]")]
    [InlineData(L.Size.Sm, "text-xs")]
    [InlineData(L.Size.Lg, "text-base")]
    [InlineData(L.Size.Xl, "text-lg")]
    [InlineData(L.Size.Xxl, "text-xl")]
    public void Title_Text_Size_Per_Rung(L.Size size, string textClass)
    {
        var cut = _ctx.Render<L.Alert>(p => p
            .Add(a => a.Size, size)
            .Add(a => a.Title, "Heads up"));

        var cls = cut.Find("p").GetAttribute("class");
        AssertHasClass(cls, textClass);
    }

    [Fact]
    public void Title_Md_Has_No_Explicit_Text_Size_Class_Inherited_From_Box()
    {
        // Md's TitleClass has no text-* token — verbatim preserved rung, size comes
        // from the alert box's own text-sm.
        var cut = _ctx.Render<L.Alert>(p => p
            .Add(a => a.Size, L.Size.Md)
            .Add(a => a.Title, "Heads up"));

        var cls = cut.Find("p").GetAttribute("class");
        AssertHasClass(cls, "mb-1");
        Assert.DoesNotContain("text-xs", cls);
        Assert.DoesNotContain("text-base", cls);
    }

    [Theory]
    [InlineData(L.Size.Xxs, "text-[8px]")]
    [InlineData(L.Size.Xs, "text-[10px]")]
    [InlineData(L.Size.Sm, "text-xs")]
    [InlineData(L.Size.Md, "text-sm")]
    [InlineData(L.Size.Lg, "text-sm")]
    [InlineData(L.Size.Xl, "text-base")]
    [InlineData(L.Size.Xxl, "text-lg")]
    public void Description_Text_Size_Per_Rung(L.Size size, string textClass)
    {
        var cut = _ctx.Render<L.Alert>(p => p
            .Add(a => a.Size, size)
            .Add(a => a.Description, "Body copy"));

        var cls = cut.Find("div.flex-1 > div").GetAttribute("class");
        AssertHasClass(cls, textClass);
    }

    // Scale-ordering test finding: Description ties Md and Lg at text-sm/14px,
    // unlike TitleClass which steps Lg up to text-base/16px. Confirmed via
    // `git show origin/master:...Alert.razor` that Lg="text-sm" is a PRE-EXISTING
    // rung (shipped before this PR/campaign; only Xxs/Xs/Xl/Xxl were added here),
    // so per the "never change an already-implemented rung" rule it stays as-is
    // rather than being bumped to match Title's progression. Documented explicitly
    // (not just tolerated by a >= check) so a future accidental change shows up
    // here. The full sequence (8/10/12/14/14/16/18px) is still monotonically
    // non-decreasing.
    [Fact]
    public void Description_Text_Size_Md_And_Lg_Deliberately_Tie_At_TextSm_PreExisting()
    {
        var md = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, L.Size.Md).Add(a => a.Description, "x"));
        var lg = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, L.Size.Lg).Add(a => a.Description, "x"));

        var mdCls = md.Find("div.flex-1 > div").GetAttribute("class");
        var lgCls = lg.Find("div.flex-1 > div").GetAttribute("class");

        AssertHasClass(mdCls, "text-sm");
        AssertHasClass(lgCls, "text-sm");
    }

    [Fact]
    public void Existing_Sm_Md_Lg_Rungs_Unchanged_At_Compact_Density()
    {
        // Verbatim regression guard for the three rungs that shipped before this
        // batch — Compact arm.
        var sm = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, L.Size.Sm).Add(a => a.Density, L.Density.Compact).AddChildContent("m"));
        var md = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, L.Size.Md).Add(a => a.Density, L.Density.Compact).AddChildContent("m"));
        var lg = _ctx.Render<L.Alert>(p => p.Add(a => a.Size, L.Size.Lg).Add(a => a.Density, L.Density.Compact).AddChildContent("m"));

        var smCls = sm.Find("[role='alert']").GetAttribute("class");
        var mdCls = md.Find("[role='alert']").GetAttribute("class");
        var lgCls = lg.Find("[role='alert']").GetAttribute("class");

        AssertHasClass(smCls, "px-2.5");
        AssertHasClass(smCls, "py-1.5");
        AssertHasClass(mdCls, "px-3");
        AssertHasClass(mdCls, "py-2");
        AssertHasClass(lgCls, "px-4");
        AssertHasClass(lgCls, "py-3");
    }
}
