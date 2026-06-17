using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.FileManager;

public class FileManagerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileManagerTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ──────────────────────────────────────────────────────────────────────────
    // Sample tree
    // ──────────────────────────────────────────────────────────────────────────

    private static List<FileSystemNode> BuildSampleTree() =>
    [
        new FileSystemNode
        {
            Id = "docs",
            Name = "Documents",
            IsFolder = true,
            Children =
            [
                new FileSystemNode { Id = "report", Name = "report.pdf", IsFolder = false, Size = 204_800, Modified = new DateTime(2024, 3, 15) },
                new FileSystemNode { Id = "notes",  Name = "notes.txt",  IsFolder = false, Size = 1_024,   Modified = new DateTime(2024, 4, 1) },
                new FileSystemNode
                {
                    Id = "archive",
                    Name = "Archive",
                    IsFolder = true,
                    Children =
                    [
                        new FileSystemNode { Id = "old-report", Name = "old-report.pdf", IsFolder = false, Size = 102_400 }
                    ]
                }
            ]
        },
        new FileSystemNode
        {
            Id = "pics",
            Name = "Pictures",
            IsFolder = true,
            Children =
            [
                new FileSystemNode { Id = "photo1", Name = "photo.jpg", IsFolder = false, Size = 3_145_728, Modified = new DateTime(2024, 5, 10) }
            ]
        },
        new FileSystemNode { Id = "readme", Name = "README.md", IsFolder = false, Size = 512 }
    ];

    // ──────────────────────────────────────────────────────────────────────────
    // Test 1: renders with sample tree — folders visible in tree pane
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_Renders_Folders_In_Tree_Pane()
    {
        var tree = BuildSampleTree();

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree));

        // The left pane (role="tree") should exist
        Assert.NotNull(cut.Find("[role='tree']"));

        // Folder names should be in the tree pane markup
        var markup = cut.Markup;
        Assert.Contains("Documents", markup);
        Assert.Contains("Pictures", markup);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 2: navigating into a folder shows its contents
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_NavigatingIntoFolder_ShowsFolderContents()
    {
        var tree = BuildSampleTree();
        string? navigatedPath = null;

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPathChanged, EventCallback.Factory.Create<string?>(this, v => navigatedPath = v)));

        // Simulate setting CurrentPath to the "docs" folder — re-render with new params
        cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPathChanged, EventCallback.Factory.Create<string?>(this, v => navigatedPath = v))
            .Add(x => x.CurrentPath, "docs"));

        var markup = cut.Markup;
        // Files inside Documents should appear in the file list
        Assert.Contains("report.pdf", markup);
        Assert.Contains("notes.txt", markup);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 3: breadcrumb reflects the path
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_Breadcrumb_Reflects_CurrentPath()
    {
        var tree = BuildSampleTree();

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs"));

        // 2.2.0: nav aria-label is routed through L["FileManager.Path"]
        // which resolves to "File manager path" (capitalised) in the
        // default English locale.
        var breadcrumb = cut.Find("nav[aria-label='File manager path']");
        Assert.Contains("Documents", breadcrumb.TextContent);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 4: clicking a file selects it (SelectedNodeChanged fires)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_ClickingFile_FiresSelectedNodeChanged()
    {
        var tree = BuildSampleTree();
        FileSystemNode? selected = null;

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.SelectedNodeChanged, EventCallback.Factory.Create<FileSystemNode?>(this, n => selected = n)));

        // Find the row for report.pdf and click it
        var rows = cut.FindAll("[role='row']");
        var reportRow = rows.FirstOrDefault(r => r.TextContent.Contains("report.pdf"));
        Assert.NotNull(reportRow);
        reportRow.Click();

        Assert.NotNull(selected);
        Assert.Equal("report", selected!.Id);
        Assert.False(selected.IsFolder);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 5: empty folder shows the empty state message
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_EmptyFolder_ShowsEmptyState()
    {
        var tree = new List<FileSystemNode>
        {
            new FileSystemNode
            {
                Id = "empty-folder",
                Name = "Empty Folder",
                IsFolder = true,
                Children = [] // explicitly empty
            }
        };

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "empty-folder"));

        Assert.Contains("This folder is empty", cut.Markup);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 6: ViewMode.Grid renders grid tiles instead of table rows
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_GridViewMode_RendersTilesNotTableRows()
    {
        var tree = BuildSampleTree();

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.ViewMode, Lumeo.FileManager.FileManagerViewMode.Grid));

        // Grid uses role="listbox" not role="grid"
        Assert.NotEmpty(cut.FindAll("[role='listbox']"));
        Assert.Empty(cut.FindAll("[role='grid']"));

        // Individual tiles use role="option"
        var tiles = cut.FindAll("[role='option']");
        Assert.NotEmpty(tiles);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 7: root level shows all top-level nodes
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_RootLevel_ShowsAllTopLevelNodes()
    {
        var tree = BuildSampleTree();

        // No CurrentPath set → shows root level
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree));

        var markup = cut.Markup;
        // Root has: Documents (folder), Pictures (folder), README.md (file)
        Assert.Contains("Documents", markup);
        Assert.Contains("Pictures", markup);
        Assert.Contains("README.md", markup);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 8: rename callback fires with correct args
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_Rename_Fires_WithCorrectArgs()
    {
        var tree = BuildSampleTree();
        (FileSystemNode Node, string NewName)? renameArgs = null;

        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, tree)
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.OnRename, EventCallback.Factory.Create<(FileSystemNode, string)>(this, v => renameArgs = v)));

        // Right-click to open the context menu on report.pdf row
        var reportRow = cut.FindAll("[role='row']")
            .FirstOrDefault(r => r.TextContent.Contains("report.pdf"));
        Assert.NotNull(reportRow);

        // Trigger context menu
        reportRow.TriggerEvent("oncontextmenu", new Microsoft.AspNetCore.Components.Web.MouseEventArgs
        {
            ClientX = 100, ClientY = 100
        });

        // Find the rename button in the context menu
        var renameBtn = cut.FindAll("[role='menu'] button")
            .FirstOrDefault(b => b.TextContent.Contains("Rename"));
        Assert.NotNull(renameBtn);
        renameBtn.Click();

        // Now the input for rename should be visible
        var renameInput = cut.Find("input[type='text']");
        Assert.NotNull(renameInput);

        // Type a new name and commit with Enter
        renameInput.Input("renamed-report.pdf");
        renameInput.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        // Callback should have fired
        Assert.NotNull(renameArgs);
        Assert.Equal("report", renameArgs!.Value.Node.Id);
        Assert.Equal("renamed-report.pdf", renameArgs.Value.NewName);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #314: swapping Root while keeping CurrentPath must show the NEW children
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_RootSwap_SameCurrentPath_ShowsNewChildren()
    {
        // First tree: "docs" contains report.pdf / notes.txt.
        var treeA = BuildSampleTree();
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, treeA)
            .Add(x => x.CurrentPath, "docs"));

        Assert.Contains("report.pdf", cut.Markup);

        // Swap in a FRESH tree whose "docs" folder holds different children,
        // keeping CurrentPath == "docs". The old early-return matched on Id and
        // kept the stale node; now the new children must render.
        var treeB = new List<FileSystemNode>
        {
            new FileSystemNode
            {
                Id = "docs",
                Name = "Documents",
                IsFolder = true,
                Children =
                [
                    new FileSystemNode { Id = "brandnew", Name = "brand-new.txt", IsFolder = false, Size = 10 }
                ]
            }
        };

        cut.Render(p => p
            .Add(x => x.Root, treeB)
            .Add(x => x.CurrentPath, "docs"));

        var markup = cut.Markup;
        Assert.Contains("brand-new.txt", markup);
        Assert.DoesNotContain("report.pdf", markup); // stale child must be gone
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #314: rows are keyboard-operable (Enter opens, Space selects)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FileManager_Row_Has_Tabindex_For_Keyboard_Focus()
    {
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildSampleTree())
            .Add(x => x.CurrentPath, "docs"));

        var row = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        Assert.Equal("0", row.GetAttribute("tabindex"));
    }

    [Fact]
    public void FileManager_Space_On_Row_Selects_The_Node()
    {
        FileSystemNode? selected = null;
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildSampleTree())
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.SelectedNodeChanged, EventCallback.Factory.Create<FileSystemNode?>(this, n => selected = n)));

        var row = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("report.pdf"));
        row.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = " " });

        Assert.NotNull(selected);
        Assert.Equal("report", selected!.Id);
    }

    [Fact]
    public void FileManager_Enter_On_Folder_Row_Navigates_Into_It()
    {
        string? navigated = null;
        var cut = _ctx.Render<Lumeo.FileManager>(p => p
            .Add(x => x.Root, BuildSampleTree())
            .Add(x => x.CurrentPath, "docs")
            .Add(x => x.CurrentPathChanged, EventCallback.Factory.Create<string?>(this, v => navigated = v)));

        // The "Archive" sub-folder appears as a row inside docs.
        var row = cut.FindAll("[role='row']").First(r => r.TextContent.Contains("Archive"));
        row.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("archive", navigated);
    }
}
