using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;

namespace Lumeo.Tests.Components.BackToTop;

/// <summary>
/// Regression coverage for the state-on-data-change bug where a runtime change
/// to <see cref="Lumeo.BackToTop.VisibilityThreshold"/> was ignored: registration
/// happened only once in OnAfterRenderAsync(firstRender), so the JS scroll
/// observer kept using the original threshold forever. The fix re-registers the
/// observer from OnParametersSetAsync when the threshold changes after the first
/// render. registerBackToTop tears down the previous listener for the same id,
/// so the latest registration's threshold is the one in effect.
///
/// These tests assert the MECHANISM via the recorded registerBackToTop JSInterop
/// invocations (arg order: [0]=id, [1]=DotNetObjectReference, [2]=threshold).
/// </summary>
public class BackToTopThresholdReRegistrationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BackToTopThresholdReRegistrationTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Changing_VisibilityThreshold_ReRegisters_With_New_Threshold()
    {
        var cut = _ctx.Render<Lumeo.BackToTop>(p => p
            .Add(b => b.VisibilityThreshold, 300));

        // Initial registration uses the original threshold.
        Assert.Contains(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "registerBackToTop" && Equals(i.Arguments[2], 300));

        // Parent re-renders with a different threshold.
        cut.Render(p => p.Add(b => b.VisibilityThreshold, 800));

        // Without the fix this re-registration never happens (registration was
        // one-shot on firstRender), so the new threshold is silently ignored.
        Assert.Contains(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "registerBackToTop" && Equals(i.Arguments[2], 800));
    }

    [Fact]
    public void Re_Render_With_Unchanged_Threshold_Does_Not_Re_Register()
    {
        var cut = _ctx.Render<Lumeo.BackToTop>(p => p
            .Add(b => b.VisibilityThreshold, 300));

        var registrationsAfterFirstRender = _ctx.JSInterop.Invocations
            .Count(i => i.Identifier == "registerBackToTop");

        // An unrelated parent re-render that does not change the threshold.
        cut.Render(p => p.Add(b => b.VisibilityThreshold, 300));

        var registrationsAfterReRender = _ctx.JSInterop.Invocations
            .Count(i => i.Identifier == "registerBackToTop");

        // Same-value re-renders must not churn the JS observer.
        Assert.Equal(registrationsAfterFirstRender, registrationsAfterReRender);
    }
}
