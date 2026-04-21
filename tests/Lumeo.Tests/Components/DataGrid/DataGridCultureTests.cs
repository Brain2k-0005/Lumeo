using System.Globalization;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Verifies that <see cref="Lumeo.DataGrid{TItem}"/> cascades its <c>Culture</c>
/// parameter through to cells so numeric / date formatting follows the grid's
/// requested locale rather than the runtime default.
/// </summary>
public class DataGridCultureTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridCultureTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(string Name, decimal Amount, DateTime When);

    private static List<Row> Data() => new()
    {
        new("Alice", 1234.56m, new DateTime(2026, 3, 15)),
    };

    private static List<DataGridColumn<Row>> Columns() => new()
    {
        new() { Field = "Name", Title = "Name" },
        new() { Field = "Amount", Title = "Amount", Format = "N2" },
        new() { Field = "When", Title = "When", Format = "d" },
    };

    [Fact]
    public void DataGrid_With_German_Culture_Formats_Decimal_With_Comma()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.Columns, Columns())
            .Add(x => x.Culture, de));

        Assert.Contains("1.234,56", cut.Markup);
    }

    [Fact]
    public void DataGrid_With_English_Culture_Formats_Decimal_With_Period()
    {
        var us = CultureInfo.GetCultureInfo("en-US");
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.Columns, Columns())
            .Add(x => x.Culture, us));

        Assert.Contains("1,234.56", cut.Markup);
    }

    [Fact]
    public void DataGrid_EffectiveCulture_Falls_Back_To_CurrentCulture_When_Not_Set()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.Columns, Columns()));

        var grid = cut.Instance;
        Assert.NotNull(grid);
        // The internal property just returns CurrentCulture when Culture is null.
        Assert.Equal(CultureInfo.CurrentCulture, grid.EffectiveCulture);
    }
}
