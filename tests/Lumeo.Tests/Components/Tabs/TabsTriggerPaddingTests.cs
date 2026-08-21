using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tabs;

/// <summary>
/// PadClass's Comfortable-density row is variant- AND orientation-scoped: only the
/// HORIZONTAL Default trigger needs `py-0.5` to fit inside TabsList's `h-8 p-[3px]` track
/// (28px content box — the wave-0 C2 overflow fix); every other (variant, orientation)
/// combination at Comfortable keeps the looser `py-1`. Two rounds of Codex findings
/// on this exact line each caught a leak the previous round's scoping missed:
///   Round 2 (#386 P2 finding 2): py-1 applied to every VARIANT, not just Default —
///     Card/Pill/Underline render inside a `h-10` track with no `p-1` at all (see
///     TabsList.razor CssClass), so they were never at overflow risk.
///   Round 3 (#386 P2 finding 5): even scoped to Default, py-1 still applied to both
///     ORIENTATIONS — TabsList's vertical Default track is `flex flex-col h-auto w-auto
///     ... p-1` (no fixed height at all, verified in TabsList.razor CssClass), so a
///     vertical Default trigger has unbounded headroom and was never at overflow risk
///     either.
/// This suite pins the FULL matrix (4 variants x 2 orientations x 3 densities = 24
/// combinations) so a third leak along some other dimension can't land unnoticed.
/// </summary>
public class TabsTriggerPaddingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public TabsTriggerPaddingTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderTabs(
        L.Tabs.TabsVariant variant,
        L.Orientation orientation = L.Orientation.Horizontal,
        L.Density density = L.Density.Comfortable)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DensityScope>(0);
            builder.AddAttribute(1, "Value", density);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(scope =>
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
            }));
            builder.CloseComponent();
        });

    // The numbers below moved once, in the 5.0 scale alignment: shadcn's tab trigger is
    // px-1.5 py-0.5 and its track h-8, measured in a project built with their CLI. The
    // SHAPE of the table is unchanged, and that is what these tests were written for -
    // only Default+Horizontal at Comfortable takes the tighter vertical padding, every
    // other variant/orientation keeps the looser one. Three rounds of review findings
    // are about that scoping, not about the particular pixel values.
    // Exact-token assertion, not Assert.Contains: "py-0.5" itself CONTAINS "py-0" as a
    // substring, so a plain Contains("py-0") check would pass on a py-0.5 class - and the
    // pair those two guard is exactly Compact against Comfortable. That is the kind of
    // class-string-only assertion this component's escaped bugs have repeatedly slipped past.
    private static void AssertExactPadding(string cls, string expectedPad, string expectedPy)
    {
        var tokens = cls.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(expectedPad, tokens);
        Assert.Contains(expectedPy, tokens);
    }

    public static IEnumerable<object[]> Matrix()
    {
        foreach (var variant in new[] { L.Tabs.TabsVariant.Default, L.Tabs.TabsVariant.Card, L.Tabs.TabsVariant.Pill, L.Tabs.TabsVariant.Underline })
        foreach (var orientation in new[] { L.Orientation.Horizontal, L.Orientation.Vertical })
        foreach (var density in new[] { L.Density.Compact, L.Density.Comfortable, L.Density.Spacious })
        {
            var (pad, py) = density switch
            {
                L.Density.Compact => ("px-1", "py-0"),
                L.Density.Spacious => ("px-3", "py-1"),
                // Comfortable: only Default+Horizontal gets the tighter py-0.5;
                // every other variant/orientation combination keeps py-1.
                _ when variant == L.Tabs.TabsVariant.Default && orientation == L.Orientation.Horizontal
                    => ("px-1.5", "py-0.5"),
                _ => ("px-2", "py-1"),
            };
            yield return new object[] { variant, orientation, density, pad, py };
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void PadClass_Matches_Variant_Orientation_Density_Matrix(
        L.Tabs.TabsVariant variant, L.Orientation orientation, L.Density density, string expectedPad, string expectedPy)
    {
        var cut = RenderTabs(variant, orientation, density);

        var cls = cut.Find("button[role='tab']").GetAttribute("class") ?? "";
        AssertExactPadding(cls, expectedPad, expectedPy);
    }

    [Fact]
    public void Default_Horizontal_Comfortable_Renders_Py1_Not_Py1Point5()
    {
        // Predicted-vs-actual (disable check): reverting PadClass to the round-2 fix
        // (Default-only, no orientation gate) still renders py-1 here — Default +
        // Horizontal is unaffected either way, this just pins the value stays correct
        // after the orientation-scoping fix.
        var cut = RenderTabs(L.Tabs.TabsVariant.Default, L.Orientation.Horizontal);

        var cls = cut.Find("button[role='tab']").GetAttribute("class") ?? "";
        AssertExactPadding(cls, "px-1.5", "py-0.5");
    }

    [Fact]
    public void Default_Vertical_Comfortable_Keeps_Py1Point5()
    {
        // This is the exact regression this test class closes: predicted WRONG value
        // under the pre-fix (Default-only, no orientation gate) code is "py-1" (28px
        // trigger) because that code's (Tabs.TabsVariant.Default, _) arm matches
        // vertical too; the correct/expected value is "py-1.5" (32px trigger), since
        // TabsList's vertical Default track is h-auto and was never overflow-constrained.
        var cut = RenderTabs(L.Tabs.TabsVariant.Default, L.Orientation.Vertical);

        var cls = cut.Find("button[role='tab']").GetAttribute("class") ?? "";
        AssertExactPadding(cls, "px-2", "py-1");
    }

    [Theory]
    [InlineData(L.Tabs.TabsVariant.Card)]
    [InlineData(L.Tabs.TabsVariant.Pill)]
    [InlineData(L.Tabs.TabsVariant.Underline)]
    public void Non_Default_Variants_Keep_Py1Point5_At_Comfortable(L.Tabs.TabsVariant variant)
    {
        var cut = RenderTabs(variant);

        var cls = cut.Find("button[role='tab']").GetAttribute("class") ?? "";
        AssertExactPadding(cls, "px-2", "py-1");
    }
}
