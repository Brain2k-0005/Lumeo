using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tabs;

/// <summary>
/// TabsList's own track height didn't honour the ambient Density, even though
/// TabsTrigger.PadClass already does (see TabsTriggerPaddingTests) — at Compact the
/// trigger shrank inside a track still sized for Comfortable (visible extra slack); at
/// Spacious the trigger grew past the track's fixed height (visible overflow/clipping).
///
/// TrackHeightClass is derived from the SAME py values PadClass resolves for
/// (Variant, Orientation=Horizontal, Density) — see its own doc comment in
/// TabsList.razor for the full 2*py+20px arithmetic — so this suite pins the matching
/// container-side matrix: 4 variants x 3 densities (horizontal only; vertical tracks
/// are h-auto/w-auto, unbounded, never at risk — pinned separately below as staying
/// untouched). Same exact-token discipline as TabsTriggerPaddingTests: Assert.Contains
/// on tokens split from the class string, never on the raw string ("h-9" is a substring
/// of nothing else in this set, but the split discipline is kept uniform with the sibling
/// suite regardless).
/// </summary>
public class TabsListDensityHeightTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public TabsListDensityHeightTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderTabsList(
        L.Tabs.TabsVariant variant,
        L.Orientation orientation = L.Orientation.Horizontal,
        L.Density? density = null)
        => _ctx.Render(builder =>
        {
            void RenderTabs(RenderTreeBuilder scope)
            {
                scope.OpenComponent<L.Tabs>(0);
                scope.AddAttribute(1, "ActiveValue", "one");
                scope.AddAttribute(2, "Variant", variant);
                scope.AddAttribute(3, "Orientation", orientation);
                scope.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
                {
                    b.OpenComponent<L.TabsList>(0);
                    b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                    {
                        inner.OpenComponent<L.TabsTrigger>(0);
                        inner.AddAttribute(1, "Value", "one");
                        inner.AddAttribute(2, "ChildContent", (RenderFragment)(t => t.AddContent(0, "First")));
                        inner.CloseComponent();
                    }));
                    b.CloseComponent();
                }));
                scope.CloseComponent();
            }

            if (density.HasValue)
            {
                builder.OpenComponent<L.DensityScope>(0);
                builder.AddAttribute(1, "Value", density.Value);
                builder.AddAttribute(2, "ChildContent", (RenderFragment)(scope => RenderTabs(scope)));
                builder.CloseComponent();
            }
            else
            {
                RenderTabs(builder);
            }
        });

    private static void AssertExactHeight(string cls, string expectedHeight)
    {
        var tokens = cls.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(expectedHeight, tokens);
        // The three height tiers this switch ever emits ("h-9"/"h-10"/"h-11") must be
        // mutually exclusive on any single render — asserting the OTHER two are absent
        // is what actually catches a leaked extra token a plain Contains would miss
        // (e.g. "h-10" is not a substring of "h-9", so a naive check could pass with
        // BOTH present).
        foreach (var other in new[] { "h-9", "h-10", "h-11" })
        {
            if (other == expectedHeight) continue;
            Assert.DoesNotContain(other, tokens);
        }
    }

    public static IEnumerable<object[]> HorizontalMatrix()
    {
        foreach (var variant in new[] { L.Tabs.TabsVariant.Default, L.Tabs.TabsVariant.Card, L.Tabs.TabsVariant.Pill, L.Tabs.TabsVariant.Underline })
        foreach (var density in new[] { L.Density.Compact, L.Density.Comfortable, L.Density.Spacious })
        {
            var expected = (variant, density) switch
            {
                (_, L.Density.Spacious) => "h-11",
                (_, L.Density.Compact) => "h-9",
                (L.Tabs.TabsVariant.Default, _) => "h-9",
                _ => "h-10",
            };
            yield return new object[] { variant, density, expected };
        }
    }

    [Theory]
    [MemberData(nameof(HorizontalMatrix))]
    public void TrackHeight_Matches_Variant_Density_Matrix(L.Tabs.TabsVariant variant, L.Density density, string expectedHeight)
    {
        var cut = RenderTabsList(variant, L.Orientation.Horizontal, density);
        var cls = cut.Find("[role='tablist']").GetAttribute("class") ?? "";
        AssertExactHeight(cls, expectedHeight);
    }

    [Fact]
    public void No_DensityScope_Default_Variant_Renders_H9_Unchanged()
    {
        // A consumer who never touches DensityScope must get exactly today's rendering:
        // Default variant's track was always h-9 before this fix.
        var cut = RenderTabsList(L.Tabs.TabsVariant.Default);
        var cls = cut.Find("[role='tablist']").GetAttribute("class") ?? "";
        AssertExactHeight(cls, "h-9");
    }

    [Theory]
    [InlineData(L.Tabs.TabsVariant.Card)]
    [InlineData(L.Tabs.TabsVariant.Pill)]
    [InlineData(L.Tabs.TabsVariant.Underline)]
    public void No_DensityScope_Non_Default_Variant_Renders_H10_Unchanged(L.Tabs.TabsVariant variant)
    {
        var cut = RenderTabsList(variant);
        var cls = cut.Find("[role='tablist']").GetAttribute("class") ?? "";
        AssertExactHeight(cls, "h-10");
    }

    // --- Vertical tracks are h-auto/w-auto and stay untouched by Density ---

    [Theory]
    [InlineData(L.Tabs.TabsVariant.Default)]
    [InlineData(L.Tabs.TabsVariant.Card)]
    [InlineData(L.Tabs.TabsVariant.Pill)]
    [InlineData(L.Tabs.TabsVariant.Underline)]
    public void Vertical_Orientation_Never_Renders_A_Fixed_Height_Token_At_Any_Density(L.Tabs.TabsVariant variant)
    {
        foreach (var density in new[] { L.Density.Compact, L.Density.Comfortable, L.Density.Spacious })
        {
            var cut = RenderTabsList(variant, L.Orientation.Vertical, density);
            var tokens = (cut.Find("[role='tablist']").GetAttribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.Contains("h-auto", tokens);
            foreach (var fixedHeight in new[] { "h-9", "h-10", "h-11" })
                Assert.DoesNotContain(fixedHeight, tokens);
        }
    }

    // --- Disable check: predicted-vs-actual against the pre-fix (hardcoded) behavior ---

    [Fact]
    public void Predicted_Vs_Actual_Compact_Card_Would_Wrongly_Stay_H10_Under_The_Old_Hardcoded_Height()
    {
        // Before this fix, TabsList.CssClass hardcoded "h-10" for Card/Pill/Underline
        // (horizontal) regardless of Density — see the pre-fix baseClasses switch. The
        // PREDICTED WRONG value at Compact under that old code is "h-10" (unchanged from
        // Comfortable); the fix's correct value is "h-9" (matching the Compact trigger's
        // now-smaller 28px content-box height). Pinning the predicted-wrong value here
        // as an explicit negative assertion, alongside the positive "h-9" assertion,
        // documents exactly what a regression back to the hardcoded class would look like.
        var cut = RenderTabsList(L.Tabs.TabsVariant.Card, L.Orientation.Horizontal, L.Density.Compact);
        var tokens = (cut.Find("[role='tablist']").GetAttribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains("h-9", tokens);       // actual (fixed) value
        Assert.DoesNotContain("h-10", tokens); // predicted value under the pre-fix hardcoded class
    }

    [Fact]
    public void Predicted_Vs_Actual_Spacious_Default_Would_Wrongly_Stay_H9_Under_The_Old_Hardcoded_Height()
    {
        // Mirror case for the Default variant: pre-fix TabsList.CssClass hardcoded "h-9"
        // for the Default horizontal track regardless of Density. Predicted WRONG value
        // at Spacious under that old code is "h-9"; correct/actual is "h-11".
        var cut = RenderTabsList(L.Tabs.TabsVariant.Default, L.Orientation.Horizontal, L.Density.Spacious);
        var tokens = (cut.Find("[role='tablist']").GetAttribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains("h-11", tokens);      // actual (fixed) value
        Assert.DoesNotContain("h-9", tokens);  // predicted value under the pre-fix hardcoded class
    }
}
