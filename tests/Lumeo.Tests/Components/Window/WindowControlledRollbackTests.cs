using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Window;

/// <summary>
/// Regression tests for the controlled-component rollback fix on Window. When
/// the window is used in fully controlled mode (OpenChanged bound) and the
/// parent vetoes a close by re-rendering with the original Open value, the UI
/// must roll back to the bound (rejected) value rather than permanently
/// showing the optimistic close.
/// </summary>
public class WindowControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public WindowControlledRollbackTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Controlled: veto rolls back ---

    [Fact]
    public void Controlled_Veto_Rolls_Back_To_Bound_Value()
    {
        // Parent starts with Open=true and vetoes every close by keeping its own
        // state unchanged (always re-renders with Open=true).
        bool parentState = true;
        IRenderedComponent<L.Window>? cut = null;

        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool incoming) =>
        {
            // Veto: do NOT update parentState; re-render with the original value.
            cut!.Render(p =>
            {
                p.Add(w => w.Open, parentState);   // still true
                p.Add(w => w.OpenChanged, EventCallback.Factory.Create<bool>(_ctx, (_) => { }));
            });
        });

        cut = _ctx.Render<L.Window>(p => p
            .Add(w => w.Open, true)
            .Add(w => w.OpenChanged, callback)
            .Add(w => w.Title, "Test Window")
            .AddChildContent("body"));

        Assert.NotEmpty(cut.FindAll("[role='dialog']"));

        // Click the close button -> Close() optimistically clears _open; the
        // parent vetoes and re-renders with Open=true (unchanged, rejected).
        cut.Find("button[aria-label='Close']").Click();

        // After the veto the window must have rolled back to open, not stayed
        // closed showing the rejected optimistic state.
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role='dialog']")));
    }

    // --- Controlled: accepted close keeps closed ---

    [Fact]
    public void Controlled_Accepted_Close_Keeps_Closed()
    {
        // Parent accepts every close by updating its own state and re-rendering.
        bool parentState = true;
        IRenderedComponent<L.Window>? cut = null;

        EventCallback<bool> callback = default;
        callback = EventCallback.Factory.Create<bool>(_ctx, (bool incoming) =>
        {
            parentState = incoming;
            cut!.Render(p =>
            {
                p.Add(w => w.Open, parentState);
                p.Add(w => w.OpenChanged, callback);
            });
        });

        cut = _ctx.Render<L.Window>(p => p
            .Add(w => w.Open, true)
            .Add(w => w.OpenChanged, callback)
            .Add(w => w.Title, "Test Window")
            .AddChildContent("body"));

        cut.Find("button[aria-label='Close']").Click();

        // Parent accepted the close — the window must stay closed.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='dialog']")));
    }

    // --- Controlled: programmatic parent close ---

    [Fact]
    public void Controlled_Programmatic_Close_Is_Adopted()
    {
        // Start open; the parent programmatically closes WITHOUT the user
        // closing first (simulates an external close, e.g. a timeout or another
        // action driving the same bound Open value).
        var cut = _ctx.Render<L.Window>(p => p
            .Add(w => w.Open, true)
            .Add(w => w.OpenChanged, EventCallback.Factory.Create<bool>(_ctx, (_) => { }))
            .Add(w => w.Title, "Test Window")
            .AddChildContent("body"));

        Assert.NotEmpty(cut.FindAll("[role='dialog']"));

        cut.Render(p => p.Add(w => w.Open, false));

        Assert.Empty(cut.FindAll("[role='dialog']"));
    }
}
