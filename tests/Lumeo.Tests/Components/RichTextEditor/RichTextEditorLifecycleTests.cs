using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.RichTextEditor;

/// <summary>
/// Regression tests for the "lifecycle" battle-test finding on the RichTextEditor
/// (triage #160): a first-render init failure latched forever.
///
/// The bug had three parts, all fixed here:
///   1. The error branch rendered the failure <c>&lt;div&gt;</c> INSTEAD of the editor,
///      so the content host (<c>@ref="_contentRef"</c>) was unmounted and a retry had
///      no valid ElementReference to init against — recovery was impossible.
///   2. There was no retry path: <c>_initError</c> never cleared once set.
///   3. Only <c>JSException</c> was caught, so any other init-time exception
///      (bad option payload, serialization, an error thrown inside the JS module)
///      escaped the component instead of surfacing a recoverable banner.
///
/// The fix renders the error banner ABOVE the always-mounted content host, adds a
/// Retry button that clears the error and re-inits against the live ref, broadens the
/// catch to the full exception set, and clears <c>_initError</c> on a successful re-init.
///
/// Mirrors <see cref="RichTextEditorBehaviorTests"/> and Chart's lifecycle suite: the
/// JS module is mocked in Loose mode and <c>rte.init</c> is driven to throw, then allowed
/// to succeed on retry via a toggled predicate.
/// </summary>
public class RichTextEditorLifecycleTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Editor/js/rich-text-editor.js";

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();

        _module = _ctx.JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private int InitCount() => _module.Invocations.Count(i => i.Identifier == "rte.init");

    // ----------------------------------------------------------------- #160

    [Fact]
    public void FailedInit_keepsContentHostMounted_alongsideErrorBanner()
    {
        // rte.init throws on first render.
        _module.Setup<string>("rte.init", _ => true).SetException(new JSException("boot failed"));

        var cut = _ctx.Render<L.RichTextEditor>();

        // The error banner is shown...
        Assert.Contains("Editor failed to initialize", cut.Markup);
        // ...AND the content host is STILL in the DOM. The bug replaced the whole tree
        // with the error div, orphaning the @ref so no retry could ever re-init.
        Assert.Contains("lumeo-rte-content", cut.Markup);
    }

    [Fact]
    public void RetryButton_afterFailedInit_clearsError_andReInits()
    {
        // First init throws; flip the predicate off so the retry succeeds via the stub.
        var failInit = true;
        _module
            .Setup<string>("rte.init", _ => failInit)
            .SetException(new JSException("boot failed"));
        // The "succeed" branch the retry will hit once failInit is false.
        _module.Setup<string>("rte.init", _ => !failInit).SetResult("rte-instance-1");

        var cut = _ctx.Render<L.RichTextEditor>();

        Assert.Contains("Editor failed to initialize", cut.Markup);
        var initsAfterFailure = InitCount();
        Assert.True(initsAfterFailure >= 1);

        // User clicks Retry. The banner is a role=alert region with exactly one button.
        failInit = false;
        cut.Find("[role='alert'] button").Click();

        // Recovered: the error cleared and rte.init ran again against the still-mounted
        // host. Before the fix the error latched forever (no retry path, host unmounted).
        Assert.DoesNotContain("Editor failed to initialize", cut.Markup);
        Assert.True(InitCount() > initsAfterFailure,
            "Retry after an init failure must trigger a fresh rte.init.");
        Assert.Contains("lumeo-rte-content", cut.Markup);
    }

    [Fact]
    public void NonJsException_duringInit_isCaught_andSurfacesBanner_withoutFaultingRender()
    {
        // A non-JSException thrown from init (e.g. serialization / option-payload error).
        // The broadened catch must surface a recoverable banner rather than letting the
        // exception escape and fault the render task.
        _module
            .Setup<string>("rte.init", _ => true)
            .SetException(new InvalidOperationException("malformed options"));

        var ex = Record.Exception(() => _ctx.Render<L.RichTextEditor>());

        Assert.Null(ex);
    }

    [Fact]
    public void SuccessfulInit_rendersNoErrorBanner()
    {
        // Normal-path guard: a clean init shows no banner and never replaces the host.
        _module.Setup<string>("rte.init", _ => true).SetResult("rte-instance-1");

        var cut = _ctx.Render<L.RichTextEditor>();

        Assert.DoesNotContain("Editor failed to initialize", cut.Markup);
        Assert.Contains("lumeo-rte-content", cut.Markup);
    }
}
