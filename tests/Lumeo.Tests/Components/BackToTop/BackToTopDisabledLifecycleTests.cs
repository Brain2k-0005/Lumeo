using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;

namespace Lumeo.Tests.Components.BackToTop;

/// <summary>
/// Regression coverage for the lifecycle bug (#188) where a Disabled BackToTop
/// still registered a window scroll listener and crossed the JS↔.NET interop
/// boundary, contradicting the contract that Disabled "disables scroll tracking
/// entirely". The render gate <c>@if (_visible &amp;&amp; !Disabled)</c> only hid the
/// markup; OnAfterRenderAsync registered the observer unconditionally on
/// firstRender and never checked Disabled, and a runtime toggle was never
/// reconciled.
///
/// The fix skips registration on first render when Disabled, and reconciles a
/// runtime Disabled toggle in OnParametersSetAsync (unregister when it flips
/// true, register when it flips false). These tests assert the MECHANISM via the
/// recorded registerBackToTop / unregisterBackToTop JSInterop invocations (loose
/// mode), counting them so a bare markup gate cannot satisfy them.
/// </summary>
public class BackToTopDisabledLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BackToTopDisabledLifecycleTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private int RegisterCount() =>
        _ctx.JSInterop.Invocations.Count(i => i.Identifier == "registerBackToTop");

    private int UnregisterCount() =>
        _ctx.JSInterop.Invocations.Count(i => i.Identifier == "unregisterBackToTop");

    [Fact]
    public void Disabled_At_First_Render_Does_Not_Register_The_Scroll_Observer()
    {
        _ctx.Render<Lumeo.BackToTop>(p => p
            .Add(b => b.Disabled, true));

        // Without the fix OnAfterRenderAsync(firstRender) registered the observer
        // unconditionally, crossing the JS↔.NET boundary even though scroll
        // tracking is supposed to be disabled entirely.
        Assert.Equal(0, RegisterCount());
    }

    [Fact]
    public void Enabled_At_First_Render_Still_Registers_Exactly_Once()
    {
        // Guard the normal-path behaviour: the non-disabled case must be unchanged.
        _ctx.Render<Lumeo.BackToTop>();

        Assert.Equal(1, RegisterCount());
    }

    [Fact]
    public void Toggling_Disabled_True_At_Runtime_Unregisters_The_Observer()
    {
        var cut = _ctx.Render<Lumeo.BackToTop>();

        // Registered while enabled, nothing torn down yet.
        Assert.Equal(1, RegisterCount());
        Assert.Equal(0, UnregisterCount());

        // Disable at runtime: the observer must be torn down so scroll tracking
        // truly stops, not merely hidden by the render gate.
        cut.Render(p => p.Add(b => b.Disabled, true));

        Assert.Equal(1, UnregisterCount());
    }

    [Fact]
    public void Re_Enabling_After_Disabled_First_Render_Registers_The_Observer()
    {
        // Disabled on first render -> no registration (the bug's core case).
        var cut = _ctx.Render<Lumeo.BackToTop>(p => p
            .Add(b => b.Disabled, true));
        Assert.Equal(0, RegisterCount());

        // Flip back to enabled: registration must now happen so the affordance
        // becomes functional again.
        cut.Render(p => p.Add(b => b.Disabled, false));

        Assert.Equal(1, RegisterCount());
    }

    [Fact]
    public async Task Disposing_Does_Not_Throw_When_Disabled_Never_Registered()
    {
        var cut = _ctx.Render<Lumeo.BackToTop>(p => p
            .Add(b => b.Disabled, true));

        // Teardown must be safe even though no observer was ever registered.
        var exception = await Record.ExceptionAsync(async () =>
            await cut.Instance.DisposeAsync());

        Assert.Null(exception);
    }
}
