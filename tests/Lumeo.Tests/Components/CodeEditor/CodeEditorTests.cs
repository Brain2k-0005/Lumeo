using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.CodeEditor;

/// <summary>
/// Surface tests for the CodeMirror-backed CodeEditor. The editor itself is
/// JS-interop heavy (the actual editor lives in code-editor.js), so these cover
/// the C# surface: rendering / ARIA, the self-host EsmBase + ShowMinimap options
/// reaching the JS init call, and graceful teardown when init fails.
/// </summary>
public class CodeEditorTests : IAsyncLifetime
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

    [Fact]
    public void Renders_Editor_Host()
    {
        var cut = _ctx.Render<L.CodeEditor>();
        Assert.Contains("lumeo-code-editor", cut.Markup);
    }

    [Fact]
    public void Editable_Editor_Has_Textbox_Role()
    {
        var cut = _ctx.Render<L.CodeEditor>(p => p.Add(c => c.ReadOnly, false));
        var root = cut.Find("div[role]");
        Assert.Equal("textbox", root.GetAttribute("role"));
    }

    [Fact]
    public void ReadOnly_Editor_Has_Document_Role()
    {
        var cut = _ctx.Render<L.CodeEditor>(p => p.Add(c => c.ReadOnly, true));
        var root = cut.Find("div[role]");
        Assert.Equal("document", root.GetAttribute("role"));
    }

    [Fact]
    public void Merges_Class_Parameter()
    {
        var cut = _ctx.Render<L.CodeEditor>(p => p.Add(c => c.Class, "my-editor"));
        Assert.Contains("my-editor", cut.Markup);
    }

    [Fact]
    public void Forwards_Additional_Attributes()
    {
        var cut = _ctx.Render<L.CodeEditor>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "ce" }));
        Assert.Contains("data-testid=\"ce\"", cut.Markup);
    }

    [Fact]
    public void Init_Receives_EsmBase_Override()
    {
        _ctx.Render<L.CodeEditor>(p => p.Add(c => c.EsmBase, "/_content/MyApp/esm"));

        var init = _ctx.JSInterop.Invocations.Single(i => i.Identifier == "init");
        // options is the 2nd arg to init(elementId, options, dotNetRef); it's a
        // Dictionary<string, object?> (trim-safe — see CodeEditor.razor), not an
        // anonymous type.
        var options = (Dictionary<string, object?>)init.Arguments[1]!;
        var esmBase = options["esmBase"];
        Assert.Equal("/_content/MyApp/esm", esmBase);
    }

    [Fact]
    public void Init_Receives_Minimap_Flag()
    {
        _ctx.Render<L.CodeEditor>(p => p.Add(c => c.ShowMinimap, true));

        var init = _ctx.JSInterop.Invocations.Single(i => i.Identifier == "init");
        // Dictionary<string, object?> (trim-safe — see CodeEditor.razor), not an
        // anonymous type.
        var options = (Dictionary<string, object?>)init.Arguments[1]!;
        var minimap = options["minimap"];
        Assert.Equal(true, minimap);
    }

    [Fact]
    public void Toggling_Minimap_Invokes_SetMinimap()
    {
        var cut = _ctx.Render<L.CodeEditor>(p => p.Add(c => c.ShowMinimap, false));
        cut.Render(p => p.Add(c => c.ShowMinimap, true));

        // After the parameter flips, the parameter-sync branch of OnAfterRenderAsync
        // pushes the new state into JS via setMinimap.
        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "setMinimap");
    }

    [Fact]
    public async Task Init_Failure_Does_Not_Crash_And_Cleans_Up()
    {
        // A fresh context where init throws, to exercise the failed-init guard.
        var ctx = new BunitContext();
        ctx.AddLumeoServices();
        var module = ctx.JSInterop.SetupModule("./_content/Lumeo.CodeEditor/js/code-editor.js");
        module.Mode = JSRuntimeMode.Loose;
        module.SetupVoid("init", _ => true).SetException(new JSException("boom"));

        // The component rethrows from OnAfterRenderAsync after cleanup; bUnit surfaces
        // that as the render task fault. We assert it's our JSException (i.e. the guard
        // ran and rethrew) rather than, say, a NullReferenceException from leaked state.
        var ex = Record.Exception(() => ctx.Render<L.CodeEditor>());
        Assert.True(ex is null || ex is JSException || ex?.InnerException is JSException);

        // Disposing after a failed init must not throw (module ref already cleaned up).
        var disposal = await Record.ExceptionAsync(async () => await ctx.DisposeAsync());
        Assert.Null(disposal);
    }
}
