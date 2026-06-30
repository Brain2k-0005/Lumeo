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
    // SelectedNode: a re-render UNCHANGED from before the interaction is NOT a
    // veto — it is the established Lumeo convention for an observer-only
    // consumer that binds SelectedNodeChanged purely to react, without echoing
    // SelectedNode back (the parameter then stays at its initial value forever).
    // See PickList's #38 / SortableList's #144 for the same, deliberately-tested
    // contract elsewhere in this library.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Controlled_SelectedNode_Unchanged_Reselect_Keeps_Local_Selection()
    {
        var tree = BuildSampleTree();

        // Parent starts unselected and its handler never echoes SelectedNode back
        // (re-renders with the SAME null it always had — an observer, not a veto).
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

        var reportRow = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        reportRow.Click();

        // The unchanged-from-before re-render must NOT clear the optimistic selection.
        var selected = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        Assert.Equal("true", selected.GetAttribute("aria-selected"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SelectedNode: a GENUINE, distinguishable veto (the parent selects something
    // ELSE) rolls back to that value.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Controlled_SelectedNode_Veto_With_Distinct_Value_Rolls_Back_To_That_Value()
    {
        var tree = BuildSampleTree();
        FileSystemNode? parentSelection = null;
        IRenderedComponent<Lumeo.FileManager>? cut = null;

        var callback = EventCallback.Factory.Create<FileSystemNode?>(_ctx, (incoming) =>
        {
            // Genuine veto: explicitly select notes.txt instead — a value that differs from BOTH
            // what the user clicked (report.pdf) AND the pre-interaction snapshot (null).
            var notes = tree[0].Children!.First(c => c.Id == "notes");
            cut!.Render(p => p
                .Add(x => x.Root, tree)
                .Add(x => x.CurrentPath, "docs")
                .Add(x => x.SelectedNode, notes)
                .Add(x => x.SelectedNodeChanged, EventCallback.Factory.Create<FileSystemNode?>(_ctx, _ => { })));
        });

        cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.SelectedNode, parentSelection)
            .Add(x => x.SelectedNodeChanged, callback));

        var reportRow = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        reportRow.Click();

        // The parent's distinct, authoritative decision wins — notes.txt is selected, report.pdf is not.
        var rows = cut.FindAll("[role='row']");
        var reportSelected = rows.First(r => r.TextContent.Contains("report.pdf")).GetAttribute("aria-selected");
        var notesSelected = rows.First(r => r.TextContent.Contains("notes.txt")).GetAttribute("aria-selected");
        Assert.Equal("false", reportSelected);
        Assert.Equal("true", notesSelected);
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
