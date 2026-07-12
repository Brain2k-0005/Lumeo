using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.PivotGrid;

/// <summary>
/// SPECIAL product fix: PivotGrid's drill-down data cells (&lt;td role="cell"
/// @onclick="OnCellClickHandler"&gt;) had NO tabindex and no key activation —
/// verified against the pre-fix source: click-through was genuinely
/// mouse-only, unlike the row-collapse toggle (already a native &lt;button&gt;,
/// fully keyboard-accessible for free).
///
/// Fix: the cell now carries tabindex="0" and an Enter/Space-activating
/// @onkeydown, but ONLY when OnCellClick is actually bound — mirroring the
/// Card/Steps conditionally-interactive contract, so a display-only pivot
/// (no OnCellClick) gains zero phantom Tab stops across a table that can
/// easily have hundreds of cells.
/// </summary>
public class PivotGridKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PivotGridKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Sale(string Region, string Country, int Year, decimal Amount);

    private static List<Sale> Data() => new()
    {
        new("North", "USA",     2023, 100m),
        new("North", "Canada",  2023,  50m),
        new("North", "USA",     2024, 200m),
        new("South", "Brazil",  2023,  30m),
        new("South", "Brazil",  2024,  70m),
    };

    // Two levels (Region -> Country) so Region rows are real GroupHeader nodes
    // with a collapse toggle — a single-level RowFields list would make every
    // row a leaf with no group header/button to press at all.
    private static IReadOnlyList<L.PivotField<Sale>> RowFields() => new List<L.PivotField<Sale>>
    {
        new("Region", s => s.Region),
        new("Country", s => s.Country),
    };

    private static IReadOnlyList<L.PivotField<Sale>> ColumnFields() => new List<L.PivotField<Sale>>
    {
        new("Year", s => s.Year),
    };

    private static IReadOnlyList<L.PivotMeasure<Sale>> SumMeasure() => new List<L.PivotMeasure<Sale>>
    {
        new("Amount", s => s.Amount, L.PivotAggregate.Sum),
    };

    private IRenderedComponent<L.PivotGrid<Sale>> RenderWithColumnField(
        Action<ComponentParameterCollectionBuilder<L.PivotGrid<Sale>>>? extra = null)
        => _ctx.Render<L.PivotGrid<Sale>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.RowFields, RowFields());
            p.Add(g => g.ColumnFields, ColumnFields());
            p.Add(g => g.Measures, SumMeasure());
            extra?.Invoke(p);
        });

    private IRenderedComponent<L.PivotGrid<Sale>> RenderWithoutColumnField(
        Action<ComponentParameterCollectionBuilder<L.PivotGrid<Sale>>>? extra = null)
        => _ctx.Render<L.PivotGrid<Sale>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.RowFields, RowFields());
            p.Add(g => g.ColumnFields, Array.Empty<L.PivotField<Sale>>());
            p.Add(g => g.Measures, SumMeasure());
            extra?.Invoke(p);
        });

    // --- Row-collapse toggle: native <button>, already fully accessible ---

    [Fact]
    public void Collapse_Toggle_Is_A_Native_Button_With_No_Blazor_Keydown_Wiring()
    {
        // The toggle is a real <button @onclick="..."> — no @onkeydown at all,
        // because none is needed: the browser's own Enter/Space-activates-
        // button semantics reach the SAME @onclick handler a mouse click does.
        // bUnit has no browser to synthesize that native translation (firing
        // KeyDown on an element with no Blazor keydown listener throws
        // MissingEventHandlerException), so the correct, non-tautological
        // assertion here is that clicking (what the native key translates to)
        // flips the state — proving the button, not a keydown handler, is
        // what makes this keyboard-accessible.
        var cut = RenderWithColumnField();

        var toggle = cut.FindAll("button[aria-expanded]").First();
        Assert.Equal("button", toggle.TagName.ToLowerInvariant());
        Assert.False(toggle.HasAttribute("blazor:onkeydown"));
        Assert.Equal("true", toggle.GetAttribute("aria-expanded"));

        toggle.Click();

        Assert.Equal("false", cut.FindAll("button[aria-expanded]").First().GetAttribute("aria-expanded"));
    }

    // --- Drill-down cells: conditionally interactive (the SPECIAL fix) ---

    [Fact]
    public void Cell_Is_Not_Focusable_When_OnCellClick_Is_Not_Bound()
    {
        // tabindex is the load-bearing signal for real keyboard reachability
        // (matches how NumberInput/HandleKeyDown etc. keep a single always-
        // wired handler that internally no-ops when inert, rather than
        // branching the whole element tree). No tabindex -> no Tab stop -> a
        // keyboard user can never focus the cell to trigger the handler.
        var cut = RenderWithColumnField();

        var cell = cut.FindAll("td[role='cell']").First();
        Assert.Null(cell.GetAttribute("tabindex"));
    }

    [Fact]
    public void Enter_Does_Nothing_When_OnCellClick_Is_Not_Bound()
    {
        // Even though the keydown handler is always wired (see the tabindex
        // test's comment), OnCellClickHandler's own HasDelegate guard makes
        // it a true no-op with nothing bound — no exception, no callback.
        var cut = RenderWithColumnField();

        var ex = Record.Exception(() => cut.FindAll("td[role='cell']").First().KeyDown("Enter"));

        Assert.Null(ex);
    }

    [Fact]
    public void Cell_Is_Focusable_When_OnCellClick_Is_Bound()
    {
        var cut = RenderWithColumnField(p => p.Add(g => g.OnCellClick, _ => { }));

        var cell = cut.FindAll("td[role='cell']").First();
        Assert.Equal("0", cell.GetAttribute("tabindex"));
    }

    [Fact]
    public void Enter_On_A_Bound_Cell_Fires_OnCellClick_With_The_Same_Args_As_A_Click()
    {
        L.PivotCellClickArgs? viaClick = null;
        var cutClick = RenderWithColumnField(p => p.Add(g => g.OnCellClick, args => viaClick = args));
        cutClick.FindAll("td[role='cell']").First().Click();

        L.PivotCellClickArgs? viaEnter = null;
        var cutEnter = RenderWithColumnField(p => p.Add(g => g.OnCellClick, args => viaEnter = args));
        cutEnter.FindAll("td[role='cell']").First().KeyDown("Enter");

        Assert.NotNull(viaClick);
        Assert.NotNull(viaEnter);
        Assert.Equal(viaClick!.RowKeys, viaEnter!.RowKeys);
        Assert.Equal(viaClick.ColumnKeys, viaEnter.ColumnKeys);
        Assert.Equal(viaClick.Measure, viaEnter.Measure);
    }

    [Fact]
    public void Space_On_A_Bound_Cell_Fires_OnCellClick()
    {
        var fired = false;
        var cut = RenderWithColumnField(p => p.Add(g => g.OnCellClick, _ => fired = true));

        cut.FindAll("td[role='cell']").First().KeyDown(" ");

        Assert.True(fired);
    }

    [Fact]
    public void Unhandled_Key_On_A_Bound_Cell_Does_Not_Fire_OnCellClick()
    {
        var fired = false;
        var cut = RenderWithColumnField(p => p.Add(g => g.OnCellClick, _ => fired = true));

        cut.FindAll("td[role='cell']").First().KeyDown("a");

        Assert.False(fired);
    }

    // --- No-column-field branch shares the same fix ---

    [Fact]
    public void NoColumnField_Cell_Is_Focusable_And_Enter_Activates_When_Bound()
    {
        var fired = false;
        var cut = RenderWithoutColumnField(p => p.Add(g => g.OnCellClick, _ => fired = true));

        var cell = cut.FindAll("td[role='cell']").First();
        Assert.Equal("0", cell.GetAttribute("tabindex"));

        cell.KeyDown("Enter");
        Assert.True(fired);
    }

    [Fact]
    public void NoColumnField_Cell_Is_Passive_When_Not_Bound()
    {
        var cut = RenderWithoutColumnField();

        var cell = cut.FindAll("td[role='cell']").First();
        Assert.Null(cell.GetAttribute("tabindex"));
    }
}
