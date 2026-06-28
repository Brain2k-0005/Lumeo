using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Descriptions;

public class DescriptionsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DescriptionsTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Title_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .Add(d => d.Title, "User Details")
            .AddChildContent("Items here"));

        Assert.Contains("User Details", cut.Markup);
        var h3 = cut.Find("h3");
        Assert.Equal("User Details", h3.TextContent);
    }

    [Fact]
    public void Default_Column_Count_Is_Three()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .AddChildContent("Items"));

        Assert.Contains("grid-cols-3", cut.Markup);
    }

    [Fact]
    public void Column_Parameter_Changes_GridCols()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .Add(d => d.Column, 2)
            .AddChildContent("Items"));

        Assert.Contains("grid-cols-2", cut.Markup);
    }

    [Fact]
    public void Bordered_True_Renders_Border_Container()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .Add(d => d.Bordered, true)
            .AddChildContent("Items"));

        Assert.Contains("rounded-lg", cut.Markup);
        Assert.Contains("border", cut.Markup);
    }

    [Fact]
    public void Non_Bordered_Uses_Gap_Classes()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .Add(d => d.Bordered, false)
            .AddChildContent("Items"));

        Assert.Contains("gap-4", cut.Markup);
    }

    // Regression (battle-wave3 #39): a value-only DescriptionsItem (empty/null
    // Label) must still emit a <dt> so each div group is a valid dt/dd pair and
    // the <dd> is not orphaned inside the <dl> for screen readers. Without the
    // fix no <dt> is rendered at all when Label is empty.
    [Fact]
    public void DescriptionsItem_Without_Label_Still_Emits_Dt()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .AddChildContent<Lumeo.DescriptionsItem>(item => item
                .AddChildContent("value only")));

        var dts = cut.FindAll("dt");
        Assert.Single(dts);
        // Hidden visually but kept in the a11y tree to preserve the dt/dd pair.
        Assert.Contains("sr-only", dts[0].GetAttribute("class"));
        Assert.Single(cut.FindAll("dd"));
    }

    // Regression (battle-wave3 #39): a whitespace-only Label is treated as empty
    // (IsNullOrWhiteSpace), so the <dt> is emitted but visually hidden. Without
    // the fix the old IsNullOrEmpty guard would render a visible, blank <dt>.
    [Fact]
    public void DescriptionsItem_Whitespace_Label_Is_Hidden()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .AddChildContent<Lumeo.DescriptionsItem>(item => item
                .Add(i => i.Label, "   ")
                .AddChildContent("value")));

        var dt = cut.Find("dt");
        Assert.Contains("sr-only", dt.GetAttribute("class"));
    }

    // Positive control: a real Label still renders a visible <dt> (not sr-only).
    [Fact]
    public void DescriptionsItem_With_Label_Emits_Visible_Dt()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .AddChildContent<Lumeo.DescriptionsItem>(item => item
                .Add(i => i.Label, "Name")
                .AddChildContent("Ada")));

        var dt = cut.Find("dt");
        Assert.Equal("Name", dt.TextContent);
        Assert.DoesNotContain("sr-only", dt.GetAttribute("class"));
    }
}
