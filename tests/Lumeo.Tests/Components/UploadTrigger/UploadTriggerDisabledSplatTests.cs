using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.UploadTrigger;

/// <summary>
/// Triage #170 (low, edge-data) — a <c>disabled</c> key carried in the consumer's
/// programmatic <see cref="L.UploadTrigger.AdditionalAttributes"/> dictionary used to
/// be splatted straight onto the inner <c>&lt;InputFile&gt;</c>, silently overriding
/// the dedicated <see cref="L.UploadTrigger.Disabled"/> parameter. A stray
/// <c>disabled=false</c> in that dictionary could re-enable a trigger the parent meant
/// to disable (and vice-versa).
///
/// Note a <c>disabled</c> attribute written directly in markup is NOT the bug surface:
/// Blazor matches component parameters case-insensitively, so <c>&lt;UploadTrigger
/// disabled="true" /&gt;</c> binds to the typed <c>Disabled</c> parameter, never the
/// unmatched-values dictionary. The clobber only happens when a consumer feeds a
/// <c>disabled</c> key through an explicit <c>AdditionalAttributes</c> dictionary, so
/// these tests reproduce exactly that.
///
/// The fix builds the inner input's attribute set in code: the consumer's splat with
/// any <c>disabled</c> key stripped, plus <c>disabled=true</c> only when the typed
/// <c>Disabled</c> is set — making <c>Disabled</c> the single source of truth.
/// </summary>
public class UploadTriggerDisabledSplatTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public UploadTriggerDisabledSplatTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Splatted_Disabled_True_Does_Not_Override_Disabled_False_Parameter()
    {
        // Disabled parameter says ENABLED, but a stray AdditionalAttributes key tries
        // to disable the native input. The dedicated parameter must win, so the input
        // stays enabled (no `disabled` attribute).
        var cut = _ctx.Render<L.UploadTrigger>(p => p
            .Add(t => t.Disabled, false)
            .Add(t => t.AdditionalAttributes, new Dictionary<string, object> { ["disabled"] = true }));

        var input = cut.Find("input[type=file]");
        Assert.False(input.HasAttribute("disabled"));
    }

    [Fact]
    public void Splatted_Disabled_False_Does_Not_Re_Enable_A_Disabled_Trigger()
    {
        // Disabled parameter says DISABLED, but a stray AdditionalAttributes key tries
        // to re-enable the native input. The dedicated parameter must win, so the input
        // stays disabled.
        var cut = _ctx.Render<L.UploadTrigger>(p => p
            .Add(t => t.Disabled, true)
            .Add(t => t.AdditionalAttributes, new Dictionary<string, object> { ["disabled"] = false }));

        var input = cut.Find("input[type=file]");
        Assert.True(input.HasAttribute("disabled"));
    }

    [Fact]
    public void Non_Disabled_Splat_Attributes_Are_Still_Forwarded()
    {
        // The strip is surgical: only `disabled` is removed, every other consumer
        // attribute still reaches the inner input.
        var cut = _ctx.Render<L.UploadTrigger>(p => p
            .Add(t => t.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "picker" }));

        var input = cut.Find("input[type=file]");
        Assert.Equal("picker", input.GetAttribute("data-testid"));
    }
}
