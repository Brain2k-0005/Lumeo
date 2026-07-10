using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

using Item = L.TreeView<string>.TreeViewItem<string>;

/// <summary>
/// PR #351 round-5, finding 1: a row click that both selects AND lazily expands must not gate the
/// selection on the child-load. The previous reorder awaited ToggleExpand() — which awaits
/// LoadChildren — BEFORE the selection callbacks, so a slow loader delayed the selection and a
/// throwing loader killed it (the exception escaped before selection ran). The expansion state is
/// still flipped synchronously (round-4), but the lazy load now runs isolated from the selection.
/// </summary>
public class TreeViewLazySelectionIsolationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewLazySelectionIsolationTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    private static List<Item> LazyRoot() =>
        [new() { Text = "Lazy", Value = "lazy", IsLeaf = false }];

    [Fact]
    public async Task Slow_loader_does_not_block_the_selection_callback()
    {
        // The loader never completes until we release the gate — the selection must land first.
        var gate = new TaskCompletionSource<List<Item>>();
        var selectedBeforeChildren = false;
        var childrenResolved = false;

        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, LazyRoot())
            .Add(c => c.LoadChildren, loader)
            .Add(c => c.OnItemClick, (Item _) =>
            {
                // Selection fires while the loader is STILL pending (children not yet resolved).
                selectedBeforeChildren = !childrenResolved;
            }));

        await cut.InvokeAsync(() => Row(cut, "Lazy").Click());

        // Selection is applied even though the loader is still hanging.
        Assert.True(selectedBeforeChildren);
        Assert.Equal("true", TreeItem(cut, "Lazy").GetAttribute("aria-selected"));
        Assert.DoesNotContain("Child-A", cut.Markup); // children not resolved yet

        // Release the loader — the branch now fills in without disturbing the selection.
        childrenResolved = true;
        await cut.InvokeAsync(() => gate.SetResult(
        [
            new() { Text = "Child-A", Value = "a", IsLeaf = true }
        ]));

        Assert.Contains("Child-A", cut.Markup);
        Assert.Equal("true", TreeItem(cut, "Lazy").GetAttribute("aria-selected"));
    }

    [Fact]
    public async Task Throwing_loader_still_selects_and_raises_no_unhandled_exception()
    {
        Func<Item, Task<List<Item>>> loader = _ => throw new InvalidOperationException("loader boom");

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, LazyRoot())
            .Add(c => c.LoadChildren, loader));

        // The click must NOT surface the loader exception (bUnit rethrows unhandled ones here).
        await cut.InvokeAsync(() => Row(cut, "Lazy").Click());

        // Selection is applied despite the loader blowing up.
        Assert.Equal("true", TreeItem(cut, "Lazy").GetAttribute("aria-selected"));
    }

    [Fact]
    public async Task Faulted_task_loader_still_selects_and_raises_no_unhandled_exception()
    {
        // Same as above but the loader returns a faulted Task rather than throwing synchronously.
        Func<Item, Task<List<Item>>> loader = _ =>
            Task.FromException<List<Item>>(new InvalidOperationException("loader boom"));

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, LazyRoot())
            .Add(c => c.LoadChildren, loader));

        await cut.InvokeAsync(() => Row(cut, "Lazy").Click());

        Assert.Equal("true", TreeItem(cut, "Lazy").GetAttribute("aria-selected"));
    }

    /// <summary>
    /// PR #351 round-7, finding 1: when a row click both selects AND lazily expands, but the loader
    /// FAILS, the expansion must roll back. The round-5 split flips IsExpanded synchronously; before
    /// this change a failed load left the row aria-expanded=true with a down chevron over an empty
    /// branch, and the next click COLLAPSED it instead of retrying. Now the failure rolls the
    /// expansion back to collapsed (selection intact) and — because ChildrenLoaded stayed false — a
    /// second click retries the loader and the branch fills in.
    /// </summary>
    [Fact]
    public async Task Failed_row_click_load_rolls_expansion_back_keeps_selection_and_allows_retry()
    {
        var attempts = 0;
        Func<Item, Task<List<Item>>> loader = _ =>
        {
            attempts++;
            if (attempts == 1)
                throw new InvalidOperationException("loader boom"); // first attempt fails
            return Task.FromResult(new List<Item>
            {
                new() { Text = "Child-A", Value = "a", IsLeaf = true }
            });
        };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, LazyRoot())
            .Add(c => c.LoadChildren, loader));

        // First click: selects, tries to load, loader throws — the click must not surface it.
        await cut.InvokeAsync(() => Row(cut, "Lazy").Click());

        // Selection survived the failure (round-5) AND the expansion rolled back (round-7): the row
        // is collapsed again, not stranded aria-expanded=true over an empty branch.
        Assert.Equal("true", TreeItem(cut, "Lazy").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "Lazy").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("Child-A", cut.Markup);

        // Retry: because ChildrenLoaded stayed false, a second click re-runs the loader (which now
        // succeeds) rather than merely toggling a phantom expanded-but-empty state.
        await cut.InvokeAsync(() => Row(cut, "Lazy").Click());

        Assert.Equal(2, attempts);
        Assert.Contains("Child-A", cut.Markup);
        Assert.Equal("true", TreeItem(cut, "Lazy").GetAttribute("aria-expanded"));
        Assert.Equal("true", TreeItem(cut, "Lazy").GetAttribute("aria-selected"));
    }

    /// <summary>
    /// The finding's "slow-then-fail" variant: the loader hangs (selection lands first, round-5),
    /// then FAILS. The eventual failure must still roll the expansion back to collapsed without
    /// disturbing the already-applied selection.
    /// </summary>
    [Fact]
    public async Task Slow_then_failing_loader_rolls_expansion_back_after_the_selection_landed()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, LazyRoot())
            .Add(c => c.LoadChildren, loader));

        await cut.InvokeAsync(() => Row(cut, "Lazy").Click());

        // Selection is applied while the loader still hangs; the row is optimistically expanded.
        Assert.Equal("true", TreeItem(cut, "Lazy").GetAttribute("aria-selected"));
        Assert.Equal("true", TreeItem(cut, "Lazy").GetAttribute("aria-expanded"));

        // The loader now fails — the row-click drains it isolated, and the expansion rolls back.
        await cut.InvokeAsync(() => gate.SetException(new InvalidOperationException("loader boom")));

        Assert.Equal("false", TreeItem(cut, "Lazy").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("Child-A", cut.Markup);
        Assert.Equal("true", TreeItem(cut, "Lazy").GetAttribute("aria-selected")); // selection intact
    }
}
