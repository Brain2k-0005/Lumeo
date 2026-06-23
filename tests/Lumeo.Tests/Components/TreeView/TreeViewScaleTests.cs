using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

using Item = L.TreeView<string>.TreeViewItem<string>;

/// <summary>
/// Enterprise-scale "battle test" for <see cref="L.TreeView{T}"/>: a tree backing a
/// large hierarchy (file systems, org charts, category trees) must NOT materialise
/// the whole structure up front. With <c>LoadChildren</c> the tree is effectively
/// unbounded — every node has 1,000 non-leaf children — and we prove only the
/// branches the user actually expands are ever fetched.
/// </summary>
public class TreeViewScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public TreeViewScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task Lazy_load_only_materialises_the_branches_the_user_expands()
    {
        const int childrenPerNode = 1_000;
        var loadCalls = 0;
        var totalNodesMaterialised = 0;

        // A logically-unbounded tree: every node has 1,000 non-leaf children, so the
        // full structure is astronomically large. LoadChildren generates one branch
        // on demand — nothing exists until it is asked for.
        Task<List<Item>> Load(Item node)
        {
            loadCalls++;
            var kids = Enumerable.Range(0, childrenPerNode)
                .Select(i => new Item { Text = $"{node.Value}-{i}", Value = $"{node.Value}-{i}", IsLeaf = false })
                .ToList();
            totalNodesMaterialised += kids.Count;
            return Task.FromResult(kids);
        }

        var roots = Enumerable.Range(0, 5)
            .Select(i => new Item { Text = $"Root {i}", Value = $"r{i}", IsLeaf = false })
            .ToList();

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, roots)
            .Add(c => c.LoadChildren, Load));

        // Nothing is expanded → the unbounded tree has NOT been touched at all.
        Assert.Equal(0, loadCalls);
        Assert.Equal(5, cut.FindAll("[role='treeitem']").Count);

        // Expand exactly one root → only THAT branch's 1,000 children are fetched.
        var root0 = cut.FindAll("[role='treeitem']").First(el => el.TextContent.Contains("Root 0"));
        await cut.InvokeAsync(() => root0.QuerySelector("button")!.Click());

        Assert.Equal(1, loadCalls);
        Assert.Equal(childrenPerNode, totalNodesMaterialised);
        // 5 roots + the 1,000 loaded children are present; the rest of the
        // (effectively infinite) tree was never materialised.
        var rendered = cut.FindAll("[role='treeitem']").Count;
        Assert.InRange(rendered, childrenPerNode, childrenPerNode + 10);
    }
}
