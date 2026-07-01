using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// Regression tests for battle-wave1 finding #68 (lifecycle): the seed-to-selected
/// one-shot (<c>_wasOpen</c>) is consumed on the first open render and never re-runs
/// when the selected <c>Value</c> changes while the popover STAYS open. Radix/shadcn
/// behaviour is that the listbox highlights the current value; if the value is swapped
/// programmatically (or by a parent re-render) mid-open, the highlight must follow it.
///
/// The fix re-runs the seed in OnParametersSet when the selected value changes and the
/// highlight is still parked on the previously-seeded value (untouched), while leaving
/// an explicit user ArrowDown/typeahead highlight alone.
///
/// Mirrors SelectFocusOnDataChangeTests / SelectKeyboardNavTests' data-bound render +
/// bg-accent highlight assertions.
/// </summary>
public class SelectReseedOnValueChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SelectReseedOnValueChangeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly RenderFragment Child = b =>
    {
        b.OpenComponent<L.SelectTrigger>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
        b.CloseComponent();
        b.OpenComponent<L.SelectContent>(2);
        b.CloseComponent();
    };

    private IRenderedComponent<L.Select> RenderOpen(string? value, IEnumerable<object> items)
        => _ctx.Render<L.Select>(p =>
        {
            p.Add(s => s.Open, true);
            p.Add(s => s.Value, value);
            p.Add(s => s.Items, items);
            p.Add(s => s.ChildContent, Child);
        });

    private static AngleSharp.Dom.IElement? FindOption(IRenderedComponent<L.Select> cut, string text)
        => cut.FindAll("button[role='option']").FirstOrDefault(b => b.TextContent.Contains(text));

    [Fact]
    public void Highlight_Follows_The_Selected_Value_When_It_Changes_While_Open()
    {
        // Opens highlighting the selected value ("banana") via the seed-to-selected.
        var cut = RenderOpen("banana", new object[] { "apple", "banana", "cherry" });
        Assert.Contains("bg-accent", FindOption(cut, "banana")!.ClassList);

        // The parent swaps Value to "cherry" while the popover STAYS open (no close).
        // Without the fix the one-shot _wasOpen latch keeps the highlight stuck on
        // "banana"; with the fix the seed re-runs and the highlight moves to "cherry".
        cut.Render(p => p.Add(s => s.Value, "cherry"));

        Assert.Contains("bg-accent", FindOption(cut, "cherry")!.ClassList);
        Assert.DoesNotContain("bg-accent", FindOption(cut, "banana")!.ClassList);
    }

    [Fact]
    public void Value_Change_Does_Not_Fight_An_Explicit_User_ArrowDown()
    {
        // Open seeded on "apple" (index 0); the user ArrowDowns to "banana" (index 1).
        var cut = RenderOpen("apple", new object[] { "apple", "banana", "cherry" });
        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown("ArrowDown"); // user-driven highlight → "banana"
        Assert.Contains("bg-accent", FindOption(cut, "banana")!.ClassList);

        // Now the parent swaps Value to "cherry" mid-open. Because the highlight was
        // moved by the USER (not the auto-seed), the re-seed must NOT yank it onto
        // "cherry" — the keyboard cursor stays where the user left it.
        cut.Render(p => p.Add(s => s.Value, "cherry"));

        Assert.Contains("bg-accent", FindOption(cut, "banana")!.ClassList);
        Assert.DoesNotContain("bg-accent", FindOption(cut, "cherry")!.ClassList);
    }
}
