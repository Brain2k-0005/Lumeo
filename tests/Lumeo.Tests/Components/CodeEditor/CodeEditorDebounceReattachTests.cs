using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.CodeEditor;

/// <summary>
/// Lifecycle regression for battle-wave2 #26 — "60ms input debounce silently
/// disabled after any rebuild".
///
/// The editor's change listener debounces OnEditorChange round-trips to 60ms.
/// <c>init()</c> built that debounced listener inline, but <c>rebuild()</c> —
/// reached on every language / theme / readOnly / minimap toggle and on an
/// auto-theme flip — re-attached a BARE, non-debounced listener that called
/// <c>dotNetRef.invokeMethodAsync('OnEditorChange', …)</c> directly. After the
/// first such rebuild the debounce was gone: every keystroke fired an immediate
/// JS→.NET round-trip until the next page load. The destroy() debounce-cancel
/// path also referenced the init-time timer closure, so it could not cancel a
/// timer armed by a post-rebuild listener.
///
/// The fix factors the debounced notifier + its update listener into a single
/// <c>makeChangeListener(core, dotNetRef)</c> factory that BOTH init and rebuild
/// use, and stores the per-listener <c>cancel</c> on the instance as
/// <c>cancelNotify</c> (swapped on rebuild) so destroy() always cancels the
/// CURRENT listener.
///
/// The debounce lives entirely inside code-editor.js (CodeMirror's
/// updateListener), which bUnit's mocked JS runtime never executes — so the
/// authoritative regression is a SOURCE contract on the shipped module
/// (<see cref="Rebuild_Reuses_Shared_Debounced_Listener_Factory"/>). A
/// companion bUnit test guards the C#-observable teardown edge the same fix
/// touches: a rebuild-triggering toggle followed by disposal must still record
/// destroy and must not throw.
/// </summary>
public class CodeEditorDebounceReattachTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo.CodeEditor/js/code-editor.js");
        module.Mode = JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ------------------------------------------------------------------
    // Primary regression: the JS source contract.
    // ------------------------------------------------------------------

    [Fact]
    public void Rebuild_Reuses_Shared_Debounced_Listener_Factory()
    {
        var src = ReadEditorJs();

        // The shared factory must exist and own the 60ms debounce.
        Assert.Contains("function makeChangeListener", src);
        var factory = ExtractBody(src, "function makeChangeListener");
        Assert.Contains("setTimeout(", factory);
        Assert.Contains("60", factory);
        Assert.Contains("invokeMethodAsync('OnEditorChange'", factory);

        // rebuild() must build its listener THROUGH the shared factory (so it
        // inherits the debounce) rather than hand-rolling a bare listener that
        // invokes OnEditorChange directly off the update event.
        var rebuild = ExtractBody(src, "async function rebuild(");
        Assert.Contains("makeChangeListener(", rebuild);

        // The pre-fix bug: rebuild attached its own updateListener whose body
        // called invokeMethodAsync directly with no setTimeout/debounce. Assert
        // that bare, undebounced pattern is gone — rebuild itself must not
        // contain an OnEditorChange invoke (it delegates to the factory).
        Assert.DoesNotContain("invokeMethodAsync('OnEditorChange'", rebuild);
        Assert.DoesNotContain("setTimeout(", rebuild); // no second, divergent debounce

        // init() must also route through the same factory, not a private copy,
        // so the two paths can never drift apart again.
        var init = ExtractBody(src, "export async function init(");
        Assert.Contains("makeChangeListener(", init);
        Assert.DoesNotContain("invokeMethodAsync('OnEditorChange'", init);

        // destroy() must cancel the CURRENT listener's pending timer via the
        // instance-stored handle (swapped on rebuild), not a stale init closure.
        var destroy = ExtractBody(src, "export async function destroy(");
        Assert.Contains("cancelNotify", destroy);
    }

    // ------------------------------------------------------------------
    // Companion: C#-observable teardown after a rebuild-triggering toggle.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Dispose_After_Rebuild_Toggle_Records_Destroy_And_Does_Not_Throw()
    {
        var cut = _ctx.Render<L.CodeEditor>(p => p.Add(c => c.Language, "json"));

        // A language change is one of the parameter toggles whose JS handler runs
        // rebuild() (re-attaching the change listener). The C# side pushes it via
        // setLanguage; assert that fired so we know the rebuild path was exercised.
        cut.Render(p => p.Add(c => c.Language, "javascript"));
        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "setLanguage");

        // Disposing after that rebuild must tear the editor down cleanly: the
        // destroy interop call is recorded (which is also where the current
        // listener's pending debounce timer is cancelled) and nothing throws.
        var ex = await Record.ExceptionAsync(async () => await cut.Instance.DisposeAsync());
        Assert.Null(ex);
        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "destroy");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string ReadEditorJs() => File.ReadAllText(GetEditorJsPath());

    // Walk up from the test assembly directory to the repo root and locate the
    // shipped CodeEditor module. Mirrors ComponentContractTests.GetRegistryPath:
    // some runners add an arch/coverage subfolder under the base dir, so we
    // search upward rather than assume a fixed depth.
    private static string GetEditorJsPath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(
                dir.FullName, "src", "Lumeo.CodeEditor", "wwwroot", "js", "code-editor.js");
            if (File.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException(
            $"Could not locate src/Lumeo.CodeEditor/wwwroot/js/code-editor.js above '{AppContext.BaseDirectory}'.");
    }

    // Returns the brace-balanced body of the function whose signature text is
    // `signatureStart` (matched literally). Lets each assertion scope to one
    // function so e.g. a debounce in init() can't satisfy a claim about rebuild().
    private static string ExtractBody(string src, string signatureStart)
    {
        var sigIdx = src.IndexOf(signatureStart, StringComparison.Ordinal);
        Assert.True(sigIdx >= 0, $"signature not found: {signatureStart}");

        var open = src.IndexOf('{', sigIdx);
        Assert.True(open >= 0, $"no opening brace after: {signatureStart}");

        var depth = 0;
        for (var i = open; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}')
            {
                depth--;
                if (depth == 0) return src.Substring(open, i - open + 1);
            }
        }
        throw new InvalidOperationException($"unbalanced braces after: {signatureStart}");
    }
}
