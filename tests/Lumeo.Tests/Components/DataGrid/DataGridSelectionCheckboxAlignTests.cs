using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// The select-all checkbox and the row checkboxes sit in one column only if they are
/// placed the same way. They used to rely on each cell's padding: Compact rows use
/// px-2 while the header cell keeps px-3 (4 px apart), and an app rule that pads th
/// differently from td moved the select-all box out of line. Both are now centred in
/// their cell, so any symmetric padding difference leaves them aligned.
/// </summary>
public class DataGridSelectionCheckboxAlignTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridSelectionCheckboxAlignTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static readonly Row[] Data = { new(1, "Alice"), new(2, "Bob") };

    private static List<DataGridColumn<Row>> Cols() => new()
    {
        new() { Field = "Id", Title = "ID" },
        new() { Field = "Name", Title = "Name" },
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Header_And_Row_Checkboxes_Are_Centred_In_Their_Cells(bool compact)
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.Compact, compact)
            .Add(g => g.SelectionMode, DataGridSelectionMode.Multiple)
            .Add(g => g.Columns, Cols()));

        var headerBox = cut.Find("thead [role='checkbox']");
        var rowBoxes = cut.FindAll("tbody [role='checkbox']");
        Assert.Equal(Data.Length, rowBoxes.Count);

        foreach (var box in rowBoxes.Prepend(headerBox))
        {
            var cell = box.Closest("th, td");
            Assert.NotNull(cell);
            var wrapper = cell!.Children.Single();
            var cls = wrapper.GetAttribute("class") ?? "";
            Assert.Contains("justify-center", cls);
            Assert.Contains("flex", cls);
        }
    }

    [Fact]
    public void Single_Selection_Marker_Is_Centred()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.SelectionMode, DataGridSelectionMode.Single)
            .Add(g => g.Columns, Cols()));

        var marker = cut.Find("tbody td button[aria-pressed]");
        var cls = marker.GetAttribute("class") ?? "";
        Assert.Contains("mx-auto", cls);
        Assert.Contains("block", cls);
    }
}
