using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.FileManager;

/// <summary>
/// Battle-wave-2 triage keyboard-a11y bugs for FileManager:
///
/// #126 — the context menu did not restore focus on close and was not
/// focus-trapped. The fix engages a Tab-cycling focus trap (SetupFocusTrap) on
/// open, which saves the trigger element and restores focus to it on close
/// (RemoveFocusTrap) — WCAG 2.4.3.
///
/// #127 — context-menu items were not arrow-key navigable: HandleContextMenuKeyDown
/// handled only Escape. The fix mirrors MenubarContent: ArrowUp/Down/Home/End move
/// focus across the menuitems via GetMenuItemCount + FocusMenuItemByIndex.
///
/// #128 — every file row / grid tile carried tabindex=0 (no roving tabindex) and
/// arrow keys did nothing. The fix adopts a roving tabindex: only the active item
/// is tabindex=0, the rest tabindex=-1, and Arrow/Home/End move the active item
/// (and DOM focus) along the list.
///
/// #129 — Space on a row selected the node but also scrolled the page (no
/// preventDefault). The fix registers a native key-selective preventDefault on the
/// file-list container for Space/Arrows/Home/End (SkipEditable so the inline
/// rename input keeps typing) rather than @onkeydown:preventDefault which would
/// also swallow Tab.
///
/// bUnit cannot move real DOM focus, so these tests assert the OBSERVABLE
/// MECHANISMS: the rendered tabindex/aria after a keydown, and the interop calls
/// the component issues through a TrackingInteropService (FocusTrapSetups /
/// FocusTrapRemovals / FocusMenuItemCalls / FocusElementCalls).
/// </summary>
public class FileManagerKeyboardA11yTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileManagerKeyboardA11yTests() => _ctx.AddLumeoServices();

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
                new FileSystemNode { Id = "notes",  Name = "notes.txt",  IsFolder = false, Size = 1_024 },
                new FileSystemNode { Id = "draft",  Name = "draft.txt",  IsFolder = false, Size = 2_048 }
            ]
        }
    ];

    // ──────────────────────────────────────────────────────────────────────────
    // #126: closing the context menu restores focus to the trigger
    // (RemoveFocusTrap is the return-focus mechanism)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_ClosingContextMenu_RestoresFocus_ViaFocusTrap()
    {
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildSampleTree())
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.OnRename, EventCallback.Factory.Create<(FileSystemNode, string)>(this, _ => { })));

        // Open the context menu on report.pdf.
        var reportRow = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        reportRow.TriggerEvent("oncontextmenu", new MouseEventArgs { ClientX = 50, ClientY = 50 });

        // The menu exists and a Tab-cycling focus trap was engaged on it (this both
        // traps Tab inside the menu and saves the trigger for later restore).
        var menu = cut.Find("[role='menu']");
        var menuId = menu.GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(menuId));
        cut.WaitForAssertion(() =>
            Assert.Contains(interop.FocusTrapSetups, t => t.ElementId == menuId));

        // Press Escape on the menu → the menu closes AND the trap is removed, which
        // returns focus to the element that had it when the menu opened (WCAG 2.4.3).
        // Pre-fix CloseContextMenu only flipped the open bool — no RemoveFocusTrap.
        menu.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.WaitForAssertion(() => Assert.Contains(menuId!, interop.FocusTrapRemovals));
        Assert.Empty(cut.FindAll("[role='menu']"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #127: ArrowDown inside the open context menu moves focus across menuitems
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_ContextMenu_ArrowDown_MovesMenuItemFocus()
    {
        var interop = new TrackingInteropService { MenuItemCount = 3 };
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildSampleTree())
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.OnRename, EventCallback.Factory.Create<(FileSystemNode, string)>(this, _ => { }))
            .Add(x => x.OnDelete, EventCallback.Factory.Create<IReadOnlyList<FileSystemNode>>(this, _ => { })));

        var reportRow = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        reportRow.TriggerEvent("oncontextmenu", new MouseEventArgs { ClientX = 50, ClientY = 50 });

        var menu = cut.Find("[role='menu']");
        var menuId = menu.GetAttribute("id");

        // ArrowDown must move menu-item focus to index 0 (first item). Pre-fix the
        // keydown handler ignored ArrowDown entirely, so no FocusMenuItemByIndex
        // call was ever issued for the menu.
        menu.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Contains(interop.FocusMenuItemCalls, c => c.ContainerId == menuId && c.Index == 0);

        // A second ArrowDown advances to index 1 (proves a roving index, not a
        // constant), confirming the menu is genuinely arrow-navigable.
        menu.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Contains(interop.FocusMenuItemCalls, c => c.ContainerId == menuId && c.Index == 1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #128: file rows use a roving tabindex (exactly one tabindex=0), and
    // ArrowDown moves the single tab stop to the next row
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_Rows_Use_Roving_Tabindex_And_ArrowDown_Moves_It()
    {
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildSampleTree())
            .Add(x => x.CurrentPath, "docs"));

        // Exactly ONE row is in the tab sequence (tabindex=0); the rest are -1.
        // Pre-fix EVERY row carried tabindex=0, so this count was 3.
        var rows = cut.FindAll("[role='row']");
        Assert.Equal(1, rows.Count(r => r.GetAttribute("tabindex") == "0"));

        // The first row (report.pdf) is the initial tab stop.
        var firstRow = rows.First(r => r.TextContent.Contains("report.pdf"));
        Assert.Equal("0", firstRow.GetAttribute("tabindex"));
        var secondRow = rows.First(r => r.TextContent.Contains("notes.txt"));
        Assert.Equal("-1", secondRow.GetAttribute("tabindex"));

        // ArrowDown on the first row moves the single tab stop to the second row.
        // Pre-fix HandleRowKeyDown handled only Enter/Space, so nothing moved.
        firstRow.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        var rowsAfter = cut.FindAll("[role='row']");
        Assert.Equal(1, rowsAfter.Count(r => r.GetAttribute("tabindex") == "0"));
        Assert.Equal("-1", rowsAfter.First(r => r.TextContent.Contains("report.pdf")).GetAttribute("tabindex"));
        Assert.Equal("0", rowsAfter.First(r => r.TextContent.Contains("notes.txt")).GetAttribute("tabindex"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #128 (focus-move mechanism): ArrowDown also moves DOM focus to the newly
    // active row via Interop.FocusElement(itemId)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_ArrowDown_Focuses_The_Next_Item_ById()
    {
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildSampleTree())
            .Add(x => x.CurrentPath, "docs"));

        var firstRow = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        firstRow.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        // The newly active row (notes.txt, index 1) must have been focused by id.
        var nextRow = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("notes.txt"));
        var nextId = nextRow.GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(nextId));
        cut.WaitForAssertion(() => Assert.Contains(nextId!, interop.FocusElementCalls));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #129: the file-list container is the preventDefault registration target
    // (stable id) and Space still selects the row. The native page-scroll
    // suppression is a JS-only effect that bUnit cannot observe; we assert the
    // wiring surface (the container id the keys are registered against) plus the
    // preserved Space-select behaviour.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_FileList_HasStableId_And_Space_Still_Selects()
    {
        FileSystemNode? selected = null;
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildSampleTree())
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.SelectedNodeChanged, EventCallback.Factory.Create<FileSystemNode?>(this, n => selected = n)));

        // The scroll container (the right pane that wraps the table) carries a
        // stable id — the element the preventDefault keys are registered against.
        var table = cut.Find("[role='grid']");
        var container = table.ParentElement;
        Assert.NotNull(container);
        Assert.False(string.IsNullOrEmpty(container!.GetAttribute("id")));

        // Space on a row still selects (preventDefault must suppress scroll, not the
        // selection itself).
        var row = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        row.KeyDown(new KeyboardEventArgs { Key = " " });

        Assert.NotNull(selected);
        Assert.Equal("report", selected!.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Folder tree keyboard navigation (WCAG 2.1.1) — the tree pane had ARIA
    // (aria-expanded/aria-selected/aria-level) but no tabindex and no
    // @onkeydown: keyboard/screen-reader users could not reach or navigate it
    // at all. The fix mirrors the file list's roving-tabindex pattern above
    // (keyed by node Id, since the visible order reshapes on expand/collapse)
    // plus TreeViewNode.razor's WAI-ARIA semantics: ArrowRight expands a
    // collapsed folder or descends into an already-expanded one; ArrowLeft
    // collapses an expanded folder or ascends to its parent; Home/End jump to
    // the first/last VISIBLE node; Enter/Space navigate into the folder.
    // ──────────────────────────────────────────────────────────────────────────

    // Two root folders, one with a nested folder child (Reports) plus a file
    // sibling (notes.txt — must NOT appear in the tree, only the file list),
    // the other with an empty nested folder (Vacation). Enough shape to
    // exercise expand/descend, collapse/ascend, and Home/End across siblings.
    private static List<FileSystemNode> BuildTreeNavigationSample() =>
    [
        new FileSystemNode
        {
            Id = "documents",
            Name = "Documents",
            IsFolder = true,
            Children =
            [
                new FileSystemNode
                {
                    Id = "reports",
                    Name = "Reports",
                    IsFolder = true,
                    Children = [new FileSystemNode { Id = "q1", Name = "q1.pdf", IsFolder = false, Size = 1_024 }]
                },
                new FileSystemNode { Id = "notes", Name = "notes.txt", IsFolder = false, Size = 512 }
            ]
        },
        new FileSystemNode
        {
            Id = "pictures",
            Name = "Pictures",
            IsFolder = true,
            Children = [new FileSystemNode { Id = "vacation", Name = "Vacation", IsFolder = true, Children = [] }]
        }
    ];

    // Treeitems carry a deterministic per-node id (TreeItemId: "{treeId}-item-{nodeId}"),
    // so an id-suffix selector picks out exactly ONE node even once it has an
    // expanded child nested inside its own DOM subtree — unlike a TextContent
    // match, which would also pick up the (nested) child's name.
    private static AngleSharp.Dom.IElement TreeItem(IRenderedComponent<Lumeo.FileManager> cut, string nodeId)
        => cut.Find($"[id$='-item-{nodeId}']");

    [Fact]
    public void FileManager_Tree_Uses_Roving_Tabindex_Initially()
    {
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildTreeNavigationSample()));

        // Nothing is expanded yet, so only the two root folders are visible
        // treeitems. Pre-fix EVERY treeitem had no tabindex attribute at all
        // (mouse-only); the fix puts exactly one in the tab sequence.
        var treeItems = cut.FindAll("[role='treeitem']");
        Assert.Equal(2, treeItems.Count);
        Assert.Equal(1, treeItems.Count(t => t.GetAttribute("tabindex") == "0"));

        Assert.Equal("0", TreeItem(cut, "documents").GetAttribute("tabindex"));
        Assert.Equal("-1", TreeItem(cut, "pictures").GetAttribute("tabindex"));
    }

    [Fact]
    public void FileManager_Tree_ArrowDown_Then_ArrowUp_MovesRovingTabStop()
    {
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildTreeNavigationSample()));

        TreeItem(cut, "documents").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Equal(1, cut.FindAll("[role='treeitem']").Count(t => t.GetAttribute("tabindex") == "0"));
        Assert.Equal("-1", TreeItem(cut, "documents").GetAttribute("tabindex"));
        Assert.Equal("0", TreeItem(cut, "pictures").GetAttribute("tabindex"));

        // ArrowUp moves it back.
        TreeItem(cut, "pictures").KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        Assert.Equal("0", TreeItem(cut, "documents").GetAttribute("tabindex"));
        Assert.Equal("-1", TreeItem(cut, "pictures").GetAttribute("tabindex"));
    }

    [Fact]
    public void FileManager_Tree_ArrowDown_Focuses_The_Next_Treeitem_ById()
    {
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<IComponentInteropService>(interop);

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildTreeNavigationSample()));

        TreeItem(cut, "documents").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        var picturesId = TreeItem(cut, "pictures").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(picturesId));
        cut.WaitForAssertion(() => Assert.Contains(picturesId!, interop.FocusElementCalls));
    }

    [Fact]
    public void FileManager_Tree_ArrowRight_Expands_Then_Descends_Into_First_Child()
    {
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildTreeNavigationSample()));

        Assert.Equal("false", TreeItem(cut, "documents").GetAttribute("aria-expanded"));

        // First ArrowRight expands the collapsed folder and stays on it (does
        // NOT move the roving cursor yet — WAI-ARIA tree semantics).
        TreeItem(cut, "documents").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("true", TreeItem(cut, "documents").GetAttribute("aria-expanded"));
        Assert.Equal("0", TreeItem(cut, "documents").GetAttribute("tabindex"));
        // Only the folder child (Reports) is now visible — the sibling FILE
        // (notes.txt) must never appear as a treeitem.
        var afterExpand = cut.FindAll("[role='treeitem']");
        Assert.Equal(3, afterExpand.Count);
        Assert.Equal("2", TreeItem(cut, "reports").GetAttribute("aria-level"));
        Assert.DoesNotContain(afterExpand, t => t.TextContent.Contains("notes.txt"));

        // Second ArrowRight on the now-expanded folder descends into its
        // first (folder) child.
        TreeItem(cut, "documents").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("0", TreeItem(cut, "reports").GetAttribute("tabindex"));
        Assert.Equal("-1", TreeItem(cut, "documents").GetAttribute("tabindex"));
    }

    [Fact]
    public void FileManager_Tree_ArrowLeft_Collapses_Then_Ascends_To_Parent()
    {
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildTreeNavigationSample()));

        // Expand Documents and descend onto Reports (mirrors the ArrowRight test).
        TreeItem(cut, "documents").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        TreeItem(cut, "documents").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // Reports has no folder children of its own (only a file), so ArrowLeft
        // on it ascends straight to its parent, Documents.
        TreeItem(cut, "reports").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.Equal("0", TreeItem(cut, "documents").GetAttribute("tabindex"));
        Assert.Equal("true", TreeItem(cut, "documents").GetAttribute("aria-expanded")); // still expanded — only the cursor moved
        Assert.Equal("-1", TreeItem(cut, "reports").GetAttribute("tabindex"));

        // ArrowLeft again, now focused on the expanded Documents, collapses it
        // (stays on the same node) — Reports drops out of the tree entirely.
        TreeItem(cut, "documents").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        var afterCollapse = cut.FindAll("[role='treeitem']");
        Assert.Equal(2, afterCollapse.Count);
        Assert.Equal("false", TreeItem(cut, "documents").GetAttribute("aria-expanded"));
        Assert.Equal("0", TreeItem(cut, "documents").GetAttribute("tabindex"));
    }

    [Fact]
    public void FileManager_Tree_Home_And_End_JumpToFirstAndLastVisibleNode()
    {
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildTreeNavigationSample()));

        // Expand Documents so the visible order is [Documents, Reports, Pictures].
        TreeItem(cut, "documents").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        TreeItem(cut, "documents").KeyDown(new KeyboardEventArgs { Key = "End" });
        Assert.Equal("0", TreeItem(cut, "pictures").GetAttribute("tabindex"));

        TreeItem(cut, "pictures").KeyDown(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal("0", TreeItem(cut, "documents").GetAttribute("tabindex"));
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void FileManager_Tree_EnterOrSpace_NavigatesIntoFolder(string key)
    {
        string? navigatedPath = "unset";
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildTreeNavigationSample())
            .Add(x => x.CurrentPathChanged, EventCallback.Factory.Create<string?>(this, path => navigatedPath = path)));

        TreeItem(cut, "pictures").KeyDown(new KeyboardEventArgs { Key = key });

        Assert.Equal("pictures", navigatedPath);
    }
}
