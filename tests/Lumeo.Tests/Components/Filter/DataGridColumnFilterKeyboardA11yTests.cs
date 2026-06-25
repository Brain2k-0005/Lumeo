using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Filter;

/// <summary>
/// Battle-test regressions for the DataGrid column-filter popover ("Filter"
/// component) keyboard / a11y class:
///
/// #12 (medium) — Arrow/Space/Enter keys inside the Number and Date filter
///   inputs leaked up to the host &lt;th&gt;'s keydown handler and moved the
///   grid's roving focus. Only the Text fallback input carried
///   <c>@onkeydown:stopPropagation</c>; the Number/Date inputs did not. The fix
///   adds <c>@onkeydown:stopPropagation="true"</c> to those inputs so the event
///   is consumed at the input and never reaches the surrounding grid handler.
///
/// #65 (low) — the operator &lt;Select&gt; combobox in the popover had no
///   accessible name (no aria-label / aria-labelledby), so screen readers
///   announced it as an unlabeled "combobox". The fix adds an aria-label
///   (Filter.Operator) to the SelectTrigger button.
///
/// bUnit honors the <c>:stopPropagation</c> flag while bubbling: when the event
/// is stopped at an element that has no own handler for it, bUnit reports a
/// <see cref="MissingEventHandlerException"/> instead of reaching the ancestor
/// handler — exactly the signal we assert on for the propagation fix.
/// </summary>
public class DataGridColumnFilterKeyboardA11yTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridColumnFilterKeyboardA11yTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    // Render the column filter wrapped in a host element that carries an
    // @onkeydown handler — this stands in for the real DataGridHeaderCell <th>
    // (which has @onkeydown="HandleKeyDown"). If a filter input lets the keydown
    // bubble, the host handler fires and flips _hostKeyReceived; if the input
    // stops propagation (the fix), bUnit raises MissingEventHandlerException and
    // the host handler is never reached.
    private IRenderedComponent<IComponent> RenderFilterInHost(
        DataGridColumn<Row> column, Action onHostKey)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "host-th");
            builder.AddAttribute(2, "onkeydown",
                EventCallback.Factory.Create<KeyboardEventArgs>(this, _ => onHostKey()));
            builder.OpenComponent<DataGridColumnFilter<Row>>(3);
            builder.AddAttribute(4, "Column", column);
            builder.AddAttribute(5, "OnApply",
                EventCallback.Factory.Create<FilterDescriptor?>(this, _ => { }));
            builder.CloseComponent();
            builder.CloseElement();
        });
    }

    private IRenderedComponent<DataGridColumnFilter<Row>> RenderFilter(DataGridColumn<Row> column)
    {
        return _ctx.Render<DataGridColumnFilter<Row>>(b =>
        {
            b.OpenComponent<DataGridColumnFilter<Row>>(0);
            b.AddAttribute(1, "Column", column);
            b.AddAttribute(2, "OnApply",
                EventCallback.Factory.Create<FilterDescriptor?>(this, _ => { }));
            b.CloseComponent();
        });
    }

    // ── #12 — Number / Date inputs must not leak keydown to the host ──────────

    [Fact]
    public void Number_Filter_Input_Stops_Keydown_From_Reaching_Host()
    {
        var column = new DataGridColumn<Row>
        {
            Field = "Id", Title = "ID", Filterable = true,
            FilterType = DataGridFilterType.Number
        };
        var hostReceived = false;
        var cut = RenderFilterInHost(column, () => hostReceived = true);

        var input = cut.Find("input[type=number]");

        // The fix stops propagation at the input (which has no own keydown
        // handler), so bUnit can't bubble to the host handler → it throws.
        // Without the fix the event would bubble and run the host handler.
        Assert.Throws<MissingEventHandlerException>(
            () => input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));
        Assert.False(hostReceived,
            "Arrow keydown in the Number filter input must not reach the host grid handler.");
    }

    [Fact]
    public void Date_Filter_Input_Stops_Keydown_From_Reaching_Host()
    {
        var column = new DataGridColumn<Row>
        {
            Field = "Created", Title = "Created", Filterable = true,
            FilterType = DataGridFilterType.Date
        };
        var hostReceived = false;
        var cut = RenderFilterInHost(column, () => hostReceived = true);

        var input = cut.Find("input[type=date]");

        Assert.Throws<MissingEventHandlerException>(
            () => input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));
        Assert.False(hostReceived,
            "Arrow keydown in the Date filter input must not reach the host grid handler.");
    }

    [Fact]
    public void Text_Filter_Input_Still_Stops_Keydown_From_Reaching_Host()
    {
        // Guard the pre-existing Text behaviour so the fix to Number/Date didn't
        // regress the one input that was already correct.
        var column = new DataGridColumn<Row>
        {
            Field = "Name", Title = "Name", Filterable = true,
            FilterType = DataGridFilterType.Text
        };
        var hostReceived = false;
        var cut = RenderFilterInHost(column, () => hostReceived = true);

        var input = cut.Find("input[type=text]");

        Assert.Throws<MissingEventHandlerException>(
            () => input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));
        Assert.False(hostReceived);
    }

    // ── #65 — operator combobox must have an accessible name ──────────────────

    [Fact]
    public void Operator_Select_Combobox_Has_Accessible_Name()
    {
        var column = new DataGridColumn<Row>
        {
            Field = "Id", Title = "ID", Filterable = true,
            FilterType = DataGridFilterType.Number
        };

        var cut = RenderFilter(column);

        // SelectTrigger renders <button role="combobox" ...> and splats
        // additional attributes (incl. aria-label) onto it.
        var combobox = cut.Find("[role=combobox]");
        var ariaLabel = combobox.GetAttribute("aria-label");

        Assert.False(string.IsNullOrWhiteSpace(ariaLabel),
            "The operator combobox must carry an aria-label so it isn't announced as an unlabeled control.");
    }
}
