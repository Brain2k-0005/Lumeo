using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Segmented;

/// <summary>
/// Regression tests for the controlled-component rollback fix on Segmented.
/// When Segmented is used in controlled mode (ValueChanged bound) and the parent
/// vetoes a selection by re-rendering with the original Value, the UI must roll
/// back to the bound value rather than keeping the optimistic selection.
/// </summary>
public class SegmentedControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SegmentedControlledRollbackTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Segmented.SegmentedOption> Options() =>
    [
        new() { Label = "Day", Value = "day" },
        new() { Label = "Week", Value = "week" },
        new() { Label = "Month", Value = "month" }
    ];

    // --- Controlled: veto rolls back ---

    [Fact]
    public void Controlled_Veto_Rolls_Back_To_Bound_Value()
    {
        // Parent starts with Value="day" and vetoes every selection by keeping its
        // own state unchanged (always re-renders with Value="day").
        const string parentState = "day";
        IRenderedComponent<L.Segmented>? cut = null;

        var callback = EventCallback.Factory.Create<string>(_ctx, (string incoming) =>
        {
            // Veto: do NOT update parentState; re-render with the original value.
            cut!.Render(p =>
            {
                p.Add(s => s.Options, Options());
                p.Add(s => s.Value, parentState);   // still "day"
                p.Add(s => s.ValueChanged, EventCallback.Factory.Create<string>(_ctx, (_) => { }));
            });
        });

        cut = _ctx.Render<L.Segmented>(p => p
            .Add(s => s.Options, Options())
            .Add(s => s.Value, parentState)
            .Add(s => s.ValueChanged, callback));

        Assert.Equal("true", cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Day")).GetAttribute("aria-checked"));

        // Click "Week" — SelectOption optimistically selects "week" and fires
        // ValueChanged; the parent vetoes and re-renders with Value="day".
        cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Week")).Click();

        // After veto the UI must have rolled back to "day", not stayed at "week".
        Assert.Equal("true", cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Day")).GetAttribute("aria-checked"));
        Assert.Equal("false", cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Week")).GetAttribute("aria-checked"));
    }

    // --- Controlled: accepted selection keeps new value ---

    [Fact]
    public void Controlled_Accepted_Selection_Keeps_New_Value()
    {
        // Parent accepts every selection by updating its own state and re-rendering.
        string parentState = "day";
        IRenderedComponent<L.Segmented>? cut = null;

        EventCallback<string> callback = default;
        callback = EventCallback.Factory.Create<string>(_ctx, (string incoming) =>
        {
            parentState = incoming;
            cut!.Render(p =>
            {
                p.Add(s => s.Options, Options());
                p.Add(s => s.Value, parentState);
                p.Add(s => s.ValueChanged, callback);
            });
        });

        cut = _ctx.Render<L.Segmented>(p => p
            .Add(s => s.Options, Options())
            .Add(s => s.Value, parentState)
            .Add(s => s.ValueChanged, callback));

        cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Week")).Click();

        // Parent accepted — "week" must now show selected.
        Assert.Equal("true", cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Week")).GetAttribute("aria-checked"));
        Assert.Equal("false", cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Day")).GetAttribute("aria-checked"));
    }

    // --- Controlled: programmatic parent reset ---

    [Fact]
    public void Controlled_Programmatic_Reset_Is_Adopted()
    {
        // Start at Value="month"; parent programmatically resets to "day" WITHOUT
        // the user selecting first (simulates an external data reload or form reset).
        var cut = _ctx.Render<L.Segmented>(p => p
            .Add(s => s.Options, Options())
            .Add(s => s.Value, "month")
            .Add(s => s.ValueChanged, EventCallback.Factory.Create<string>(_ctx, (_) => { })));

        Assert.Equal("true", cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Month")).GetAttribute("aria-checked"));

        // Parent resets the bound value without a user selection first.
        cut.Render(p => p
            .Add(s => s.Options, Options())
            .Add(s => s.Value, "day")
            .Add(s => s.ValueChanged, EventCallback.Factory.Create<string>(_ctx, (_) => { })));

        Assert.Equal("true", cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Day")).GetAttribute("aria-checked"));
        Assert.Equal("false", cut.FindAll("button[role='radio']").First(b => b.TextContent.Contains("Month")).GetAttribute("aria-checked"));
    }
}
