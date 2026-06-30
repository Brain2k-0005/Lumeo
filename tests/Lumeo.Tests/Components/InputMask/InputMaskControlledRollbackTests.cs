using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InputMask;

/// <summary>
/// Regression tests for the controlled-component rollback fix on InputMask.
/// When InputMask is used in controlled mode (ValueChanged bound) and the parent
/// vetoes an edit by re-rendering with the original Value, the masked display must
/// roll back to the bound value rather than keeping the optimistic edit.
/// </summary>
public class InputMaskControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public InputMaskControlledRollbackTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Controlled: veto rolls back ---

    [Fact]
    public void Controlled_Veto_Rolls_Back_To_Bound_Value()
    {
        // Parent starts with Value="123" and vetoes every edit by keeping its own
        // state unchanged (always re-renders with the original Value).
        string? parentState = "123";
        IRenderedComponent<L.InputMask>? cut = null;

        var callback = EventCallback.Factory.Create<string?>(_ctx, (string? incoming) =>
        {
            // Veto: do NOT update parentState; re-render with the original value.
            cut!.Render(p =>
            {
                p.Add(b => b.Mask, "###-###");
                p.Add(b => b.Value, parentState);   // still "123"
                p.Add(b => b.ValueChanged, EventCallback.Factory.Create<string?>(_ctx, (_) => { }));
            });
        });

        cut = _ctx.Render<L.InputMask>(p => p
            .Add(b => b.Mask, "###-###")
            .Add(b => b.Value, parentState)
            .Add(b => b.ValueChanged, callback));

        Assert.Equal("123", cut.Find("input").GetAttribute("value"));

        // Type another digit — HandleInput optimistically updates the masked
        // display and fires ValueChanged; the parent vetoes and re-renders with
        // the original Value.
        cut.Find("input").Input("1234");

        // After veto the UI must roll back to the bound "123", not keep the
        // optimistic "123-4".
        Assert.Equal("123", cut.Find("input").GetAttribute("value"));
    }

    // --- Controlled: accepted edit keeps new value ---

    [Fact]
    public void Controlled_Accepted_Edit_Keeps_New_Value()
    {
        // Parent accepts every edit by updating its own state and re-rendering.
        string? parentState = "123";
        IRenderedComponent<L.InputMask>? cut = null;

        EventCallback<string?> callback = default;
        callback = EventCallback.Factory.Create<string?>(_ctx, (string? incoming) =>
        {
            parentState = incoming;
            cut!.Render(p =>
            {
                p.Add(b => b.Mask, "###-###");
                p.Add(b => b.Value, parentState);
                p.Add(b => b.ValueChanged, callback);
            });
        });

        cut = _ctx.Render<L.InputMask>(p => p
            .Add(b => b.Mask, "###-###")
            .Add(b => b.Value, parentState)
            .Add(b => b.ValueChanged, callback));

        cut.Find("input").Input("1234");

        // Parent accepted — the masked display should reflect the new raw value.
        Assert.Equal("123-4", cut.Find("input").GetAttribute("value"));
    }

    // --- Controlled: programmatic parent reset ---

    [Fact]
    public void Controlled_Programmatic_Reset_Is_Adopted()
    {
        // Start with Value="123456"; parent programmatically resets to "654321"
        // WITHOUT the user editing first (simulates an external data reload).
        var cut = _ctx.Render<L.InputMask>(p => p
            .Add(b => b.Mask, "###-###")
            .Add(b => b.Value, "123456")
            .Add(b => b.ValueChanged, EventCallback.Factory.Create<string?>(_ctx, (_) => { })));

        Assert.Equal("123-456", cut.Find("input").GetAttribute("value"));

        cut.Render(p => p
            .Add(b => b.Mask, "###-###")
            .Add(b => b.Value, "654321")
            .Add(b => b.ValueChanged, EventCallback.Factory.Create<string?>(_ctx, (_) => { })));

        Assert.Equal("654-321", cut.Find("input").GetAttribute("value"));
    }
}
