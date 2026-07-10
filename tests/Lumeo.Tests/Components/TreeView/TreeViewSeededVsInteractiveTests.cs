using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

/// <summary>
/// PR #351 round-9 finding 2 — the TreeView selection has TWO ORIGIN BUCKETS that survive an Items
/// vanish/return by DIFFERENT rules:
///
///  • SEEDED (a value came from the consumer's <c>SelectedValues</c>) keeps VALUE semantics: on a
///    vanish it is re-queued as a VALUE and re-binds to EVERY match when the tree returns — the
///    value contract — even for a duplicate/null value. Before the fix a seeded selection silently
///    degraded into an identity carry on an empty/async reload, whose ambiguity rule then DROPPED it,
///    so a controlled duplicate seed lost its selection on a plain data refresh.
///  • INTERACTIVE (a click chose the node instance) keeps IDENTITY semantics: on a vanish it carries
///    the structural identity and DROPS on ambiguity, never lighting up a same-valued sibling.
///
///  • An interactive click on a SEEDED node converts it to interactive, after which that value
///    follows the identity (drop-on-ambiguity) rules.
///
/// All three tests reuse ONE <c>SelectedValues</c> reference across renders, so the reload skips
/// re-seeding — the carry path is what must preserve (or correctly drop) the selection.
/// </summary>
public class TreeViewSeededVsInteractiveTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewSeededVsInteractiveTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    private static List<L.TreeView<string>.TreeViewItem<string>> Empty() => [];

    // Two sibling leaves that SHARE the Value "same" under an expanded root — the duplicate/null
    // shape whose identity a structural position can't prove. Fresh instances per call.
    private static List<L.TreeView<string>.TreeViewItem<string>> DupTree() =>
    [
        new()
        {
            Text = "Root", Value = "root", IsExpanded = true,
            Children =
            [
                new() { Text = "First",  Value = "same" },
                new() { Text = "Second", Value = "same" }
            ]
        }
    ];

    // ---- SEEDED: value semantics survive a vanish/return and re-bind ALL matches ----

    [Fact]
    public void Duplicate_value_seed_survives_an_empty_reload_and_rebinds_all_matches()
    {
        // The public value contract: a seed naming a duplicate value selects EVERY match.
        var seed = new List<string> { "same" };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, DupTree())
            .Add(c => c.SelectedValues, seed));

        // Both "same"-valued siblings are selected from the seed.
        Assert.Equal(2, cut.FindAll("[role='treeitem'][aria-selected='true']").Count);

        // An async refetch momentarily returns nothing — both selected nodes vanish. Because the
        // selection is SEEDED (value origin), the value is carried as a VALUE, not an identity carry.
        cut.Render(p => p.Add(c => c.Items, Empty()).Add(c => c.SelectedValues, seed));
        Assert.Empty(cut.FindAll("[role='treeitem']"));

        // The duplicate-valued tree returns (fresh instances, SAME SelectedValues reference). The
        // value contract re-binds BOTH matches — the seed did NOT degrade into a dropped identity.
        cut.Render(p => p.Add(c => c.Items, DupTree()).Add(c => c.SelectedValues, seed));

        Assert.Equal(2, cut.FindAll("[role='treeitem'][aria-selected='true']").Count);
        Assert.Equal("true", TreeItem(cut, "First").GetAttribute("aria-selected"));
        Assert.Equal("true", TreeItem(cut, "Second").GetAttribute("aria-selected"));
    }

    // ---- INTERACTIVE: identity semantics drop a duplicate on ambiguity ----

    [Fact]
    public async Task Interactive_duplicate_selection_still_drops_on_ambiguity_after_a_vanish_return()
    {
        // No seed — the selection is INTERACTIVE (a click on one of two "same"-valued siblings).
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, DupTree()));

        await cut.InvokeAsync(() => Row(cut, "Second").Click());
        Assert.Equal("true", TreeItem(cut, "Second").GetAttribute("aria-selected"));
        Assert.Single(cut.FindAll("[role='treeitem'][aria-selected='true']"));

        // Empty reload → the node vanishes into an IDENTITY carry (its value is a duplicate).
        cut.Render(p => p.Add(c => c.Items, Empty()));
        Assert.Empty(cut.FindAll("[role='treeitem']"));

        // The duplicate-valued tree returns: neither the carried path nor the value can prove which
        // sibling was "Second", so the carry DROPS — an interactive duplicate never re-binds a
        // same-valued sibling (contrast the seeded test, which re-binds ALL matches).
        cut.Render(p => p.Add(c => c.Items, DupTree()));

        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));
    }

    // ---- CONVERSION: interacting with a seeded node makes it follow identity rules ----

    [Fact]
    public async Task Interacting_with_a_seeded_node_then_reloading_follows_identity_rules()
    {
        // Seed the duplicate value → both siblings selected (value contract).
        var seed = new List<string> { "same" };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, DupTree())
            .Add(c => c.SelectedValues, seed));
        Assert.Equal(2, cut.FindAll("[role='treeitem'][aria-selected='true']").Count);

        // The user single-select clicks "Second" — an authoritative interaction that takes the whole
        // selection INTERACTIVE and voids the seed origin. Only "Second" stays selected.
        await cut.InvokeAsync(() => Row(cut, "Second").Click());
        Assert.Single(cut.FindAll("[role='treeitem'][aria-selected='true']"));
        Assert.Equal("true", TreeItem(cut, "Second").GetAttribute("aria-selected"));

        // Empty reload with the SAME SelectedValues reference (skips re-seed). The now-interactive
        // selection vanishes as an identity carry — NOT re-queued as a value.
        cut.Render(p => p.Add(c => c.Items, Empty()).Add(c => c.SelectedValues, seed));
        Assert.Empty(cut.FindAll("[role='treeitem']"));

        // The duplicate tree returns: because the selection is now INTERACTIVE, identity is
        // unprovable among the two "same" siblings → the selection DROPS. It must NOT re-bind both
        // the way an untouched seed would, and must NOT light up a same-valued sibling.
        cut.Render(p => p.Add(c => c.Items, DupTree()).Add(c => c.SelectedValues, seed));

        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));
    }
}
