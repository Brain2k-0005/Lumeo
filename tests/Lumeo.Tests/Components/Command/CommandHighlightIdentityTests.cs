using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Command;

/// <summary>
/// Regression tests for the state-on-data-change battle-test findings #71/#72:
/// the Command palette highlight used to be a raw positional index into
/// VisibleItems, so it was NOT anchored to a command's identity.
///
///  #71 — inserting/removing an item ABOVE the highlight (without any search
///        change) silently slid the highlight, and therefore the Enter target,
///        onto a DIFFERENT command.
///  #72 — when the registered items shrank (or emptied then refilled) without a
///        search change, the stale ordinal was reused instead of resetting to a
///        clean pristine state.
///
/// The fix anchors the highlight to the CommandItem instance (_highlighted) and
/// derives the visible index on demand, so insertions/removals around it no
/// longer move it, and a removed-highlight resets to pristine.
///
/// These tests drive the data change through a typed host whose item list is
/// re-rendered with <c>cut.Render(p =&gt; p.Add(...))</c> (each item @key'd by its
/// label so unchanged items keep their CommandItem instance across renders), and
/// assert the MECHANISM via aria-selected / aria-activedescendant rather than
/// real DOM focus.
/// </summary>
public class CommandHighlightIdentityTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CommandHighlightIdentityTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>
    /// Test host: a Command palette whose item set is a parameter so a parent
    /// re-render can add/remove items. Each item is @key'd by its label so Blazor
    /// preserves the CommandItem instance for unchanged labels (the identity the
    /// fix anchors the highlight to). The label doubles as the FilterValue.
    /// </summary>
    private sealed class HighlightHost : ComponentBase
    {
        [Parameter] public IReadOnlyList<string> Items { get; set; } = Array.Empty<string>();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<L.Command>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.CommandInput>(0);
                b.CloseComponent();

                b.OpenComponent<L.CommandList>(1);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(list =>
                {
                    var seq = 0;
                    foreach (var label in Items)
                    {
                        var captured = label;
                        list.OpenComponent<L.CommandItem>(seq++);
                        list.SetKey(captured); // stable identity across re-renders
                        list.AddAttribute(seq++, "FilterValue", captured);
                        list.AddAttribute(seq++, "ChildContent",
                            (RenderFragment)(i => i.AddContent(0, captured)));
                        list.CloseComponent();
                    }
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    private static AngleSharp.Dom.IElement? FindOption(IRenderedComponent<HighlightHost> cut, string text)
        => cut.FindAll("button[role='option']").FirstOrDefault(b => b.TextContent.Contains(text));

    // #71 — inserting an item above the highlight must NOT move it.
    [Fact]
    public void Inserting_Item_Above_Highlight_Keeps_Highlight_On_Same_Command()
    {
        var cut = _ctx.Render<HighlightHost>(p => p
            .Add(h => h.Items, new[] { "Save", "Open", "Quit" }));

        // Highlight the second command ("Open") via the keyboard.
        var input = cut.Find("input");
        input.KeyDown("ArrowDown"); // Save
        input.KeyDown("ArrowDown"); // Open
        Assert.Equal("true", FindOption(cut, "Open")!.GetAttribute("aria-selected"));

        // Parent inserts a NEW command ABOVE the highlight (no search change).
        cut.Render(p => p.Add(h => h.Items, new[] { "New", "Save", "Open", "Quit" }));

        // The highlight must still be on "Open" — not slid to whatever now sits
        // at the old ordinal (which was "Save" before the fix).
        Assert.Equal("true", FindOption(cut, "Open")!.GetAttribute("aria-selected"));
        Assert.Equal("false", FindOption(cut, "Save")!.GetAttribute("aria-selected"));
        Assert.Equal(
            FindOption(cut, "Open")!.GetAttribute("id"),
            cut.Find("input").GetAttribute("aria-activedescendant"));
    }

    // #71 — removing an item above the highlight must NOT move it.
    [Fact]
    public void Removing_Item_Above_Highlight_Keeps_Highlight_On_Same_Command()
    {
        var cut = _ctx.Render<HighlightHost>(p => p
            .Add(h => h.Items, new[] { "Save", "Open", "Quit" }));

        var input = cut.Find("input");
        input.KeyDown("ArrowDown"); // Save
        input.KeyDown("ArrowDown"); // Open
        input.KeyDown("ArrowDown"); // Quit
        Assert.Equal("true", FindOption(cut, "Quit")!.GetAttribute("aria-selected"));

        // Remove a command ABOVE the highlight (no search change).
        cut.Render(p => p.Add(h => h.Items, new[] { "Open", "Quit" }));

        // The highlight (and Enter target) must still be "Quit", not the command
        // that now occupies the old index-2 slot.
        Assert.Equal("true", FindOption(cut, "Quit")!.GetAttribute("aria-selected"));
        Assert.Equal(
            FindOption(cut, "Quit")!.GetAttribute("id"),
            cut.Find("input").GetAttribute("aria-activedescendant"));
    }

    // #72 — removing the highlighted command itself resets to a pristine state
    // (no positional fallback), so nothing is highlighted until the user moves.
    [Fact]
    public void Removing_The_Highlighted_Command_Resets_To_Pristine()
    {
        var cut = _ctx.Render<HighlightHost>(p => p
            .Add(h => h.Items, new[] { "Save", "Open", "Quit" }));

        var input = cut.Find("input");
        input.KeyDown("ArrowDown"); // Save
        input.KeyDown("ArrowDown"); // Open (this is the one we remove)
        Assert.Equal("true", FindOption(cut, "Open")!.GetAttribute("aria-selected"));

        // The highlighted command disappears (e.g. items shrink for a refill).
        cut.Render(p => p.Add(h => h.Items, new[] { "Save", "Quit" }));

        // No surviving option inherits the highlight via a stale ordinal.
        Assert.Equal("false", FindOption(cut, "Save")!.GetAttribute("aria-selected"));
        Assert.Equal("false", FindOption(cut, "Quit")!.GetAttribute("aria-selected"));
        Assert.True(string.IsNullOrEmpty(cut.Find("input").GetAttribute("aria-activedescendant")));
    }
}
