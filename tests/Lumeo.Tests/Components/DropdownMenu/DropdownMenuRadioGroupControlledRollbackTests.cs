using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// Regression tests for the controlled-component rollback fix on
/// DropdownMenuRadioGroup. When the group is used in controlled mode
/// (ValueChanged bound) and the parent vetoes a selection by re-rendering
/// with the original Value, the UI must roll back to the bound value.
/// </summary>
public class DropdownMenuRadioGroupControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DropdownMenuRadioGroupControlledRollbackTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment RadioItems => b =>
    {
        b.OpenComponent<L.DropdownMenuRadioItem>(0);
        b.AddAttribute(1, "Value", "a");
        b.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
        b.CloseComponent();

        b.OpenComponent<L.DropdownMenuRadioItem>(3);
        b.AddAttribute(4, "Value", "b");
        b.AddAttribute(5, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Banana")));
        b.CloseComponent();
    };

    // --- Controlled: veto rolls back ---

    [Fact]
    public void Controlled_Veto_Rolls_Back_To_Bound_Value()
    {
        // Parent starts with Value="a" and vetoes every selection by keeping
        // its own state unchanged (always re-renders with Value="a").
        var parentState = "a";
        IRenderedComponent<L.DropdownMenuRadioGroup>? cut = null;

        var callback = EventCallback.Factory.Create<string>(_ctx, (string incoming) =>
        {
            // Veto: do NOT update parentState; re-render with the original value.
            cut!.Render(p =>
            {
                p.Add(g => g.Value, parentState);   // still "a"
                p.Add(g => g.ValueChanged, EventCallback.Factory.Create<string>(_ctx, (_) => { }));
                p.Add(g => g.ChildContent, RadioItems);
            });
        });

        cut = _ctx.Render<L.DropdownMenuRadioGroup>(p => p
            .Add(g => g.Value, "a")
            .Add(g => g.ValueChanged, callback)
            .Add(g => g.ChildContent, RadioItems));

        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[0].GetAttribute("aria-checked"));
        Assert.Equal("false", cut.FindAll("[role='menuitemradio']")[1].GetAttribute("aria-checked"));

        // Click "Banana" — OnValueSelected sets _value="b" and fires ValueChanged; the
        // parent vetoes and re-renders with Value="a".
        cut.FindAll("[role='menuitemradio']")[1].Click();

        // After veto the UI must have rolled back to "a" selected, not stayed at "b".
        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[0].GetAttribute("aria-checked"));
        Assert.Equal("false", cut.FindAll("[role='menuitemradio']")[1].GetAttribute("aria-checked"));
    }

    // --- Controlled: accepted selection keeps new value ---

    [Fact]
    public void Controlled_Accepted_Selection_Keeps_New_Value()
    {
        // Parent accepts every selection by updating its own state and re-rendering.
        var parentState = "a";
        IRenderedComponent<L.DropdownMenuRadioGroup>? cut = null;

        EventCallback<string> callback = default;
        callback = EventCallback.Factory.Create<string>(_ctx, (string incoming) =>
        {
            parentState = incoming;
            cut!.Render(p =>
            {
                p.Add(g => g.Value, parentState);
                p.Add(g => g.ValueChanged, callback);
                p.Add(g => g.ChildContent, RadioItems);
            });
        });

        cut = _ctx.Render<L.DropdownMenuRadioGroup>(p => p
            .Add(g => g.Value, "a")
            .Add(g => g.ValueChanged, callback)
            .Add(g => g.ChildContent, RadioItems));

        cut.FindAll("[role='menuitemradio']")[1].Click();

        // Parent accepted — "b" should now be selected.
        Assert.Equal("false", cut.FindAll("[role='menuitemradio']")[0].GetAttribute("aria-checked"));
        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[1].GetAttribute("aria-checked"));
    }

    // --- Controlled: programmatic parent reset ---

    [Fact]
    public void Controlled_Programmatic_Reset_Is_Adopted()
    {
        // Start with Value="b"; parent programmatically resets to "a" WITHOUT
        // the user clicking first (simulates an external data reload or form reset).
        var cut = _ctx.Render<L.DropdownMenuRadioGroup>(p => p
            .Add(g => g.Value, "b")
            .Add(g => g.ValueChanged, EventCallback.Factory.Create<string>(_ctx, (_) => { }))
            .Add(g => g.ChildContent, RadioItems));

        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[1].GetAttribute("aria-checked"));

        // Parent resets the bound value without a user selection first.
        cut.Render(p =>
        {
            p.Add(g => g.Value, "a");
            p.Add(g => g.ValueChanged, EventCallback.Factory.Create<string>(_ctx, (_) => { }));
            p.Add(g => g.ChildContent, RadioItems);
        });

        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[0].GetAttribute("aria-checked"));
        Assert.Equal("false", cut.FindAll("[role='menuitemradio']")[1].GetAttribute("aria-checked"));
    }

    // --- Uncontrolled: stale re-render does NOT clobber optimistic selection ---

    [Fact]
    public void Uncontrolled_StaleReRender_Does_Not_Revert_Selected_State()
    {
        // No ValueChanged binding — the group owns its own selected state.
        var cut = _ctx.Render<L.DropdownMenuRadioGroup>(p => p
            .Add(g => g.ChildContent, RadioItems));

        cut.FindAll("[role='menuitemradio']")[1].Click();
        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[1].GetAttribute("aria-checked"));

        // Unrelated parent re-render re-supplies the original (stale, still-null) Value.
        // The optimistic selection must survive.
        cut.Render(p => p.Add(g => g.ChildContent, RadioItems));

        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[1].GetAttribute("aria-checked"));
        Assert.Equal("false", cut.FindAll("[role='menuitemradio']")[0].GetAttribute("aria-checked"));
    }
}
