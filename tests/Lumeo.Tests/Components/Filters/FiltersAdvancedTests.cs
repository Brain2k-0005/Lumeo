using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Filters;

/// <summary>The advanced builder: rows with a combinator, groups, the add-row flow through a
/// pending row, inline value editing, the group menu, reordering and keyboard travel.</summary>
public class FiltersAdvancedTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FiltersAdvancedTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IReadOnlyList<FilterField> Fields() => new[]
    {
        new FilterField { Id = "title", Label = "Title" },
        new FilterField { Id = "status", Label = "Status", Kind = FilterValueKind.Select, Options = new[] { new FilterChoice("draft", "Draft"), new FilterChoice("done", "Done") } },
        new FilterField { Id = "amount", Label = "Amount", Kind = FilterValueKind.Number, Validate = c => c.Value is double d && d < 0 ? "No negatives" : null },
    };

    private static FilterQuery Preset() => FilterQuery.Of(
        new FilterRule("r1", new[] { "title" }, "contains", "a"),
        new FilterGroup("g1", FilterCombinator.Or, new FilterNode[]
        {
            new FilterRule("r2", new[] { "status" }, "is", "draft"),
            new FilterRule("r3", new[] { "amount" }, "gt", 5.0),
        }));

    private IRenderedComponent<Lumeo.Filters> Render(Action<ComponentParameterCollectionBuilder<Lumeo.Filters>>? configure = null)
        => _ctx.Render<Lumeo.Filters>(p =>
        {
            p.Add(f => f.Fields, Fields()).Add(f => f.Variant, FiltersVariant.Advanced).Add(f => f.AdvancedMode, FiltersAdvancedMode.Inline);
            configure?.Invoke(p);
        });

    private static IReadOnlyList<AngleSharp.Dom.IElement> Rows(IRenderedComponent<Lumeo.Filters> cut) => cut.FindAll("[data-slot='filter-row']");

    [Fact]
    public void Renders_Rows_Groups_And_Combinators()
    {
        var cut = Render(p => p.Add(f => f.DefaultQuery, Preset()));

        var rows = Rows(cut);
        Assert.Equal(4, rows.Count); // r1, g1, r2, r3
        Assert.Equal("Where", rows[0].QuerySelector("[data-slot='filter-row'] span, span")!.TextContent.Trim());
        Assert.Equal("And", rows[1].QuerySelector("[data-filter-cell='combinator']")!.TextContent.Trim());
        var group = cut.Find("[data-slot='filter-group']");
        Assert.NotNull(group.QuerySelector("[data-slot='filter-group-footer'] [data-filter-cell='add']"));
        // the group's second row toggles Or
        Assert.Equal("Or", rows[3].QuerySelector("[data-filter-cell='combinator']")!.TextContent.Trim());
        Assert.Equal("a", rows[0].QuerySelector("[data-filter-cell='value']")!.GetAttribute("value"));
    }

    [Fact]
    public void Empty_Panel_Shows_The_Empty_State_And_Footer()
    {
        var cut = Render();
        Assert.NotNull(cut.Find("[data-slot='filters-advanced-empty']"));
        Assert.NotNull(cut.Find("[data-slot='filters-advanced-add']"));
        Assert.Empty(cut.FindAll("[data-slot='filters-advanced-clear']"));
    }

    [Fact]
    public void Add_Row_Is_Pending_Until_Its_Field_Is_Picked_Then_Opens_The_Condition()
    {
        var cut = Render();
        cut.Find("[data-slot='filters-advanced-add']").Click();

        var row = Assert.Single(Rows(cut));
        Assert.Equal("true", row.GetAttribute("data-pending"));
        Assert.Null(row.QuerySelector("[data-filter-cell='operator']"));
        // the field picker opened by itself
        cut.FindAll("[data-slot='filter-field-picker'] [data-slot='command-item']").First(e => e.TextContent.Contains("Status")).Click();

        row = Assert.Single(Rows(cut));
        Assert.Null(row.GetAttribute("data-pending"));
        Assert.Equal("Status", row.QuerySelector("[data-filter-cell='field']")!.GetAttribute("aria-label"));
        Assert.NotNull(row.QuerySelector("[data-filter-cell='operator']"));
        // and the condition menu opened next
        cut.FindAll("[data-slot='filter-menu'] [data-slot='command-item']").First(e => e.TextContent.Contains("is not")).Click();
        Assert.Equal("is not", Rows(cut)[0].QuerySelector("[data-filter-cell='operator']")!.TextContent.Trim());
    }

    [Fact]
    public void Inline_Value_Commits_On_Enter_And_Shows_A_Custom_Issue()
    {
        FilterQuery? last = null;
        var cut = Render(p => p.Add(f => f.DefaultQuery, FilterQuery.Of(new FilterRule("r", new[] { "amount" }, "gt", 5.0)))
            .Add(f => f.QueryChanged, EventCallback.Factory.Create<FilterQuery>(this, q => last = q)));

        var input = cut.Find("[data-filter-cell='value']");
        input.Input("-3");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(-3.0, last!.Flatten()[0].Values[0]);
        var value = cut.Find("[data-filter-cell='value']");
        Assert.Equal("true", value.GetAttribute("aria-invalid"));
        Assert.Equal("No negatives", value.GetAttribute("aria-description"));
    }

    [Fact]
    public void Group_Menu_Unwraps_And_Combinator_Toggles()
    {
        FilterQuery? last = null;
        var cut = Render(p => p.Add(f => f.DefaultQuery, Preset()).Add(f => f.QueryChanged, EventCallback.Factory.Create<FilterQuery>(this, q => last = q)));

        Rows(cut)[3].QuerySelector("[data-filter-cell='combinator']")!.Click();
        Assert.Equal(FilterCombinator.And, ((FilterGroup)last!.Rules[1]).Combinator);

        cut.Find("[data-slot='filter-group-footer'] [data-filter-cell='menu']").Click();
        cut.FindAll("[role='menuitem']").First(e => e.TextContent.Contains("Ungroup")).Click();
        Assert.Equal(3, last!.Rules.Count);
        Assert.Equal(3, Rows(cut).Count);
    }

    [Fact]
    public void Add_Group_And_Add_Into_It()
    {
        FilterQuery? last = null;
        var cut = Render(p => p.Add(f => f.QueryChanged, EventCallback.Factory.Create<FilterQuery>(this, q => last = q)));
        cut.Find("[data-slot='filters-advanced-add-group']").Click();
        Assert.NotNull(cut.Find("[data-slot='filter-group-empty']"));
        cut.Find("[data-slot='filter-group-footer'] [data-filter-cell='add']").Click();

        var group = Assert.IsType<FilterGroup>(Assert.Single(last!.Rules));
        Assert.Single(group.Rules);
        Assert.Equal("true", Rows(cut)[1].GetAttribute("data-pending"));
    }

    [Fact]
    public void Reorderable_Shows_Handles_And_Alt_Arrow_Moves()
    {
        FilterQuery? last = null;
        var cut = Render(p => p.Add(f => f.DefaultQuery, Preset()).Add(f => f.Reorderable, true).Add(f => f.QueryChanged, EventCallback.Factory.Create<FilterQuery>(this, q => last = q)));
        Assert.NotEmpty(cut.FindAll("[data-filter-drag]"));

        Rows(cut)[0].QuerySelector("[data-filter-cell='field']")!.Focus();
        // focusing re-renders (the roving tab stop moved), so the cell is looked up again
        Rows(cut)[0].QuerySelector("[data-filter-cell='field']")!.KeyDown(new KeyboardEventArgs { Key = "ArrowDown", AltKey = true });
        Assert.Equal("g1", last!.Rules[0].Id);
        Assert.Equal("r1", last.Rules[1].Id);
    }

    [Fact]
    public void Delete_On_A_Cell_Removes_The_Row()
    {
        var cut = Render(p => p.Add(f => f.DefaultQuery, Preset()));
        Rows(cut)[0].QuerySelector("[data-filter-cell='field']")!.Focus();
        Rows(cut)[0].QuerySelector("[data-filter-cell='field']")!.KeyDown(new KeyboardEventArgs { Key = "Delete" });
        Assert.Equal(3, Rows(cut).Count);
    }

    [Fact]
    public void Popover_Mode_Shows_The_Trigger_With_A_Count()
    {
        var cut = _ctx.Render<Lumeo.Filters>(p => p.Add(f => f.Fields, Fields()).Add(f => f.Variant, FiltersVariant.Advanced).Add(f => f.DefaultQuery, Preset()));
        var trigger = cut.Find("[data-slot='filters-advanced-trigger']");
        Assert.Contains("3", trigger.TextContent);
        Assert.Contains("3 filters applied", trigger.GetAttribute("aria-label"));
        trigger.Click();
        Assert.NotNull(cut.Find("[data-slot='filters-advanced']"));
    }

    [Fact]
    public void Clear_All_Empties_The_Builder()
    {
        var cut = Render(p => p.Add(f => f.DefaultQuery, Preset()));
        cut.Find("[data-slot='filters-advanced-clear']").Click();
        Assert.Empty(Rows(cut));
        Assert.NotNull(cut.Find("[data-slot='filters-advanced-empty']"));
    }


    [Fact]
    public void A_Vetoed_Field_Pick_Keeps_The_Row_Pending()
    {
        var cut = Render(p => p.Add(f => f.OnBeforeQueryChange, (FilterQueryChange c) => c.Details.Reason != FilterChangeReason.Update));
        cut.Find("[data-slot='filters-advanced-add']").Click();
        Assert.Equal("true", Assert.Single(Rows(cut)).GetAttribute("data-pending"));

        cut.FindAll("[data-slot='filter-field-picker'] [data-slot='command-item']").First(e => e.TextContent.Contains("Status")).Click();

        var row = Assert.Single(Rows(cut));
        Assert.Equal("true", row.GetAttribute("data-pending"));
        Assert.Null(row.QuerySelector("[data-filter-cell='operator']"));
    }

    [Fact]
    public void Changing_A_Settled_Rows_Field_Starts_It_Over_At_The_Condition()
    {
        FilterQuery? last = null;
        var cut = Render(p => p.Add(f => f.DefaultQuery, Preset()).Add(f => f.QueryChanged, EventCallback.Factory.Create<FilterQuery>(this, q => last = q)));
        Rows(cut)[0].QuerySelector("[data-filter-cell='field']")!.Click();
        cut.FindAll("[data-slot='filter-field-picker'] [data-slot='command-item']").First(e => e.TextContent.Contains("Amount")).Click();

        var rule = (FilterRule)last!.Rules[0];
        Assert.Equal(("amount", ""), (rule.Field, rule.Operator));
        Assert.NotEmpty(cut.FindAll("[data-slot='filter-menu'] [data-slot='command-item']"));
    }

    [Fact]
    public void Rows_Announce_Their_Depth_Through_The_Labels()
    {
        var cut = Render(p => p.Add(f => f.DefaultQuery, Preset()).Add(f => f.Labels, new FilterLabels { RowLevel = "{0} (Ebene {1})" }));
        Assert.EndsWith("(Ebene 2)", Rows(cut)[2].GetAttribute("aria-label"));
        Assert.EndsWith("(Ebene 1)", cut.Find("[data-slot='filter-row'][data-node-id='g1']").GetAttribute("aria-label"));
    }

    [Fact]
    public void Discard_Closes_A_Rows_Value_Popover()
    {
        var cut = Render(p => p.Add(f => f.DefaultQuery, FilterQuery.Of(new FilterRule("r", new[] { "amount" }, "between", new FilterRange(1.0, 5.0)))));
        Rows(cut)[0].QuerySelector("[data-filter-cell='value']")!.Click();
        Assert.NotEmpty(cut.FindAll("[data-slot='filter-editor']"));

        cut.FindAll("[data-slot='filter-editor'] button").First(b => b.TextContent.Trim() == "Discard changes").Click();
        Assert.Empty(cut.FindAll("[data-slot='filter-editor']"));
    }
}
