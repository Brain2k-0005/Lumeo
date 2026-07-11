using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.PivotGrid;

/// <summary>
/// PR #356 round-2 (Codex/CodeRabbit) — two bugs in the drill-down cell
/// Enter/Space-scroll-suppression registration (see PivotGridKeyboardTests for
/// the feature this registration protects):
///
/// 1. A consumer-splatted <c>id</c> in AdditionalAttributes renders AFTER the
///    wrapper's explicit <c>id="@_wrapperId"</c> and wins in the DOM, but the
///    registration used to always target the raw internal id — leaving it
///    pointed at an id no element in the document carries, so Space silently
///    stopped being suppressed. Fixed via EffectiveWrapperId (mirrors
///    PopoverTrigger/DialogTrigger/SheetTrigger's own EffectiveId pattern).
///
/// 2. The registration only ran on firstRender, so a consumer that binds
///    OnCellClick AFTER the initial render (e.g. once some other state
///    resolves) never got the suppression registered at all. Fixed by
///    checking the registered-flag instead of firstRender.
/// </summary>
public class PivotGridPreventDefaultRegistrationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PivotGridPreventDefaultRegistrationTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Sale(string Region, int Year, decimal Amount);

    private static List<Sale> Data() => new()
    {
        new("North", 2023, 100m),
        new("South", 2023, 30m),
    };

    private static IReadOnlyList<L.PivotField<Sale>> RowFields() => new List<L.PivotField<Sale>>
    {
        new("Region", s => s.Region),
    };

    private static IReadOnlyList<L.PivotMeasure<Sale>> SumMeasure() => new List<L.PivotMeasure<Sale>>
    {
        new("Amount", s => s.Amount, L.PivotAggregate.Sum),
    };

    private IReadOnlyList<Lumeo.Services.PreventDefaultKeyRule>? RuleSetFor(string elementId) =>
        _ctx.JSInterop.Invocations
            .Where(i => i.Identifier == "registerPreventDefaultKeys"
                        && i.Arguments[0] is string id && id == elementId)
            .Select(i => i.Arguments[1] as IReadOnlyList<Lumeo.Services.PreventDefaultKeyRule>)
            .LastOrDefault();

    [Fact]
    public void Consumer_Splatted_Id_Wins_In_The_Dom()
    {
        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.RowFields, RowFields())
            .Add(g => g.Measures, SumMeasure())
            .Add(g => g.OnCellClick, _ => { })
            .Add(g => g.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "my-pivot" }));

        Assert.Equal("my-pivot", cut.Find("div[id]").GetAttribute("id"));
    }

    [Fact]
    public void Registration_Targets_The_Consumer_Splatted_Id_Not_The_Internal_Fallback()
    {
        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.RowFields, RowFields())
            .Add(g => g.Measures, SumMeasure())
            .Add(g => g.OnCellClick, _ => { })
            .Add(g => g.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "my-pivot" }));

        cut.WaitForAssertion(() => Assert.NotNull(RuleSetFor("my-pivot")));
        Assert.Contains(RuleSetFor("my-pivot")!, r => r.Key == "Enter");
    }

    [Fact]
    public void Without_A_Consumer_Id_Registration_Targets_The_Rendered_Fallback_Id()
    {
        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.RowFields, RowFields())
            .Add(g => g.Measures, SumMeasure())
            .Add(g => g.OnCellClick, _ => { }));

        var renderedId = cut.Find("div[id]").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(renderedId));
        cut.WaitForAssertion(() => Assert.NotNull(RuleSetFor(renderedId!)));
    }

    [Fact]
    public void Late_Bound_OnCellClick_Still_Registers_The_Suppression()
    {
        // OnCellClick starts unbound (nothing to protect yet) — the FIRST render's
        // OnAfterRenderAsync must not be the only chance to register.
        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.RowFields, RowFields())
            .Add(g => g.Measures, SumMeasure()));

        var renderedId = cut.Find("div[id]").GetAttribute("id")!;
        Assert.Null(RuleSetFor(renderedId));

        // Bind OnCellClick on a LATER render.
        cut.Render(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.RowFields, RowFields())
            .Add(g => g.Measures, SumMeasure())
            .Add(g => g.OnCellClick, _ => { }));

        cut.WaitForAssertion(() => Assert.NotNull(RuleSetFor(renderedId)));
    }
}
