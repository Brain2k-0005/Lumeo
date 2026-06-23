using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services.Interop;
using L = Lumeo;

namespace Lumeo.Tests.Components.RichTextEditor;

/// <summary>
/// Behavior/interop tests for the TipTap-backed RichTextEditor. The editor surface
/// lives in rich-text-editor.js, so these assert the C# ⇄ JS contract rather than
/// the editing experience itself:
///   - the JS module is imported (by path) on first render,
///   - toolbar toggle buttons (bold/italic) dispatch the right namespaced command
///     into the module, and reflect the active state pushed back from JS via aria-pressed,
///   - the Value/ValueChanged round-trip is wired through the OnContentUpdate JSInvokable.
///
/// The fixture's JSInterop runs in Loose mode (calls are recorded). We additionally
/// stub <c>rte.init</c> to return a non-empty instance id so the component flips to its
/// "initialized" state and the command path actually reaches JS — otherwise loose-mode
/// returns a null id and toolbar clicks are swallowed by the not-initialized guard.
/// </summary>
public class RichTextEditorBehaviorTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Editor/js/rich-text-editor.js";

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();

        // Pre-register the RichTextEditor's own isolated module so we can drive
        // the init handshake and inspect command invocations against it.
        _module = _ctx.JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;

        // rte.init returns the instance id used by every subsequent command — a
        // non-empty string is what flips the component's _initialized flag on.
        _module.Setup<string>("rte.init", _ => true).SetResult("rte-instance-1");

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private void SetActive(RichTextActiveState state) =>
        _module.Setup<RichTextActiveState?>("rte.getActive", _ => true).SetResult(state);

    private static RichTextActiveState ActiveState(bool bold = false, bool italic = false) =>
        new(
            Bold: bold, Italic: italic, Underline: false, Strike: false, Code: false,
            Paragraph: true, Heading1: false, Heading2: false, Heading3: false,
            BulletList: false, OrderedList: false, Blockquote: false, CodeBlock: false,
            Link: false, CanUndo: false, CanRedo: false);

    [Fact]
    public void Imports_js_module_by_path_on_first_render()
    {
        _ctx.Render<L.RichTextEditor>();

        // The dynamic import("./_content/Lumeo.Editor/js/rich-text-editor.js") is the
        // load-bearing contract: it lazy-loads the ~100kB TipTap bundle only for apps
        // that actually mount the editor. Assert it happened with the exact path.
        var import = Assert.Single(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "import" && i.Arguments.Contains(ModulePath));
        Assert.Contains(ModulePath, import.Arguments);
    }

    [Fact]
    public void Init_runs_against_the_module_on_first_render()
    {
        _ctx.Render<L.RichTextEditor>();

        // After the module import, the component hands its content host + options to
        // rte.init. This is the single entry point that boots the editor instance.
        Assert.Contains(_module.Invocations, i => i.Identifier == "rte.init");
    }

    [Fact]
    public void Bold_toolbar_button_dispatches_bold_command_to_module()
    {
        var cut = _ctx.Render<L.RichTextEditor>();

        // Standard toolbar is the default; the Bold button carries its localized aria-label.
        var bold = cut.Find("button[aria-label='Bold']");
        bold.Click();

        // The toolbar forwards ("bold", null) → RichTextCommandAsync → rte.command with
        // payload [instanceId, name]. Verify the name reached the module.
        var command = Assert.Single(
            _module.Invocations,
            i => i.Identifier == "rte.command" && i.Arguments.Contains("bold"));
        Assert.Equal("rte-instance-1", command.Arguments[0]);
        Assert.Equal("bold", command.Arguments[1]);
    }

    [Fact]
    public void Italic_toolbar_button_dispatches_italic_command_to_module()
    {
        var cut = _ctx.Render<L.RichTextEditor>();

        var italic = cut.Find("button[aria-label='Italic']");
        italic.Click();

        Assert.Contains(
            _module.Invocations,
            i => i.Identifier == "rte.command" && i.Arguments.Contains("italic"));
    }

    [Fact]
    public void Toolbar_reflects_active_state_pushed_back_from_js_via_aria_pressed()
    {
        // Seed the active-state JS returns BEFORE first render so the initial
        // RefreshActiveAsync (run after a successful init) sees bold active.
        SetActive(ActiveState(bold: true, italic: false));

        var cut = _ctx.Render<L.RichTextEditor>();

        // aria-pressed mirrors the RichTextActiveState the editor reported: bold on,
        // italic off. This is the toggle-button a11y contract the toolbar exposes.
        Assert.Equal("true", cut.Find("button[aria-label='Bold']").GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.Find("button[aria-label='Italic']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public async Task OnContentUpdate_callback_raises_ValueChanged_with_new_html()
    {
        string? observed = null;
        var cut = _ctx.Render<L.RichTextEditor>(p => p
            .Add(c => c.Value, "<p>initial</p>")
            .Add(c => c.ValueChanged, (string? html) => observed = html));

        // Simulate the JS editor reporting an edit back through the [JSInvokable]
        // OnContentUpdate hook — the Value/ValueChanged round-trip the component promises.
        await cut.InvokeAsync(() => cut.Instance.OnContentUpdate("<p>edited</p>"));

        Assert.Equal("<p>edited</p>", observed);
    }
}
