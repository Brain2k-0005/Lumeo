using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// #317 — entering fullscreen adds the `lumeo-fullscreen-active` class on &lt;html&gt;.
/// It must be removed both on the normal collapse path AND when the grid is
/// disposed while still expanded (route change / parent re-render), otherwise the
/// class leaks onto the host page. SetHtmlClass routes through the components.js
/// `setHtmlClass` interop call, which we assert against the recorded invocations.
/// </summary>
public class DataGridFullscreenTeardownTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridFullscreenTeardownTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<Row> Data() => new() { new(1, "Alice"), new(2, "Bob") };
    private static List<DataGridColumn<Row>> Columns() => new()
    {
        new() { Field = "Id", Title = "ID" },
        new() { Field = "Name", Title = "Name" },
    };

    private static int SetHtmlClassCount(BunitContext ctx, string className, bool active) =>
        ctx.JSInterop.Invocations.Count(i =>
            i.Identifier == "setHtmlClass"
            && i.Arguments.Count == 2
            && Equals(i.Arguments[0], className)
            && Equals(i.Arguments[1], active));

    [Fact]
    public async Task Entering_Fullscreen_Adds_Html_Class()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.Columns, Columns())
            .Add(x => x.ShowToolbar, true)
            .Add(x => x.Expandable, true));

        await cut.InvokeAsync(() => cut.Instance.ToggleExpanded());

        Assert.True(SetHtmlClassCount(_ctx, "lumeo-fullscreen-active", true) >= 1);
    }

    [Fact]
    public async Task Collapsing_Removes_Html_Class()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.Columns, Columns())
            .Add(x => x.ShowToolbar, true)
            .Add(x => x.Expandable, true));

        await cut.InvokeAsync(() => cut.Instance.ToggleExpanded()); // expand
        await cut.InvokeAsync(() => cut.Instance.ToggleExpanded()); // collapse

        Assert.True(SetHtmlClassCount(_ctx, "lumeo-fullscreen-active", false) >= 1);
    }

    [Fact]
    public async Task Disposing_While_Expanded_Removes_Html_Class()
    {
        // Use a dedicated context so we can dispose it mid-fullscreen and inspect
        // the invocations recorded up to (and including) disposal.
        using var ctx = new BunitContext();
        ctx.AddLumeoServices();

        var cut = ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.Columns, Columns())
            .Add(x => x.ShowToolbar, true)
            .Add(x => x.Expandable, true));

        await cut.InvokeAsync(() => cut.Instance.ToggleExpanded()); // now expanded

        var removalsBefore = SetHtmlClassCount(ctx, "lumeo-fullscreen-active", false);

        // Dispose while still expanded — the leak path. DisposeAsync must clear the class.
        await cut.Instance.DisposeAsync();

        var removalsAfter = SetHtmlClassCount(ctx, "lumeo-fullscreen-active", false);
        Assert.True(removalsAfter > removalsBefore,
            "Disposing while expanded must remove the lumeo-fullscreen-active html class.");
    }
}
