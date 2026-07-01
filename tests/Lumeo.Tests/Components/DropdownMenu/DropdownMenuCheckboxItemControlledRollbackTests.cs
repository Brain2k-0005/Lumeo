using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// Regression tests for the controlled-component rollback fix on
/// DropdownMenuCheckboxItem. When the item is used in controlled mode
/// (CheckedChanged bound) and the parent vetoes a toggle by re-rendering
/// with the original Checked value, the UI must roll back to the bound value.
/// </summary>
public class DropdownMenuCheckboxItemControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DropdownMenuCheckboxItemControlledRollbackTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Helper: render a standalone DropdownMenuCheckboxItem with optional callback.
    private IRenderedComponent<L.DropdownMenuCheckboxItem> RenderItem(
        bool @checked,
        EventCallback<bool>? checkedChanged = null)
    {
        return _ctx.Render<L.DropdownMenuCheckboxItem>(p =>
        {
            p.Add(b => b.Checked, @checked);
            if (checkedChanged.HasValue)
                p.Add(b => b.CheckedChanged, checkedChanged.Value);
            p.Add(b => b.ChildContent, (RenderFragment)(b => b.AddContent(0, "Bold")));
        });
    }

    // --- Controlled: veto rolls back ---

    [Fact]
    public void Controlled_Veto_Rolls_Back_To_Bound_Value()
    {
        // Parent starts with Checked=false and vetoes every toggle by keeping
        // its own state unchanged (i.e. it always re-renders with Checked=false).
        bool parentState = false;
        IRenderedComponent<L.DropdownMenuCheckboxItem>? cut = null;

        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool incoming) =>
        {
            // Veto: do NOT update parentState; re-render with the original value.
            cut!.Render(p =>
            {
                p.Add(b => b.Checked, parentState);          // still false
                p.Add(b => b.CheckedChanged, EventCallback.Factory.Create<bool>(_ctx, (_) => { }));
                p.Add(b => b.ChildContent, (RenderFragment)(b => b.AddContent(0, "Bold")));
            });
        });

        cut = RenderItem(@checked: false, checkedChanged: callback);

        // aria-checked must start as "false"
        Assert.Equal("false", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));

        // Click — Toggle sets _checked=true and fires CheckedChanged; the parent
        // vetoes and re-renders with Checked=false.
        cut.Find("[role='menuitemcheckbox']").Click();

        // After veto the UI must have rolled back to false, not stayed at true.
        Assert.Equal("false", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));
    }

    // --- Controlled: accepted toggle keeps new value ---

    [Fact]
    public void Controlled_Accepted_Toggle_Keeps_New_Value()
    {
        // Parent accepts every toggle by updating its own state and re-rendering.
        bool parentState = false;
        IRenderedComponent<L.DropdownMenuCheckboxItem>? cut = null;

        EventCallback<bool> callback = default;
        callback = EventCallback.Factory.Create<bool>(_ctx, (bool incoming) =>
        {
            parentState = incoming;
            cut!.Render(p =>
            {
                p.Add(b => b.Checked, parentState);
                p.Add(b => b.CheckedChanged, callback);
                p.Add(b => b.ChildContent, (RenderFragment)(b => b.AddContent(0, "Bold")));
            });
        });

        cut = RenderItem(@checked: false, checkedChanged: callback);

        cut.Find("[role='menuitemcheckbox']").Click();

        // Parent accepted — value should now be true.
        Assert.Equal("true", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));
    }

    // --- Controlled: programmatic parent reset ---

    [Fact]
    public void Controlled_Programmatic_Reset_Is_Adopted()
    {
        // Start checked=true; parent programmatically resets to false WITHOUT
        // the user clicking (simulates an external data reload or form reset).
        var cut = RenderItem(@checked: true,
            checkedChanged: EventCallback.Factory.Create<bool>(_ctx, (_) => { }));

        Assert.Equal("true", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));

        // Parent resets the bound value without a user toggle first.
        cut.Render(p =>
        {
            p.Add(b => b.Checked, false);
            p.Add(b => b.CheckedChanged, EventCallback.Factory.Create<bool>(_ctx, (_) => { }));
            p.Add(b => b.ChildContent, (RenderFragment)(b => b.AddContent(0, "Bold")));
        });

        Assert.Equal("false", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));
    }

    // --- Uncontrolled: stale re-render does NOT clobber optimistic toggle ---

    [Fact]
    public void Uncontrolled_StaleReRender_Does_Not_Revert_Toggled_State()
    {
        // No CheckedChanged binding — the item owns its own checked state.
        var cut = RenderItem(@checked: false);

        cut.Find("[role='menuitemcheckbox']").Click();
        Assert.Equal("true", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));

        // Unrelated parent re-render re-supplies the original (stale) Checked=false.
        // The optimistic toggle must survive.
        cut.Render(p =>
        {
            p.Add(b => b.Checked, false);
            p.Add(b => b.ChildContent, (RenderFragment)(b => b.AddContent(0, "Bold")));
        });

        Assert.Equal("true", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));
    }
}
