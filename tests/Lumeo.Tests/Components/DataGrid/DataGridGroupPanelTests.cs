using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for the DataGrid group-panel feature (rc.41).
///
/// rc.41 fixed three bugs:
///   1. Drag-to-group panel drop zone was not accepting drops correctly.
///   2. Clear + re-add was broken — the &lt;select&gt; native DOM value stayed on
///      the previously-picked option so the placeholder never reappeared, making
///      subsequent adds silently fail (the key-on-count fix).
///   3. Per-chip Remove button left stale expansion state, causing the next
///      re-group to render empty groups.
///
/// These tests exercise the panel entirely through observable HTML — no private
/// field reflection — matching the bUnit style in adjacent test files.
/// </summary>
public class DataGridGroupPanelTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridGroupPanelTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ---------------------------------------------------------------------------
    // Test data
    // ---------------------------------------------------------------------------

    private record Employee(int Id, string Name, string Department, string Status);

    private static List<Employee> GetEmployees() => new()
    {
        new(1, "Alice",   "Engineering", "Active"),
        new(2, "Bob",     "Engineering", "Active"),
        new(3, "Carol",   "Marketing",   "Active"),
        new(4, "Dan",     "Marketing",   "Inactive"),
        new(5, "Eve",     "HR",          "Active"),
    };

    /// <summary>
    /// 2 groupable columns (Department, Status) + 1 non-groupable (Name) + 1 non-groupable (Id).
    /// </summary>
    private static List<DataGridColumn<Employee>> GetColumns() => new()
    {
        new() { Field = "Id",         Title = "ID" },
        new() { Field = "Name",       Title = "Name" },
        new() { Field = "Department", Title = "Department", Groupable = true },
        new() { Field = "Status",     Title = "Status",     Groupable = true },
    };

    // ---------------------------------------------------------------------------
    // Helper
    // ---------------------------------------------------------------------------

    private IRenderedComponent<DataGrid<Employee>> RenderGrid(
        bool showGroupPanel = true,
        IReadOnlyList<string>? groupByFields = null)
    {
        return _ctx.Render<DataGrid<Employee>>(p =>
        {
            p.Add(x => x.Items, GetEmployees());
            p.Add(x => x.Columns, GetColumns());
            p.Add(x => x.ShowGroupPanel, showGroupPanel);
            if (groupByFields is not null)
                p.Add(x => x.GroupByFields, groupByFields);
        });
    }

    // ===========================================================================
    // 1. GroupPanel_NotShownByDefault
    // ===========================================================================

    [Fact]
    public void GroupPanel_NotShownByDefault()
    {
        var cut = RenderGrid(showGroupPanel: false);

        // The panel div has data-slot="datagrid-group-panel" — it must not exist.
        var panels = cut.FindAll("[data-slot=\"datagrid-group-panel\"]");
        Assert.Empty(panels);
    }

    // ===========================================================================
    // 2. GroupPanel_ShownWhenEnabled
    // ===========================================================================

    [Fact]
    public void GroupPanel_ShownWhenEnabled()
    {
        var cut = RenderGrid(showGroupPanel: true);

        // Panel element must exist.
        var panel = cut.Find("[data-slot=\"datagrid-group-panel\"]");
        Assert.NotNull(panel);

        // With no active grouping the placeholder hint must be visible.
        Assert.Contains("Drag a Groupable column header here, or use the dropdown", cut.Markup);
    }

    // ===========================================================================
    // 3. GroupPanel_ShowsAddDropdownWithGroupableColumnsOnly
    // ===========================================================================

    [Fact]
    public void GroupPanel_ShowsAddDropdownWithGroupableColumnsOnly()
    {
        // 4 columns total, only 2 are Groupable.
        var cut = RenderGrid(showGroupPanel: true);

        // 2.2.0: the add-level UI is now a DropdownMenu trigger instead of a
        // native <select>. Click the trigger to open the menu, then count
        // the rendered menu items.
        var trigger = cut.Find("[data-slot=\"datagrid-group-add-trigger\"]");
        Assert.NotNull(trigger);
        trigger.Click();

        var items = cut.FindAll("[role=\"menu\"] [role=\"menuitem\"]");
        // Only the 2 groupable columns (no placeholder option anymore — the
        // trigger itself displays "+ Add group level").
        Assert.Equal(2, items.Count);
    }

    // ===========================================================================
    // 4. AddGroupField_AppendsToRuntimeFields
    // ===========================================================================

    [Fact]
    public void AddGroupField_AppendsToRuntimeFields()
    {
        var cut = RenderGrid(showGroupPanel: true);

        // 2.2.0: open the add-level DropdownMenu, then click the Department item.
        cut.Find("[data-slot=\"datagrid-group-add-trigger\"]").Click();
        cut.Find("[role=\"menu\"] [data-group-add-field=\"Department\"]").Click();

        // A chip for "Department" should appear in the panel.
        var panel = cut.Find("[data-slot=\"datagrid-group-panel\"]");
        Assert.Contains("Department", panel.TextContent);

        // The placeholder text must be gone.
        Assert.DoesNotContain("Drag a Groupable column header here", cut.Markup);
    }

    // ===========================================================================
    // 5. AddGroupField_RejectsNonGroupableColumns
    // ===========================================================================

    [Fact]
    public void AddGroupField_RejectsNonGroupableColumns()
    {
        var cut = RenderGrid(showGroupPanel: true);

        // 2.2.0: "Name" is not Groupable. The DropdownMenu only exposes
        // groupable columns, so it can't surface "Name" to the user. We
        // verify the guard by opening the menu and confirming neither Name
        // nor Id appear as menu items.
        cut.Find("[data-slot=\"datagrid-group-add-trigger\"]").Click();

        var items = cut.FindAll("[role=\"menu\"] [role=\"menuitem\"]");
        var itemFields = items.Select(i => i.GetAttribute("data-group-add-field")).ToList();
        Assert.DoesNotContain("Name", itemFields);
        Assert.DoesNotContain("Id",   itemFields);

        // Placeholder hint still shown — no spurious chip added.
        Assert.Contains("Drag a Groupable column header here", cut.Markup);
    }

    // ===========================================================================
    // 6. RemoveGroupField_DropsChip
    // ===========================================================================

    [Fact]
    public void RemoveGroupField_DropsChip()
    {
        // Pre-seed two grouping levels via the GroupByFields parameter.
        var cut = RenderGrid(showGroupPanel: true,
            groupByFields: new[] { "Department", "Status" });

        // Both chips must be present.
        var panel = cut.Find("[data-slot=\"datagrid-group-panel\"]");
        Assert.Contains("Department", panel.TextContent);
        Assert.Contains("Status",     panel.TextContent);

        // Click the "Remove grouping" button for Department.
        // Each chip has exactly one remove button (title="Remove grouping").
        // The first one belongs to the first chip (Department).
        var removeButtons = cut.FindAll("button[title=\"Remove grouping\"]");
        Assert.True(removeButtons.Count >= 1, "Expected at least one Remove grouping button");
        removeButtons[0].Click();

        // After removal only Status should remain.
        // NOTE: assert against the chip-span structure, NOT panel.TextContent —
        // the removed field reappears in the "+ Add group level" dropdown
        // (which is part of the same panel), so a broad string search would
        // falsely fail. Each chip is a <span> with a remove button inside;
        // count them and inspect their text.
        var remainingChips = cut.FindAll("[data-slot=\"datagrid-group-panel\"] span.inline-flex.items-center.gap-1.rounded.bg-card");
        Assert.Single(remainingChips);
        Assert.Contains("Status", remainingChips[0].TextContent);
        Assert.DoesNotContain("Department", remainingChips[0].TextContent);
    }

    // ===========================================================================
    // 7. ClearGroupFields_EmptiesAllChips
    // ===========================================================================

    [Fact]
    public void ClearGroupFields_EmptiesAllChips()
    {
        var cut = RenderGrid(showGroupPanel: true,
            groupByFields: new[] { "Department", "Status" });

        // Both chips present before clear.
        Assert.Contains("Department", cut.Markup);
        Assert.Contains("Status",     cut.Markup);

        // Click the "Clear all grouping" button.
        var clearBtn = cut.Find("button[title=\"Clear all grouping\"]");
        clearBtn.Click();

        // Panel should revert to the placeholder state — no chips.
        Assert.Contains("Drag a Groupable column header here", cut.Markup);
        Assert.DoesNotContain("Remove grouping", cut.Markup);
    }

    // ===========================================================================
    // 8. AddAfterClear_StillWorks  (rc.41 regression — the select reset bug)
    //
    // The bug: after ClearGroupFields, the <select>'s native DOM value was stuck
    // on the last-picked option. Picking the same field again sent an empty change
    // event (browser de-duplication) so the grouping was never re-applied.
    // rc.41 fixed this by keying the <select> on _runtimeGroupFields.Count so a
    // fresh DOM element is created every time, resetting the native value to "".
    // ===========================================================================

    [Fact]
    public void AddAfterClear_StillWorks()
    {
        var cut = RenderGrid(showGroupPanel: true);

        // 2.2.0: with DropdownMenu replacing the native <select>, the
        // "value stuck after clear" bug from rc.41 can't recur structurally
        // (the menu has no persistent value). We keep this test as a
        // behavioural guard: add → clear → add must still work end-to-end.

        // Step 1: Add Department.
        cut.Find("[data-slot=\"datagrid-group-add-trigger\"]").Click();
        cut.Find("[role=\"menu\"] [data-group-add-field=\"Department\"]").Click();
        Assert.Contains("Department", cut.Find("[data-slot=\"datagrid-group-panel\"]").TextContent);

        // Step 2: Clear all.
        var clearBtn = cut.Find("button[title=\"Clear all grouping\"]");
        clearBtn.Click();
        Assert.Contains("Drag a Groupable column header here", cut.Markup);

        // Step 3: Add Department again — must succeed.
        cut.Find("[data-slot=\"datagrid-group-add-trigger\"]").Click();
        cut.Find("[role=\"menu\"] [data-group-add-field=\"Department\"]").Click();

        // The chip must appear — grouping is active again.
        var panelAfterReadd = cut.Find("[data-slot=\"datagrid-group-panel\"]");
        Assert.Contains("Department", panelAfterReadd.TextContent);
        Assert.DoesNotContain("Drag a Groupable column header here", cut.Markup);
    }
}
