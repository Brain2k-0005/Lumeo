using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression for battle-test wave-1 n=8 (keyboard-a11y): the grid's roving-tabindex
/// keyboard navigation (Arrow / Home / End / PageUp / PageDown / Space) must suppress
/// the browser default so those keys move the focused cell / toggle selection instead
/// of scrolling the page. Because a per-key <c>@onkeydown:preventDefault</c> directive
/// is fixed at render time and would also block Tab from leaving the grid, the fix
/// registers a key-selective native <c>preventDefault</c> listener on the table
/// (id == grid id) via <see cref="IComponentInteropService.RegisterPreventDefaultKeys"/>
/// in OnAfterRenderAsync — the same idiom Select / DateTimePicker / Calendar use.
///
/// bUnit can't observe the JS-level <c>preventDefault</c> nor real page scroll, so we
/// assert the wiring surface: the recorded <c>registerPreventDefaultKeys</c> interop
/// invocation, that it targets the grid/table id, covers exactly the nav keys (and NOT
/// Tab), and that every rule sets <c>SkipEditable</c> so the inline-edit input keeps its
/// caret/typing keys alive. DataGrid injects the concrete ComponentInteropService whose
/// interop calls land on bUnit's loose module, so they show up in JSInterop.Invocations.
/// </summary>
public class DataGridKeyboardPreventDefaultTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridKeyboardPreventDefaultTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record TestItem(int Id, string Name, decimal Amount);

    private static List<TestItem> Data() => new()
    {
        new(1, "Alice", 100m),
        new(2, "Bob", 200m),
        new(3, "Charlie", 150m),
    };

    private static List<DataGridColumn<TestItem>> Columns() => new()
    {
        new() { Field = "Id", Title = "ID", Sortable = true },
        new() { Field = "Name", Title = "Name" },
        new() { Field = "Amount", Title = "Amount", Sortable = true, Format = "C2" },
    };

    private IRenderedComponent<DataGrid<TestItem>> RenderGrid()
        => _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.Columns, Columns())
            .Add(x => x.ShowPagination, false)
            .Add(x => x.SelectionMode, DataGridSelectionMode.Multiple));

    // Filters to the TABLE's own registration by elementId (PR-353 round-13 #3): every
    // Resizable column's resize handle now ALSO calls registerPreventDefaultKeys for its
    // own ArrowLeft/ArrowRight (see DataGridAwaitedCommitRaceTests), so an unqualified
    // "the one registerPreventDefaultKeys call" assumption no longer holds — this suite
    // is specifically about the grid/table-level nav-key registration.
    private static (string ElementId, IReadOnlyList<PreventDefaultKeyRule> Rules) SingleRegistration(BunitContext ctx, string tableId)
    {
        var reg = Assert.Single(ctx.JSInterop.Invocations,
            i => i.Identifier == "registerPreventDefaultKeys" && (string)i.Arguments[0]! == tableId);
        var elementId = (string)reg.Arguments[0]!;
        var rules = Lumeo.Tests.Helpers.PreventDefaultRuleCapture.Parse(reg.Arguments[1]);
        return (elementId, rules);
    }

    [Fact]
    public void Grid_Registers_PreventDefault_On_The_Table_For_Nav_Keys()
    {
        var cut = RenderGrid();

        var tableId = cut.Find("table").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(tableId));

        var (elementId, rules) = SingleRegistration(_ctx, tableId!);

        // Registered against the table (its id IS the grid id), where cell/header
        // keydowns bubble to.
        Assert.Equal(tableId, elementId);

        var keys = rules.Select(r => r.Key).ToList();
        Assert.Contains("ArrowDown", keys);
        Assert.Contains("ArrowUp", keys);
        Assert.Contains("ArrowLeft", keys);
        Assert.Contains("ArrowRight", keys);
        Assert.Contains("Home", keys);
        Assert.Contains("End", keys);
        Assert.Contains("PageUp", keys);
        Assert.Contains("PageDown", keys);
        // Space (the modern KeyboardEvent.key) — toggles row selection, must not scroll.
        Assert.Contains(" ", keys);
    }

    [Fact]
    public void PreventDefault_Does_Not_Swallow_Tab()
    {
        // Tab must stay live so the user can move focus out of the grid; suppressing
        // it (the reason a blanket @onkeydown:preventDefault was rejected) would trap
        // keyboard focus inside the table.
        var cut = RenderGrid();
        var tableId = cut.Find("table").GetAttribute("id")!;
        var (_, rules) = SingleRegistration(_ctx, tableId);

        Assert.DoesNotContain(rules, r => r.Key == "Tab");
    }

    [Fact]
    public void Every_NavKey_Rule_Skips_Editable_So_Inline_Editor_Keeps_Caret_Keys()
    {
        // SkipEditable means the listener never suppresses a key whose target sits
        // inside an <input>/<textarea>/<select> — so cell-edit typing, caret arrows,
        // Home/End and the editor's own Tab/Enter/Escape handling are untouched.
        var cut = RenderGrid();
        var tableId = cut.Find("table").GetAttribute("id")!;
        var (_, rules) = SingleRegistration(_ctx, tableId);

        Assert.NotEmpty(rules);
        Assert.All(rules, r => Assert.True(r.SkipEditable));
    }

    [Fact]
    public async Task PreventDefault_Listener_Is_Torn_Down_On_Dispose()
    {
        // The native keydown listener must not outlive the table element. Use a
        // dedicated context (mirrors DataGridFullscreenTeardownTests) so we can
        // dispose the grid and inspect the invocations recorded up to disposal.
        using var ctx = new BunitContext();
        ctx.AddLumeoServices();

        var cut = ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.Columns, Columns())
            .Add(x => x.ShowPagination, false)
            .Add(x => x.SelectionMode, DataGridSelectionMode.Multiple));

        // Read the table's id directly off the DOM rather than assuming it's the
        // only registerPreventDefaultKeys caller — every Resizable column's resize
        // handle now ALSO registers one for its own Arrow keys (round-13 #3).
        var tableId = cut.Find("table").GetAttribute("id")!;

        var unregBefore = ctx.JSInterop.Invocations.Count(i =>
            i.Identifier == "unregisterPreventDefaultKeys"
            && (string)i.Arguments[0]! == tableId);

        await cut.Instance.DisposeAsync();

        var unregAfter = ctx.JSInterop.Invocations.Count(i =>
            i.Identifier == "unregisterPreventDefaultKeys"
            && (string)i.Arguments[0]! == tableId);

        Assert.True(unregAfter > unregBefore,
            "Disposing the grid must unregister the table's preventDefault key listener.");
    }
}
