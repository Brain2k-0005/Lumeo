using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Switch;

/// <summary>
/// Regression tests for the controlled-component rollback fix on Switch.
/// When the switch is used in controlled mode (CheckedChanged bound) and the
/// parent vetoes a toggle by re-rendering with the original Checked value, the
/// UI must roll back to the bound value rather than keeping the optimistic toggle.
/// </summary>
public class SwitchControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SwitchControlledRollbackTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Controlled: veto rolls back ---

    [Fact]
    public void Controlled_Veto_Rolls_Back_To_Bound_Value()
    {
        // Parent starts with Checked=false and vetoes every toggle by keeping
        // its own state unchanged (always re-renders with Checked=false).
        bool parentState = false;
        IRenderedComponent<L.Switch>? cut = null;

        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool incoming) =>
        {
            // Veto: do NOT update parentState; re-render with the original value.
            cut!.Render(p =>
            {
                p.Add(b => b.Checked, parentState);   // still false
                p.Add(b => b.CheckedChanged, EventCallback.Factory.Create<bool>(_ctx, (_) => { }));
            });
        });

        cut = _ctx.Render<L.Switch>(p => p
            .Add(b => b.Checked, false)
            .Add(b => b.CheckedChanged, callback));

        Assert.Equal("false", cut.Find("button[role='switch']").GetAttribute("aria-checked"));

        // Click — Toggle sets _checked=true and fires CheckedChanged; the parent
        // vetoes and re-renders with Checked=false.
        cut.Find("button[role='switch']").Click();

        // After veto the UI must have rolled back to false, not stayed at true.
        Assert.Equal("false", cut.Find("button[role='switch']").GetAttribute("aria-checked"));
        Assert.Contains("bg-input", cut.Find("button[role='switch']").GetAttribute("class"));
    }

    // --- Controlled: accepted toggle keeps new value ---

    [Fact]
    public void Controlled_Accepted_Toggle_Keeps_New_Value()
    {
        // Parent accepts every toggle by updating its own state and re-rendering.
        bool parentState = false;
        IRenderedComponent<L.Switch>? cut = null;

        EventCallback<bool> callback = default;
        callback = EventCallback.Factory.Create<bool>(_ctx, (bool incoming) =>
        {
            parentState = incoming;
            cut!.Render(p =>
            {
                p.Add(b => b.Checked, parentState);
                p.Add(b => b.CheckedChanged, callback);
            });
        });

        cut = _ctx.Render<L.Switch>(p => p
            .Add(b => b.Checked, false)
            .Add(b => b.CheckedChanged, callback));

        cut.Find("button[role='switch']").Click();

        // Parent accepted — value should now be true.
        Assert.Equal("true", cut.Find("button[role='switch']").GetAttribute("aria-checked"));
        Assert.Contains("bg-primary", cut.Find("button[role='switch']").GetAttribute("class"));
    }

    // --- Controlled: programmatic parent reset ---

    [Fact]
    public void Controlled_Programmatic_Reset_Is_Adopted()
    {
        // Start checked=true; parent programmatically resets to false WITHOUT
        // the user clicking (simulates an external data reload or form reset).
        var cut = _ctx.Render<L.Switch>(p => p
            .Add(b => b.Checked, true)
            .Add(b => b.CheckedChanged, EventCallback.Factory.Create<bool>(_ctx, (_) => { })));

        Assert.Equal("true", cut.Find("button[role='switch']").GetAttribute("aria-checked"));

        // Parent resets the bound value without a user toggle first.
        cut.Render(p => p
            .Add(b => b.Checked, false)
            .Add(b => b.CheckedChanged, EventCallback.Factory.Create<bool>(_ctx, (_) => { })));

        Assert.Equal("false", cut.Find("button[role='switch']").GetAttribute("aria-checked"));
    }

    // --- Controlled: veto restores track colour ---

    [Fact]
    public void Controlled_Veto_Restores_Unchecked_Track_Colour()
    {
        // Specifically validates that the bg-input / bg-primary CSS class is also
        // rolled back, not just aria-checked (both derive from _checked backing field).
        bool parentState = false;
        IRenderedComponent<L.Switch>? cut = null;

        var callback = EventCallback.Factory.Create<bool>(_ctx, (_) =>
        {
            cut!.Render(p =>
            {
                p.Add(b => b.Checked, parentState);
                p.Add(b => b.CheckedChanged, EventCallback.Factory.Create<bool>(_ctx, (_2) => { }));
            });
        });

        cut = _ctx.Render<L.Switch>(p => p
            .Add(b => b.Checked, false)
            .Add(b => b.CheckedChanged, callback));

        cut.Find("button[role='switch']").Click();

        var cls = cut.Find("button[role='switch']").GetAttribute("class") ?? "";
        Assert.Contains("bg-input", cls);
        Assert.DoesNotContain("bg-primary", cls);
    }
}
