using System.Linq;
using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AgentMessageList;

/// <summary>
/// Battle-test wave 3 regression coverage for AgentMessageList.
///
/// • #wave3-26 (lifecycle): the auto-scroll observer registration was a one-shot
///   latched on <c>firstRender</c> (<c>if (!firstRender) return;</c>). A runtime
///   toggle of <see cref="Lumeo.AgentMessageList.AutoScroll"/> therefore had no
///   effect — it could neither be enabled late (false→true never registered) nor
///   disabled (true→false never tore the observer down). The fix reconciles the
///   observer against the LIVE AutoScroll state on every render.
///
/// The lifecycle mechanism is asserted via the recorded components.js module
/// invocations (ai.observeAutoScroll / ai.disposeAutoScroll). The service imports
/// the versioned module URL (?v=&lt;assembly-version&gt;), so the test mirrors that
/// URL builder when setting up the handle it verifies against.
/// </summary>
public class AgentMessageListRegressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly BunitJSModuleInterop _module;

    public AgentMessageListRegressionTests()
    {
        _ctx.AddLumeoServices();

        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        _module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        _module.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ── #wave3-26: enable late ────────────────────────────────────────────────
    [Fact]
    public void AutoScroll_Enabled_After_First_Render_Registers_Observer()
    {
        // First render with AutoScroll OFF — nothing to observe, so no registration.
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.AutoScroll, false));
        Assert.DoesNotContain(
            _module.Invocations,
            i => i.Identifier == "ai.observeAutoScroll");

        // The consumer turns auto-scroll on at runtime (e.g. user re-enabled it).
        cut.Render(p => p.Add(x => x.AutoScroll, true));

        // Without the fix the firstRender gate has already passed on render #1, so
        // a late enable never registers. With the fix OnAfterRenderAsync reconciles
        // against the live AutoScroll value and registers on this render.
        cut.WaitForAssertion(() => Assert.Contains(
            _module.Invocations,
            i => i.Identifier == "ai.observeAutoScroll"));
    }

    // ── #wave3-26: disable at runtime ─────────────────────────────────────────
    [Fact]
    public void AutoScroll_Disabled_At_Runtime_Disposes_Observer()
    {
        // Default AutoScroll is true → the observer registers on the first render.
        var cut = _ctx.Render<Lumeo.AgentMessageList>();
        cut.WaitForAssertion(() => Assert.Contains(
            _module.Invocations,
            i => i.Identifier == "ai.observeAutoScroll"));

        // The consumer turns auto-scroll off at runtime.
        cut.Render(p => p.Add(x => x.AutoScroll, false));

        // Without the fix the `if (!firstRender) return;` early-out means a runtime
        // disable never tears the observer down (it would only be released on full
        // disposal). With the fix the reconcile disposes it immediately.
        cut.WaitForAssertion(() => Assert.Contains(
            _module.Invocations,
            i => i.Identifier == "ai.disposeAutoScroll"));
    }
}
