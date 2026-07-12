using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services.Interop;
using L = Lumeo;

namespace Lumeo.Tests.Components.RichTextEditor;

/// <summary>
/// Keyboard coverage for RichTextEditor's own Blazor-level key handling:
///   - the link-insert dialog's HandleLinkDialogKey on its URL &lt;Input&gt;:
///     Enter applies the link (dispatches the "link" command to JS and
///     closes), Escape cancels (closes WITHOUT dispatching);
///   - the toolbar's buttons are plain native &lt;button&gt;s (role="toolbar"
///     on the wrapper, but NO roving-tabindex is wired — a real APG-toolbar
///     gap, out of scope to fix here), so Tab/Enter/Space are the browser's
///     native semantics; command-dispatch-on-click is already covered by
///     RichTextEditorBehaviorTests (Bold_toolbar_button_dispatches_bold_command_to_module)
///     — this file only asserts the element IS a real button, not a div.
///
/// TriggerDropdown (slash-command / mention suggestion list) carries NO
/// @onkeydown of its own — verified against TriggerDropdown.razor, whose own
/// doc comment says Up/Down/Enter/Escape are handled by the JS suggestion
/// plugin in the live editor (or by a consumer wiring their own keys for a
/// server-rendered trigger UI). There is nothing to drive through bUnit here;
/// a dedicated test pins that reality down instead of asserting fictional
/// Blazor-level key handling.
/// </summary>
public class RichTextEditorKeyboardTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Editor/js/rich-text-editor.js";

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        _module = _ctx.JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
        _module.Setup<string>("rte.init", _ => true).SetResult("rte-instance-1");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // DialogContent keeps its role="dialog" div mounted (aria-modal flips to
    // "false") during its exit animation — presence alone is not "open"; the
    // ConsentBanner/DatePicker suites use the same aria-modal='true' signal.
    private static bool DialogOpen(IRenderedComponent<L.RichTextEditor> cut)
        => cut.FindAll("div[role='dialog'][aria-modal='true']").Count > 0;

    private static void OpenLinkDialog(IRenderedComponent<L.RichTextEditor> cut)
    {
        cut.Find("button[aria-label='Link']").Click();
        Assert.True(DialogOpen(cut));
    }

    [Fact]
    public void Enter_In_Link_Dialog_Applies_The_Link_And_Closes()
    {
        var cut = _ctx.Render<L.RichTextEditor>();
        OpenLinkDialog(cut);

        var urlInput = cut.Find("div[role='dialog'] input");
        urlInput.Input("https://example.test");
        urlInput.KeyDown("Enter");

        var command = Assert.Single(
            _module.Invocations,
            i => i.Identifier == "rte.command" && i.Arguments.Contains("link"));
        Assert.Equal("rte-instance-1", command.Arguments[0]);
        Assert.Equal("link", command.Arguments[1]);

        Assert.False(DialogOpen(cut));
    }

    [Fact]
    public void Escape_In_Link_Dialog_Cancels_Without_Dispatching_The_Link_Command()
    {
        var cut = _ctx.Render<L.RichTextEditor>();
        OpenLinkDialog(cut);

        var urlInput = cut.Find("div[role='dialog'] input");
        urlInput.Input("https://example.test");
        urlInput.KeyDown("Escape");

        Assert.DoesNotContain(
            _module.Invocations,
            i => i.Identifier == "rte.command" && i.Arguments.Contains("link"));
        Assert.False(DialogOpen(cut));
    }

    // --- Toolbar buttons are real <button>s, not divs pretending to be one ---

    [Fact]
    public void Toolbar_Bold_Button_Is_A_Native_Button_Element()
    {
        var cut = _ctx.Render<L.RichTextEditor>();

        var bold = cut.Find("button[aria-label='Bold']");
        Assert.Equal("button", bold.TagName.ToLowerInvariant());
        Assert.Equal("button", bold.GetAttribute("type"));
        // No roving tabindex despite role="toolbar" on the wrapper — plain
        // native Tab order across every toolbar button (documented gap vs.
        // the APG toolbar pattern, out of scope to fix here).
        Assert.Null(bold.GetAttribute("tabindex"));
    }

    // --- TriggerDropdown: no Blazor-level key handling exists ---

    [Fact]
    public void TriggerDropdown_Has_No_Keydown_Handling_Of_Its_Own()
    {
        var items = new List<L.TriggerItem>
        {
            new("h1", "Heading 1"),
            new("bullet", "Bullet list"),
        };

        var cut = _ctx.Render<L.TriggerDropdown>(p => p
            .Add(d => d.Items, items)
            .Add(d => d.SelectedIndex, 0));

        var listbox = cut.Find("[role='listbox']");
        // Firing a key on it must not silently do anything useful — there is
        // no @onkeydown wired at all, so bUnit simply finds no handler to
        // invoke (no crash, but also no navigation). This documents the real
        // contract: Up/Down/Enter/Escape are driven by the JS suggestion
        // plugin (or a consumer's own wiring), never by this component.
        Assert.False(listbox.HasAttribute("blazor:onkeydown"));
    }

    // --- The contenteditable host is a single, focusable surface ---

    [Fact]
    public void Content_Host_Renders_As_A_Single_Textbox_Region_Without_A_Blazor_Level_Tabindex_Override()
    {
        // TipTap sets `contenteditable` (and thus native focusability) via JS
        // once mounted — invisible to bUnit's static markup — but the Razor
        // side must not accidentally pin tabindex="-1" on the host, which
        // would remove it from the Tab order regardless of what JS does.
        var cut = _ctx.Render<L.RichTextEditor>();

        var host = cut.Find("[role='textbox']");
        Assert.NotEqual("-1", host.GetAttribute("tabindex"));
    }
}
