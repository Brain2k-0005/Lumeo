using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.FileManager;

/// <summary>
/// Battle-wave-2 triage #18 (high, state-on-data-change) and #19 (high,
/// keyboard-a11y) for FileManager.
///
/// #18 — ViewMode and SelectedNode were written straight into their
/// [Parameter] props by SetViewMode / SelectNode, with no internal backing
/// field. OnParametersSetAsync runs on every render, so any unrelated parent
/// re-render re-applied the parent's last-supplied parameter value (e.g. the
/// ViewMode default of List, or a null SelectedNode) and silently reverted the
/// user's uncontrolled choice. The fix stores the live value in private backing
/// fields adopted from the parameter only when the parameter actually changes.
///
/// #19 — beginning an inline rename set _renamingNode but raised no focus flag,
/// and OnAfterRenderAsync only handled the context-menu focus. The rename input
/// was never auto-focused, so keyboard users landed on a rename field with no
/// focus. The fix gives the input a stable id and focuses it on the next render.
/// </summary>
public class FileManagerStateRetentionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileManagerStateRetentionTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<FileSystemNode> BuildSampleTree() =>
    [
        new FileSystemNode
        {
            Id = "docs",
            Name = "Documents",
            IsFolder = true,
            Children =
            [
                new FileSystemNode { Id = "report", Name = "report.pdf", IsFolder = false, Size = 204_800 },
                new FileSystemNode { Id = "notes",  Name = "notes.txt",  IsFolder = false, Size = 1_024 }
            ]
        }
    ];

    // ──────────────────────────────────────────────────────────────────────────
    // #18: uncontrolled ViewMode survives an unrelated parent re-render
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_Uncontrolled_ViewMode_Survives_Parent_ReRender()
    {
        var tree = BuildSampleTree();

        // Uncontrolled: no ViewModeChanged delegate, ViewMode left at its default.
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs"));

        // Default is List → a table (role=grid), no grid-tile listbox.
        Assert.NotEmpty(cut.FindAll("[role='grid']"));
        Assert.Empty(cut.FindAll("[role='listbox']"));

        // User switches to Grid via the toolbar toggle.
        cut.Find("button[aria-label='Switch to grid view']").Click();

        // Grid view renders a role=listbox of role=option tiles.
        Assert.NotEmpty(cut.FindAll("[role='listbox']"));
        Assert.Empty(cut.FindAll("[role='grid']"));

        // An UNRELATED parent re-render: same params re-supplied (ViewMode is not
        // bound, so Blazor re-pushes its default = List). Pre-fix this reverted
        // the view to the table; post-fix the backing field keeps Grid.
        cut.Render(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs"));

        Assert.NotEmpty(cut.FindAll("[role='listbox']"));
        Assert.Empty(cut.FindAll("[role='grid']"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #18: uncontrolled selection survives an unrelated parent re-render
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_Uncontrolled_Selection_Survives_Parent_ReRender()
    {
        var tree = BuildSampleTree();

        // Uncontrolled: no SelectedNodeChanged delegate.
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs"));

        // Select report.pdf by clicking its row.
        var reportRow = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        reportRow.Click();

        // The selected row reports aria-selected=true.
        var selectedAfterClick = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        Assert.Equal("true", selectedAfterClick.GetAttribute("aria-selected"));

        // Unrelated parent re-render re-supplying the same params (SelectedNode is
        // unbound → Blazor re-pushes its default = null). Pre-fix this cleared the
        // selection; post-fix the backing field keeps it.
        cut.Render(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs"));

        var stillSelected = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        Assert.Equal("true", stillSelected.GetAttribute("aria-selected"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #18: a CONTROLLED consumer can still override the view from the parent
    // (guards against the backing field swallowing genuine external changes)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_Controlled_ViewMode_Change_From_Parent_Still_Applies()
    {
        var tree = BuildSampleTree();

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.ViewMode, Lumeo.FileManager.FileManagerViewMode.List));

        Assert.NotEmpty(cut.FindAll("[role='grid']"));

        // Parent genuinely changes the controlled ViewMode parameter to Grid.
        cut.Render(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.ViewMode, Lumeo.FileManager.FileManagerViewMode.Grid));

        Assert.NotEmpty(cut.FindAll("[role='listbox']"));
        Assert.Empty(cut.FindAll("[role='grid']"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #19: beginning an inline rename auto-focuses the rename input
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_BeginRename_AutoFocuses_The_Rename_Input()
    {
        // Swap in the tracking interop so we can assert FocusElement was invoked.
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var tree = BuildSampleTree();

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.OnRename, EventCallback.Factory.Create<(FileSystemNode, string)>(this, _ => { })));

        // Open the context menu on report.pdf and click Rename.
        var reportRow = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        reportRow.TriggerEvent("oncontextmenu", new MouseEventArgs { ClientX = 50, ClientY = 50 });

        var renameBtn = cut.FindAll("[role='menu'] button").First(b => b.TextContent.Contains("Rename"));
        renameBtn.Click();

        // The inline rename input must now exist and carry a stable id...
        var renameInput = cut.Find("input[type='text']");
        var renameId = renameInput.GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(renameId));

        // ...and the component must have asked the interop to focus exactly that id
        // on the render after the rename began. Pre-fix the input had no id and no
        // focus call was ever issued. (WaitForAssertion gives the post-render
        // OnAfterRenderAsync focus hop its async beat.)
        cut.WaitForAssertion(() => Assert.Contains(renameId!, interop.FocusElementCalls));
    }
}
