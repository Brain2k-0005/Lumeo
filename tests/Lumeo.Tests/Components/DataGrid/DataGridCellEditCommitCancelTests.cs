using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Field report #464 finding 3 — "CellEditContext, DataGridColumnDef.Editable still absent.
/// With EditMode=Cell and a custom EditTemplate, every clicked cell stays open."
///
/// Verified NOT already fixed against current source before writing these tests:
/// <c>CellEditContext&lt;TItem&gt;</c> (src/Lumeo.DataGrid/UI/DataGrid/DataGridColumn.cs) had
/// only <c>Item</c>/<c>Column</c>/<c>Value</c>/<c>ValueChanged</c> — no <c>Commit</c>/<c>Cancel</c> —
/// and <c>DataGridColumn&lt;TItem&gt;</c> had no <c>Editable</c> member at all. A custom
/// <c>EditTemplate</c> cell had no mechanism to close itself, and <c>DataGridCell</c> tracked
/// <c>_isEditing</c> purely locally with no grid-wide "one open editor" coordination, so a
/// second cell opening never closed a first one that was still open via a custom template.
/// </summary>
public class DataGridCellEditCommitCancelTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridCellEditCommitCancelTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name, string Custom, string Locked);

    private static List<Row> Sample() => new()
    {
        new(1, "Alice", "custom-a", "locked-a"),
        new(2, "Bob", "custom-b", "locked-b"),
    };

    // A custom editor with NO Commit/Cancel wiring of its own — exactly the shape the field
    // report describes: before this fix, this cell had no way to leave edit mode at all.
    private RenderFragment<CellEditContext<Row>> UnwiredCustomEditTemplate() => ctx => builder =>
    {
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "data-testid", "custom-editor");
        builder.AddAttribute(2, "value", ctx.Value);
        builder.AddAttribute(3, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
        {
            ctx.Value = e.Value?.ToString();
            ctx.ValueChanged();
        }));
        builder.CloseElement();
    };

    // A custom editor that DOES wire Commit/Cancel through the context — the fix's intended
    // usage pattern (e.g. a "Save"/"Cancel" pair, or Enter/Escape handling of its own).
    private RenderFragment<CellEditContext<Row>> WiredCustomEditTemplate() => ctx => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "data-testid", "wired-custom-editor");
        builder.CloseElement();
        builder.OpenElement(2, "button");
        builder.AddAttribute(3, "type", "button");
        builder.AddAttribute(4, "data-testid", "commit-btn");
        builder.AddAttribute(5, "onclick", EventCallback.Factory.Create(this, async () =>
        {
            ctx.Value = "committed-value";
            ctx.ValueChanged();
            await ctx.Commit();
        }));
        builder.CloseElement();
        builder.OpenElement(6, "button");
        builder.AddAttribute(7, "type", "button");
        builder.AddAttribute(8, "data-testid", "cancel-btn");
        builder.AddAttribute(9, "onclick", EventCallback.Factory.Create(this, () => ctx.Cancel()));
        builder.CloseElement();
    };

    [Fact]
    public async Task Editable_False_Column_Never_Opens_Editor_On_Click()
    {
        var columns = new List<DataGridColumn<Row>>
        {
            new() { Field = "Name", Title = "Name" },
            new() { Field = "Locked", Title = "Locked", Editable = false },
        };

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, columns)
            .Add(x => x.EditMode, DataGridEditMode.Cell));

        // Row 0's Locked cell is the second data cell of the first row.
        var lockedCell = cut.FindAll("td[data-slot='datagrid-cell']")[1];
        await cut.InvokeAsync(() => lockedCell.Click());

        Assert.DoesNotContain("<input", cut.Markup);
    }

    [Fact]
    public async Task Editable_False_Column_Never_Opens_Editor_On_Enter()
    {
        var columns = new List<DataGridColumn<Row>>
        {
            new() { Field = "Name", Title = "Name" },
            new() { Field = "Locked", Title = "Locked", Editable = false },
        };

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, columns)
            .Add(x => x.EditMode, DataGridEditMode.Cell));

        var lockedCell = cut.FindAll("td[data-slot='datagrid-cell']")[1];
        await cut.InvokeAsync(() => lockedCell.KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        Assert.DoesNotContain("<input", cut.Markup);
    }

    [Fact]
    public async Task CustomEditTemplate_Commit_Writes_Value_And_Closes_Cell()
    {
        CellEditEventArgs<Row>? captured = null;
        var columns = new List<DataGridColumn<Row>>
        {
            new() { Field = "Custom", Title = "Custom", EditTemplate = WiredCustomEditTemplate() },
        };

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, columns)
            .Add(x => x.EditMode, DataGridEditMode.Cell)
            .Add(x => x.OnCellEdit, EventCallback.Factory.Create<CellEditEventArgs<Row>>(this, args => captured = args)));

        var cell = cut.FindAll("td[data-slot='datagrid-cell']")[0];
        await cut.InvokeAsync(() => cell.Click());
        Assert.Contains("wired-custom-editor", cut.Markup);

        var commitButton = cut.Find("[data-testid='commit-btn']");
        await cut.InvokeAsync(() => commitButton.Click());

        Assert.DoesNotContain("wired-custom-editor", cut.Markup);
        Assert.NotNull(captured);
        Assert.Equal("Custom", captured!.Field);
        Assert.Equal("custom-a", captured.OldValue);
        Assert.Equal("committed-value", captured.NewValue);
    }

    [Fact]
    public async Task CustomEditTemplate_Cancel_Discards_And_Closes_Without_Firing_OnCellEdit()
    {
        var fired = false;
        var columns = new List<DataGridColumn<Row>>
        {
            new() { Field = "Custom", Title = "Custom", EditTemplate = WiredCustomEditTemplate() },
        };

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, columns)
            .Add(x => x.EditMode, DataGridEditMode.Cell)
            .Add(x => x.OnCellEdit, EventCallback.Factory.Create<CellEditEventArgs<Row>>(this, _ => fired = true)));

        var cell = cut.FindAll("td[data-slot='datagrid-cell']")[0];
        await cut.InvokeAsync(() => cell.Click());
        Assert.Contains("wired-custom-editor", cut.Markup);

        var cancelButton = cut.Find("[data-testid='cancel-btn']");
        await cut.InvokeAsync(() => cancelButton.Click());

        Assert.DoesNotContain("wired-custom-editor", cut.Markup);
        Assert.False(fired);
    }

    [Fact]
    public async Task Opening_A_Second_Cell_Commits_And_Closes_An_Open_Custom_Editor_With_No_Commit_Wiring()
    {
        // Reproduces the exact field-report complaint: a custom EditTemplate with no
        // Commit/Cancel of its own must still be closed (and committed) when a different
        // cell is opened — the grid-wide "one open editor" lock, not the template itself,
        // is what makes this work.
        CellEditEventArgs<Row>? captured = null;
        var columns = new List<DataGridColumn<Row>>
        {
            new() { Field = "Custom", Title = "Custom", EditTemplate = UnwiredCustomEditTemplate() },
            new() { Field = "Name", Title = "Name" },
        };

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, columns)
            .Add(x => x.EditMode, DataGridEditMode.Cell)
            .Add(x => x.OnCellEdit, EventCallback.Factory.Create<CellEditEventArgs<Row>>(this, args => captured = args)));

        // Open row 0's Custom cell (index 0) and change its value.
        var customCell = cut.FindAll("td[data-slot='datagrid-cell']")[0];
        await cut.InvokeAsync(() => customCell.Click());
        Assert.Contains("custom-editor", cut.Markup);

        var editorInput = cut.Find("[data-testid='custom-editor']");
        await cut.InvokeAsync(() => editorInput.Input("edited-value"));

        // Open row 1's Name cell (a different, unrelated cell) — this alone must close and
        // commit row 0's still-open custom editor.
        var nameCellRow1 = cut.FindAll("td[data-slot='datagrid-cell']")[3];
        await cut.InvokeAsync(() => nameCellRow1.Click());

        Assert.DoesNotContain("custom-editor", cut.Markup);
        Assert.NotNull(captured);
        Assert.Equal("Custom", captured!.Field);
        Assert.Equal("custom-a", captured.OldValue);
        Assert.Equal("edited-value", captured.NewValue);

        // Row 1's Name cell is now the one open editor (built-in text input).
        Assert.Contains("<input", cut.Markup);
    }
}
