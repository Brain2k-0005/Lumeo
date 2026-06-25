using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;

namespace Lumeo.Tests.Components.FileManager;

/// <summary>
/// Battle-wave-2 triage #130 (medium, lifecycle) for FileManager.
///
/// #130 — folder lazy-loading was gated by a SINGLE shared <c>_loadingFolder</c>
/// bool toggled in try/finally by OnParametersSetAsync, NavigateToFolder AND
/// ToggleTreeFolder. Under two concurrent loads (e.g. the right pane navigates
/// into folder A while the tree pane expands an unrelated folder B), whichever
/// request finished first ran its finally and cleared the shared flag — turning
/// the right-pane spinner OFF while the OTHER folder was still loading (a
/// flickering / wrong-target spinner).
///
/// The fix tracks the in-flight node ids in a HashSet so each request owns only
/// its own entry, and the right-pane spinner is gated on whether the CURRENT
/// folder (the deepest path-stack node) is specifically loading — a concurrent
/// load of a different folder no longer disturbs it. A StateHasChanged after the
/// OnParametersSetAsync await also ensures the freshly-loaded children render.
///
/// These tests drive the lazy load through a TaskCompletionSource gate so the
/// concurrency window is deterministic, and assert on the OBSERVABLE spinner
/// (the animate-spin loader) and the rendered children — no real JS / timing.
/// </summary>
public class FileManagerLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileManagerLifecycleTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Two top-level lazy folders (Children null → loaded on demand). Both render
    // a chevron in the tree pane (LoadChildren is set), so either can be expanded.
    private static List<FileSystemNode> TwoLazyFolders() =>
    [
        new FileSystemNode { Id = "alpha", Name = "Alpha", IsFolder = true, Children = null },
        new FileSystemNode { Id = "beta",  Name = "Beta",  IsFolder = true, Children = null }
    ];

    private static bool HasSpinner(IRenderedComponent<Lumeo.FileManager> cut)
        => cut.FindAll(".animate-spin").Count > 0;

    // ──────────────────────────────────────────────────────────────────────────
    // #130: a concurrent load of a DIFFERENT folder must not clear the right-pane
    // spinner while the current folder is still loading.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_Concurrent_FolderLoad_DoesNotClear_OtherFolders_Spinner()
    {
        // Per-folder completion gates. Alpha's load is held open for the whole
        // test; Beta's completes the instant it is requested.
        var alphaGate = new TaskCompletionSource<List<FileSystemNode>>();

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, TwoLazyFolders())
            .Add(x => x.LoadChildren, folder => folder.Id switch
            {
                "alpha" => alphaGate.Task,
                _ => Task.FromResult(new List<FileSystemNode>
                {
                    new FileSystemNode { Id = "beta-child", Name = "beta-child.txt", IsFolder = false, Size = 4 }
                })
            }));

        // Right pane: navigate INTO Alpha (double-click its root row). Alpha is now
        // the current folder and its load is held open → the right-pane spinner is
        // showing for Alpha.
        var alphaRow = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("Alpha"));
        alphaRow.DoubleClick();
        Assert.True(HasSpinner(cut), "Alpha's load is in flight → spinner must show.");

        // Tree pane: expand the UNRELATED Beta folder. Its load completes
        // immediately. Pre-fix Beta's finally cleared the shared _loadingFolder
        // bool, killing Alpha's still-active spinner. Post-fix the per-node gate
        // keeps Alpha's spinner up because Alpha (the current folder) is still in
        // the loading set.
        var betaChevron = cut.FindAll("[role='treeitem'] button[aria-label='Expand']")
            .First(b => b.ParentElement!.TextContent.Contains("Beta"));
        betaChevron.Click();

        Assert.True(HasSpinner(cut),
            "Concurrent load of Beta must NOT clear the current folder (Alpha) spinner.");

        // Finish Alpha's load → its spinner clears and the children render.
        cut.InvokeAsync(() => alphaGate.SetResult(new List<FileSystemNode>
        {
            new FileSystemNode { Id = "alpha-child", Name = "alpha-child.txt", IsFolder = false, Size = 8 }
        })).GetAwaiter().GetResult();

        cut.WaitForAssertion(() =>
        {
            Assert.False(HasSpinner(cut));
            Assert.Contains("alpha-child.txt", cut.Markup);
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #130: loaded children render after the OnParametersSetAsync await
    // (StateHasChanged after the await). Drives the lazy-load purely through the
    // CurrentPath parameter path — no navigation click.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_ParameterPath_LazyLoad_RendersChildren_AfterAwait()
    {
        var gate = new TaskCompletionSource<List<FileSystemNode>>();

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, TwoLazyFolders())
            .Add(x => x.CurrentPath, "alpha")
            .Add(x => x.LoadChildren, _ => gate.Task));

        // Alpha opened straight via CurrentPath; its children are still loading.
        Assert.True(HasSpinner(cut), "Current folder lazy-load in flight → spinner.");

        // Complete the load. The OnParametersSetAsync load path must StateHasChanged
        // after the await so the newly-arrived children render (pre-fix the load
        // path issued no post-await render).
        cut.InvokeAsync(() => gate.SetResult(new List<FileSystemNode>
        {
            new FileSystemNode { Id = "a1", Name = "alpha-one.txt", IsFolder = false, Size = 1 }
        })).GetAwaiter().GetResult();

        cut.WaitForAssertion(() =>
        {
            Assert.False(HasSpinner(cut));
            Assert.Contains("alpha-one.txt", cut.Markup);
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Normal-path guard: a single navigation lazy-load still shows the spinner
    // then the children, and disposal does not throw. Confirms the per-node
    // refactor preserved the ordinary behaviour and teardown.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_SingleNavigationLazyLoad_ShowsThenResolves_NoThrowOnDispose()
    {
        var gate = new TaskCompletionSource<List<FileSystemNode>>();

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, TwoLazyFolders())
            .Add(x => x.LoadChildren, _ => gate.Task));

        var alphaRow = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("Alpha"));
        alphaRow.DoubleClick();
        Assert.True(HasSpinner(cut));

        cut.InvokeAsync(() => gate.SetResult(new List<FileSystemNode>
        {
            new FileSystemNode { Id = "only", Name = "only.txt", IsFolder = false, Size = 2 }
        })).GetAwaiter().GetResult();

        cut.WaitForAssertion(() =>
        {
            Assert.False(HasSpinner(cut));
            Assert.Contains("only.txt", cut.Markup);
        });

        // Teardown is clean: the component's own IAsyncDisposable unregisters its
        // interop without throwing (and the lifecycle refactor left disposal
        // untouched). Dispose the component instance directly so we don't
        // double-dispose the shared context (the IAsyncLifetime DisposeAsync does
        // that).
        var instance = cut.Instance;
        var ex = Record.ExceptionAsync(async () => await instance.DisposeAsync()).GetAwaiter().GetResult();
        Assert.Null(ex);
    }
}
