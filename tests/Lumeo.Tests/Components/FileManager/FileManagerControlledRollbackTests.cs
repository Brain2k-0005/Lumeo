using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Lumeo.Tests.Components.FileManager;

/// <summary>
/// Regression tests for the controlled-component rollback fix on FileManager's
/// two bindable properties, ViewMode and SelectedNode. When either is used
/// controlled (its *Changed delegate is bound) and the parent vetoes the change
/// by re-rendering with the value unchanged from before the user's interaction,
/// the optimistically-mutated backing field (_viewMode / _selectedNode) must
/// roll back to the parent's authoritative (rejected) value, rather than
/// permanently keeping the user's local change because OnParametersSetAsync only
/// compared against the last value seen AS A PARAMETER (which looks unchanged
/// from the parent's point of view too).
/// </summary>
public class FileManagerControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileManagerControlledRollbackTests() => _ctx.AddLumeoServices();
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
    // ViewMode: controlled veto rolls back
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Controlled_ViewMode_Veto_Rolls_Back_To_Bound_Value()
    {
        var tree = BuildSampleTree();

        // Parent starts at List and vetoes every change by always re-rendering
        // with ViewMode=List (its own state never actually updates).
        var parentMode = Lumeo.FileManager.FileManagerViewMode.List;
        IRenderedComponent<Lumeo.FileManager>? cut = null;

        var callback = EventCallback.Factory.Create<Lumeo.FileManager.FileManagerViewMode>(_ctx, (incoming) =>
        {
            // Veto: do NOT adopt `incoming`; re-render with the original value.
            cut!.Render(p => p
                .Add(x => x.Root, tree)
                .Add(x => x.CurrentPath, "docs")
                .Add(x => x.ViewMode, parentMode)
                .Add(x => x.ViewModeChanged, EventCallback.Factory.Create<Lumeo.FileManager.FileManagerViewMode>(_ctx, _ => { })));
        });

        cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.ViewMode, parentMode)
            .Add(x => x.ViewModeChanged, callback));

        Assert.NotEmpty(cut.FindAll("[role='grid']"));

        // User clicks the grid-view toggle: SetViewMode optimistically sets
        // _viewMode=Grid and fires ViewModeChanged; the parent vetoes and
        // re-renders with ViewMode=List.
        cut.Find("button[aria-label='Switch to grid view']").Click();

        // After the veto the UI must have rolled back to the List table, not
        // stayed on the optimistic Grid view.
        Assert.NotEmpty(cut.FindAll("[role='grid']"));
        Assert.Empty(cut.FindAll("[role='listbox']"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ViewMode: controlled accepted change keeps the new value
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Controlled_ViewMode_Accepted_Change_Keeps_New_Value()
    {
        var tree = BuildSampleTree();
        var parentMode = Lumeo.FileManager.FileManagerViewMode.List;
        IRenderedComponent<Lumeo.FileManager>? cut = null;

        EventCallback<Lumeo.FileManager.FileManagerViewMode> callback = default;
        callback = EventCallback.Factory.Create<Lumeo.FileManager.FileManagerViewMode>(_ctx, (incoming) =>
        {
            // Accept: the parent updates its own state and re-renders with it.
            parentMode = incoming;
            cut!.Render(p => p
                .Add(x => x.Root, tree)
                .Add(x => x.CurrentPath, "docs")
                .Add(x => x.ViewMode, parentMode)
                .Add(x => x.ViewModeChanged, callback));
        });

        cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.ViewMode, parentMode)
            .Add(x => x.ViewModeChanged, callback));

        cut.Find("button[aria-label='Switch to grid view']").Click();

        // Parent accepted — the grid view must remain.
        Assert.NotEmpty(cut.FindAll("[role='listbox']"));
        Assert.Empty(cut.FindAll("[role='grid']"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SelectedNode: controlled veto rolls back
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Controlled_SelectedNode_Veto_Rolls_Back_To_Bound_Value()
    {
        var tree = BuildSampleTree();

        // Parent starts unselected and vetoes every selection by always
        // re-rendering with SelectedNode=null (its own state never updates).
        FileSystemNode? parentSelection = null;
        IRenderedComponent<Lumeo.FileManager>? cut = null;

        var callback = EventCallback.Factory.Create<FileSystemNode?>(_ctx, (incoming) =>
        {
            cut!.Render(p => p
                .Add(x => x.Root, tree)
                .Add(x => x.CurrentPath, "docs")
                .Add(x => x.SelectedNode, parentSelection)
                .Add(x => x.SelectedNodeChanged, EventCallback.Factory.Create<FileSystemNode?>(_ctx, _ => { })));
        });

        cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.SelectedNode, parentSelection)
            .Add(x => x.SelectedNodeChanged, callback));

        // Click report.pdf: SelectNode optimistically sets _selectedNode and
        // fires SelectedNodeChanged; the parent vetoes and re-renders with
        // SelectedNode=null.
        var reportRow = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        reportRow.Click();

        // After the veto, no row may report aria-selected=true.
        var rows = cut.FindAll("[role='row']");
        Assert.All(rows, r => Assert.Equal("false", r.GetAttribute("aria-selected")));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SelectedNode: controlled accepted selection keeps the new value
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Controlled_SelectedNode_Accepted_Selection_Keeps_New_Value()
    {
        var tree = BuildSampleTree();
        FileSystemNode? parentSelection = null;
        IRenderedComponent<Lumeo.FileManager>? cut = null;

        EventCallback<FileSystemNode?> callback = default;
        callback = EventCallback.Factory.Create<FileSystemNode?>(_ctx, (incoming) =>
        {
            parentSelection = incoming;
            cut!.Render(p => p
                .Add(x => x.Root, tree)
                .Add(x => x.CurrentPath, "docs")
                .Add(x => x.SelectedNode, parentSelection)
                .Add(x => x.SelectedNodeChanged, callback));
        });

        cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.SelectedNode, parentSelection)
            .Add(x => x.SelectedNodeChanged, callback));

        var reportRow = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        reportRow.Click();

        var selected = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        Assert.Equal("true", selected.GetAttribute("aria-selected"));
    }
}
