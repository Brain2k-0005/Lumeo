using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Cascader;

/// <summary>
/// Regression tests for the controlled-component rollback fix on Cascader.
/// When Cascader is used in controlled mode (ValueChanged bound) and the
/// parent vetoes a selection/clear by re-rendering with the original Value
/// unchanged, the internal drill-down trail (_selectedPath/_activePanels)
/// must roll back to that bound value rather than keeping the optimistic
/// mutation made by SelectOption/Clear before ValueChanged was invoked.
/// </summary>
public class CascaderControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CascaderControlledRollbackTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Cascader.CascaderOption> BuildOptions() =>
    [
        new()
        {
            Label = "Fruit", Value = "fruit",
            Children =
            [
                new() { Label = "Apple", Value = "apple" },
                new() { Label = "Banana", Value = "banana" },
            ],
        },
        new()
        {
            Label = "Veg", Value = "veg",
            Children = [new() { Label = "Carrot", Value = "carrot" }],
        },
    ];

    private static AngleSharp.Dom.IElement? OptionButton(IRenderedComponent<L.Cascader> cut, string label)
        => cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == label);

    private static IReadOnlyList<string> VisibleOptionLabels(IRenderedComponent<L.Cascader> cut)
        => cut.FindAll("button")
            .Select(b => b.TextContent.Trim())
            .Where(t => t is "Fruit" or "Veg" or "Apple" or "Banana" or "Carrot")
            .ToList();

    // The selected option carries the standalone "bg-accent" token
    // (GetOptionClass(isSelected:true)); unselected rows only ever carry
    // "hover:bg-accent/50", a different token, so ClassList.Contains is exact.
    private static int SelectedOptionCount(IRenderedComponent<L.Cascader> cut)
        => cut.FindAll("button").Count(b => b.ClassList.Contains("bg-accent"));

    // --- Controlled: veto on Clear() rolls back the drill-down ---

    [Fact]
    public void Controlled_Veto_On_Clear_Rolls_Back_Drilldown_To_Bound_Value()
    {
        // Parent starts with a committed "fruit/apple" selection and vetoes the
        // Clear by keeping its own state unchanged (always re-renders with the
        // original Value).
        var options = BuildOptions();
        List<string>? parentState = ["fruit", "apple"];
        IRenderedComponent<L.Cascader>? cut = null;

        var callback = EventCallback.Factory.Create<List<string>?>(_ctx, (List<string>? incoming) =>
        {
            // Veto: do NOT adopt incoming (the clear); re-render with the
            // original, still-committed value.
            cut!.Render(p =>
            {
                p.Add(c => c.Options, options);
                p.Add(c => c.Value, parentState);
                p.Add(c => c.Clearable, true);
                p.Add(c => c.ValueChanged, EventCallback.Factory.Create<List<string>?>(_ctx, (_) => { }));
            });
        });

        cut = _ctx.Render<L.Cascader>(p => p
            .Add(c => c.Options, options)
            .Add(c => c.Clearable, true)
            .Add(c => c.Value, parentState)
            .Add(c => c.ValueChanged, callback));

        cut.Find("button").Click(); // open

        // Both levels of the committed path are highlighted, including the
        // drilled-into "Apple" child column.
        Assert.Equal(2, SelectedOptionCount(cut));
        Assert.Contains("Apple", VisibleOptionLabels(cut));

        // Click the clear (X) button — Clear() optimistically wipes Value,
        // _selectedPath and _activePanels BEFORE awaiting ValueChanged.
        var clear = cut.FindAll("button").First(b => b.GetAttribute("aria-label") is not null);
        clear.Click();

        // The parent vetoed: the highlight and drill-down column must have
        // rolled back to the bound "fruit/apple" selection, not stayed cleared.
        Assert.Equal(2, SelectedOptionCount(cut));
        Assert.Contains("Apple", VisibleOptionLabels(cut));
    }

    // --- Controlled: accepted Clear() keeps the empty selection ---

    [Fact]
    public void Controlled_Accepted_Clear_Keeps_Empty_Selection()
    {
        var options = BuildOptions();
        List<string>? parentState = ["fruit", "apple"];
        IRenderedComponent<L.Cascader>? cut = null;

        EventCallback<List<string>?> callback = default;
        callback = EventCallback.Factory.Create<List<string>?>(_ctx, (List<string>? incoming) =>
        {
            parentState = incoming;
            cut!.Render(p =>
            {
                p.Add(c => c.Options, options);
                p.Add(c => c.Value, parentState);
                p.Add(c => c.Clearable, true);
                p.Add(c => c.ValueChanged, callback);
            });
        });

        cut = _ctx.Render<L.Cascader>(p => p
            .Add(c => c.Options, options)
            .Add(c => c.Clearable, true)
            .Add(c => c.Value, parentState)
            .Add(c => c.ValueChanged, callback));

        cut.Find("button").Click(); // open

        var clear = cut.FindAll("button").First(b => b.GetAttribute("aria-label") is not null);
        clear.Click();

        // Parent accepted the clear — selection stays empty.
        Assert.Equal(0, SelectedOptionCount(cut));
        Assert.DoesNotContain("Apple", VisibleOptionLabels(cut));
    }

    // --- Controlled: veto on a leaf commit rolls back the drill-down ---

    [Fact]
    public void Controlled_Veto_On_Leaf_Selection_Rolls_Back_Drilldown_On_Reopen()
    {
        // Parent starts with no selection and vetoes every commit by keeping
        // its own state unchanged (always re-renders with Value=null).
        var options = BuildOptions();
        List<string>? parentState = null;
        IRenderedComponent<L.Cascader>? cut = null;

        var callback = EventCallback.Factory.Create<List<string>?>(_ctx, (List<string>? incoming) =>
        {
            cut!.Render(p =>
            {
                p.Add(c => c.Options, options);
                p.Add(c => c.Value, parentState);
                p.Add(c => c.ValueChanged, EventCallback.Factory.Create<List<string>?>(_ctx, (_) => { }));
            });
        });

        cut = _ctx.Render<L.Cascader>(p => p
            .Add(c => c.Options, options)
            .Add(c => c.Value, parentState)
            .Add(c => c.ValueChanged, callback));

        cut.Find("button").Click();          // open
        OptionButton(cut, "Fruit")!.Click(); // drill into "Fruit" (no commit)
        OptionButton(cut, "Apple")!.Click(); // commit the leaf -> parent vetoes -> picker closes

        // Reopen: a correctly rolled-back trail shows only the root column
        // again; a stale optimistic trail would still show "Fruit"'s children.
        cut.Find("button").Click();
        var labels = VisibleOptionLabels(cut);
        Assert.DoesNotContain("Apple", labels);
        Assert.DoesNotContain("Banana", labels);
        Assert.Contains("Fruit", labels);

        // The trigger must still show the placeholder, not the rejected path.
        Assert.DoesNotContain("Fruit / Apple", cut.Markup);
    }
}
