using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Filters;

/// <summary>The chip row end to end: add a filter through the field picker, pick the condition,
/// enter the value, and the menu actions, keyboard and locked states around it.</summary>
public class FiltersTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FiltersTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IReadOnlyList<FilterField> Fields() => new[]
    {
        new FilterField { Id = "title", Label = "Title" },
        new FilterField { Id = "status", Label = "Status", Kind = FilterValueKind.Select, Options = new[] { new FilterChoice("draft", "Draft"), new FilterChoice("done", "Done") } },
        new FilterField { Id = "amount", Label = "Amount", Kind = FilterValueKind.Number },
        new FilterField { Id = "archived", Label = "Archived", Kind = FilterValueKind.Boolean },
    };

    private static FilterQuery Preset() => FilterQuery.Of(
        new FilterRule("r1", new[] { "status" }, "is", "draft"),
        new FilterRule("r2", new[] { "amount" }, "gt", 10.0));

    private IRenderedComponent<Lumeo.Filters> Render(Action<ComponentParameterCollectionBuilder<Lumeo.Filters>>? configure = null)
        => _ctx.Render<Lumeo.Filters>(p =>
        {
            p.Add(f => f.Fields, Fields());
            configure?.Invoke(p);
        });

    private static IReadOnlyList<AngleSharp.Dom.IElement> Chips(IRenderedComponent<Lumeo.Filters> cut) => cut.FindAll("[data-slot='filter-chip']");

    [Fact]
    public void Renders_The_Preset_As_Chips_With_Field_Operator_And_Value()
    {
        var cut = Render(p => p.Add(f => f.DefaultQuery, Preset()));

        var chips = Chips(cut);
        Assert.Equal(2, chips.Count);
        Assert.Equal("Status", chips[0].QuerySelector("[data-segment='field']")!.TextContent.Trim());
        Assert.Equal("is", chips[0].QuerySelector("[data-segment='operator']")!.TextContent.Trim());
        Assert.Equal("Draft", chips[0].QuerySelector("[data-segment='value']")!.TextContent.Trim());
        Assert.Equal("is greater than", chips[1].QuerySelector("[data-segment='operator']")!.TextContent.Trim());
        Assert.Equal("10", chips[1].QuerySelector("[data-segment='value']")!.TextContent.Trim());
        // the add button collapses to its icon once a chip exists
        Assert.Equal("Add filter", cut.Find("[data-slot='filter-add']").GetAttribute("aria-label"));
    }

    [Fact]
    public void Adding_A_Filter_Walks_Field_Operator_And_Value()
    {
        FilterQuery? last = null;
        var cut = Render(p => p.Add(f => f.QueryChanged, EventCallback.Factory.Create<FilterQuery>(this, q => last = q)));

        Assert.Empty(Chips(cut));
        cut.Find("[data-slot='filter-add']").Click();
        var titleRow = cut.FindAll("[data-slot='filter-field-picker'] [data-slot='command-item']").First(e => e.TextContent.Contains("Title"));
        titleRow.Click();

        // the chip exists with no operator and its operator popover opened by itself
        var chip = Assert.Single(Chips(cut));
        Assert.Equal("true", chip.GetAttribute("data-incomplete"));
        Assert.Equal("Select condition", chip.QuerySelector("[data-segment='operator']")!.TextContent.Trim());
        var contains = cut.FindAll("[data-slot='filter-menu'] [data-slot='command-item']").First(e => e.TextContent.Contains("contains"));
        contains.Click();

        // now the value editor is open; typing and Enter commits
        chip = Assert.Single(Chips(cut));
        Assert.Null(chip.GetAttribute("data-incomplete"));
        var input = cut.Find("[data-slot='filter-editor'] input");
        input.Input("report");
        cut.Find("[data-slot='filter-editor']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("report", Chips(cut)[0].QuerySelector("[data-segment='value']")!.TextContent.Trim());
        Assert.NotNull(last);
        var term = Assert.Single(last!.Flatten());
        Assert.Equal(("title", "contains", "report"), (term.Field, term.Operator, (string)term.Values[0]!));
    }

    [Fact]
    public void Select_Field_Commits_The_Option_And_Shows_Its_Label()
    {
        var cut = Render(p => p.Add(f => f.DefaultQuery, FilterQuery.Of(new FilterRule("r1", new[] { "status" }, "is"))));
        Chips(cut)[0].QuerySelector("[data-segment='value']")!.Click();
        cut.FindAll("[data-slot='filter-menu'] [data-slot='command-item']").First(e => e.TextContent.Contains("Done")).Click();

        Assert.Equal("Done", Chips(cut)[0].QuerySelector("[data-segment='value']")!.TextContent.Trim());
    }

    [Fact]
    public void Menu_Negates_Duplicates_And_Removes()
    {
        var cut = Render(p => p.Add(f => f.DefaultQuery, Preset()));
        Chips(cut)[0].QuerySelector("[data-segment='menu']")!.Click();
        cut.FindAll("[role='menuitem']").First(e => e.TextContent.Contains("Negate")).Click();
        Assert.Equal("is not", Chips(cut)[0].QuerySelector("[data-segment='operator']")!.TextContent.Trim());

        Chips(cut)[0].QuerySelector("[data-segment='menu']")!.Click();
        cut.FindAll("[role='menuitem']").First(e => e.TextContent.Contains("Duplicate")).Click();
        Assert.Equal(3, Chips(cut).Count);
        Assert.Equal("Status", Chips(cut)[1].QuerySelector("[data-segment='field']")!.TextContent.Trim());

        Chips(cut)[1].QuerySelector("[data-segment='menu']")!.Click();
        cut.FindAll("[role='menuitem']").First(e => e.TextContent.Contains("Remove")).Click();
        Assert.Equal(2, Chips(cut).Count);
    }

    [Fact]
    public void Clear_Empties_The_Query_And_The_Live_Region_Announces_It()
    {
        FilterQueryChange? change = null;
        var cut = Render(p => p.Add(f => f.DefaultQuery, Preset()).Add(f => f.ShowClear, true)
            .Add(f => f.OnQueryChanged, EventCallback.Factory.Create<FilterQueryChange>(this, c => change = c)));

        cut.Find("[data-slot='filter-clear']").Click();

        Assert.Empty(Chips(cut));
        Assert.Equal(FilterChangeReason.Clear, change!.Details.Reason);
        Assert.Equal("0 filters applied", cut.Find("[role='status']").TextContent.Trim());
        Assert.Empty(cut.FindAll("[data-slot='filter-clear']"));
    }

    [Fact]
    public void A_Veto_Refuses_The_Change()
    {
        var cut = Render(p => p.Add(f => f.DefaultQuery, Preset()).Add(f => f.OnBeforeQueryChange, (FilterQueryChange c) => false));
        Chips(cut)[0].QuerySelector("[data-segment='menu']")!.Click();
        cut.FindAll("[role='menuitem']").First(e => e.TextContent.Contains("Remove")).Click();
        Assert.Equal(2, Chips(cut).Count);
    }

    [Fact]
    public void ReadOnly_Keeps_Tab_Stops_But_Refuses_Edits_Disabled_Drops_Them()
    {
        var ro = Render(p => p.Add(f => f.DefaultQuery, Preset()).Add(f => f.ReadOnly, true));
        Assert.Equal("true", ro.Find("[data-slot='filters']").GetAttribute("data-readonly"));
        Assert.Equal("true", ro.Find("[data-segment='operator']").GetAttribute("aria-disabled"));
        Assert.Null(ro.Find("[data-segment='operator']").GetAttribute("disabled"));

        var dis = Render(p => p.Add(f => f.DefaultQuery, Preset()).Add(f => f.Disabled, true));
        Assert.NotNull(dis.Find("[data-segment='operator']").GetAttribute("disabled"));
        Assert.NotNull(dis.Find("[data-slot='filter-add']").GetAttribute("disabled"));
    }

    [Fact]
    public void Delete_On_A_Focused_Chip_Removes_It()
    {
        var cut = Render(p => p.Add(f => f.DefaultQuery, Preset()));
        var chip = Chips(cut)[0];
        chip.Focus();
        chip.KeyDown(new KeyboardEventArgs { Key = "Delete" });
        Assert.Single(Chips(cut));
        Assert.Equal("Amount", Chips(cut)[0].QuerySelector("[data-segment='field']")!.TextContent.Trim());
    }

    [Fact]
    public void A_Controlled_Query_Renders_What_The_Host_Says()
    {
        var cut = Render(p => p.Add(f => f.Query, Preset()));
        Assert.Equal(2, Chips(cut).Count);
        cut.Render(p => p.Add(f => f.Fields, Fields()).Add(f => f.Query, FilterQuery.Of(new FilterRule("x", new[] { "title" }, "is", "a"))));
        Assert.Single(Chips(cut));
        Assert.Equal("Title", Chips(cut)[0].QuerySelector("[data-segment='field']")!.TextContent.Trim());
    }

    [Fact]
    public void An_Unknown_Field_Renders_A_Removable_Chip()
    {
        var cut = Render(p => p.Add(f => f.DefaultQuery, FilterQuery.Of(new FilterRule("u", new[] { "gone" }, "is", "x"))));
        var chip = Assert.Single(Chips(cut));
        Assert.Equal("true", chip.GetAttribute("data-unknown"));
        chip.QuerySelector("[data-segment='remove']")!.Click();
        Assert.Empty(Chips(cut));
    }

    [Fact]
    public void Boolean_And_Empty_Operators_Take_No_Value_Segment()
    {
        var cut = Render(p => p.Add(f => f.DefaultQuery, FilterQuery.Of(new FilterRule("e", new[] { "title" }, "empty"))));
        var chip = Assert.Single(Chips(cut));
        Assert.Null(chip.QuerySelector("[data-segment='value']"));
        Assert.Equal("is empty", chip.QuerySelector("[data-segment='operator']")!.TextContent.Trim());
    }
}
