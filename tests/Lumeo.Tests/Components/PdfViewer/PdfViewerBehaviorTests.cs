using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.PdfViewer;

/// <summary>
/// Behavior/interop tests for the PDF.js-backed PdfViewer. The actual rendering
/// lives in the satellite module pdf-viewer.js, so — like the RichTextEditor /
/// CodeEditor / Scheduler behavior suites — these assert the C# ⇄ JS contract
/// rather than pixels on a canvas (which needs a real browser):
///   - mounting with a Src dynamically imports the isolated, version-stamped
///     pdf-viewer.js module (asserted by its exact path), runs <c>load</c>, and
///     paints the first page via the module's <c>renderPage</c> export,
///   - the next / previous page toolbar buttons drive <c>renderPage</c> with the
///     new page number, clamp at the document bounds, and raise PageChanged,
///   - the zoom-in / zoom-out toolbar buttons update the displayed zoom %, raise
///     ZoomChanged, and re-invoke <c>renderPage</c> at the new scale,
///   - disposal tears the canvas down via the module's <c>destroy</c> export.
///
/// The fixture's JSInterop runs in Loose mode (calls are recorded, un-setup calls
/// return defaults). We stub the module's <c>load</c> export to return a non-zero
/// page count — that is what arms CanGoPrev/CanGoNext so the nav buttons leave
/// their disabled state and actually reach JS, instead of being swallowed by the
/// "no pages loaded" guard (loose-mode would otherwise return a null LoadResult,
/// which the component treats as a load failure). Because LoadResult is a private
/// nested type, the stub is registered reflectively against the component's own
/// type — the only seam available from the test assembly. Assertions key off the
/// recorded interop identifiers / arguments and ARIA labels rather than brittle CSS.
/// </summary>
public class PdfViewerBehaviorTests : IAsyncLifetime
{
    private const string Src = "https://example.test/sample.pdf";
    private const int TotalPages = 5;

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    // The component caches the module URL with a ?v= suffix derived from its OWN
    // assembly's informational version (cache-busting on republish). Recompute the
    // same value so SetupModule registers the exact import path the component asks for.
    private static string ModulePath()
    {
        var asm = typeof(L.PdfViewer).Assembly;
        var v = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm.GetName().Version?.ToString()
                ?? "0";
        return $"./_content/Lumeo.PdfViewer/js/pdf-viewer.js?v={v}";
    }

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();

        // Pre-register the viewer's own isolated, version-stamped module so the
        // dynamic import resolves and load/renderPage/destroy are recorded against it.
        _module = _ctx.JSInterop.SetupModule(ModulePath());
        _module.Mode = JSRuntimeMode.Loose;

        // load(canvasId, src) → LoadResult { TotalPages }. The result type is a
        // private nested type, so register the stub reflectively: a real LoadResult
        // with TotalPages > 0 is what flips _totalPages so the nav buttons un-disable.
        StubLoadResult(TotalPages);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Build and register a planned `load` invocation whose result is a real instance
    // of PdfViewer's private LoadResult with the given page count. Mirrors
    //   _module.Setup<LoadResult>("load", _ => true).SetResult(new LoadResult{...})
    // but reaches the inaccessible result type via reflection.
    private void StubLoadResult(int totalPages)
    {
        var loadResultType = typeof(L.PdfViewer)
            .GetNestedType("LoadResult", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PdfViewer.LoadResult nested type not found.");

        var result = Activator.CreateInstance(loadResultType)!;
        loadResultType.GetProperty("TotalPages")!.SetValue(result, totalPages);

        // BunitJSInteropSetupExtensions.Setup<TResult>(BunitJSInterop, string, InvocationMatcher)
        var setupOpen = typeof(BunitJSInteropSetupExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "Setup"
                      && m.IsGenericMethodDefinition
                      && m.GetParameters().Length == 3
                      && m.GetParameters()[1].ParameterType == typeof(string)
                      && m.GetParameters()[2].ParameterType.Name == "InvocationMatcher");

        // InvocationMatcher is a delegate (Action/Predicate-shaped). Match any `load`.
        var matcherType = setupOpen.GetParameters()[2].ParameterType;
        var matchAll = BuildMatchAll(matcherType);

        var handler = setupOpen.MakeGenericMethod(loadResultType)
            .Invoke(null, [_module, "load", matchAll])!;

        // handler.SetResult(result)
        handler.GetType().GetMethod("SetResult")!.Invoke(handler, [result]);
    }

    // InvocationMatcher matches when it does NOT throw / returns true for the call.
    // Build a delegate of its exact type that accepts the invocation and matches all.
    private static Delegate BuildMatchAll(Type matcherType)
    {
        var invoke = matcherType.GetMethod("Invoke")!;
        var returnsBool = invoke.ReturnType == typeof(bool);
        var paramType = invoke.GetParameters()[0].ParameterType;

        var p = System.Linq.Expressions.Expression.Parameter(paramType, "i");
        System.Linq.Expressions.Expression body = returnsBool
            ? System.Linq.Expressions.Expression.Constant(true)
            : System.Linq.Expressions.Expression.Empty();
        var lambda = System.Linq.Expressions.Expression.Lambda(matcherType, body, p);
        return lambda.Compile();
    }

    private IRenderedComponent<L.PdfViewer> RenderViewer(
        Action<ComponentParameterCollectionBuilder<L.PdfViewer>>? extra = null)
    {
        return _ctx.Render<L.PdfViewer>(p =>
        {
            p.Add(c => c.Src, Src);
            extra?.Invoke(p);
        });
    }

    private static IElement Button(IRenderedComponent<L.PdfViewer> cut, string ariaLabel)
        => cut.Find($"[aria-label='{ariaLabel}']");

    [Fact]
    public void Mounting_With_Src_Imports_The_Versioned_PdfViewer_Module_By_Path()
    {
        RenderViewer();

        // The dynamic import("./_content/Lumeo.PdfViewer/js/pdf-viewer.js?v=…") is the
        // load-bearing contract: it lazy-loads the PDF.js bundle only for apps that
        // actually mount a viewer. Assert it happened with the exact versioned path.
        var import = Assert.Single(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "import" && i.Arguments.Contains(ModulePath()));
        Assert.Contains(ModulePath(), import.Arguments);
    }

    [Fact]
    public void Mounting_Calls_Load_Then_Paints_First_Page_Via_RenderPage()
    {
        RenderViewer();

        // First render: load the document, then paint page 1.
        _module.VerifyInvoke("load");
        _module.VerifyInvoke("renderPage");

        // renderPage(canvasId, page, zoom) — the initial paint targets page 1.
        var render = _module.Invocations.First(i => i.Identifier == "renderPage");
        Assert.Equal(1, render.Arguments[1]);
    }

    [Fact]
    public async Task NextButton_Invokes_RenderPage_For_Page_2_And_Raises_PageChanged()
    {
        var changed = new List<int>();
        var cut = RenderViewer(p =>
            p.Add(c => c.PageChanged, EventCallback.Factory.Create<int>(this, v => changed.Add(v))));

        var before = _module.Invocations.Count(i => i.Identifier == "renderPage");
        await cut.InvokeAsync(() => Button(cut, "Next page").Click());

        // Advancing re-paints via renderPage targeting page 2 …
        var renders = _module.Invocations.Where(i => i.Identifier == "renderPage").ToList();
        Assert.True(renders.Count > before, "Next page should trigger another renderPage.");
        Assert.Equal(2, renders[^1].Arguments[1]);

        // … and surfaces the new page to the consumer.
        Assert.Contains(2, changed);
    }

    [Fact]
    public async Task PrevButton_Is_Disabled_On_First_Page_And_Re_Enabled_After_Advancing()
    {
        var cut = RenderViewer();

        // Page 1: there is nowhere to go back to, so Previous is disabled.
        Assert.True(Button(cut, "Previous page").HasAttribute("disabled"));

        // Advance, then Previous becomes a live control again …
        await cut.InvokeAsync(() => Button(cut, "Next page").Click());
        Assert.False(Button(cut, "Previous page").HasAttribute("disabled"));

        // … and going back re-paints page 1.
        await cut.InvokeAsync(() => Button(cut, "Previous page").Click());
        var lastRender = _module.Invocations.Last(i => i.Identifier == "renderPage");
        Assert.Equal(1, lastRender.Arguments[1]);
    }

    [Fact]
    public async Task ZoomIn_Updates_Percent_Label_RaisesZoomChanged_And_Re_Renders()
    {
        var zooms = new List<double>();
        var cut = RenderViewer(p =>
            p.Add(c => c.ZoomChanged, EventCallback.Factory.Create<double>(this, v => zooms.Add(v))));

        // Default zoom 1.0 renders as "100%".
        Assert.Contains("100%", cut.Markup);

        var before = _module.Invocations.Count(i => i.Identifier == "renderPage");
        await cut.InvokeAsync(() => Button(cut, "Zoom in").Click());

        // One ZoomStep (0.25) up → 125%, ZoomChanged fires, and the page re-paints
        // at the new scale (renderPage invoked again).
        Assert.Contains("125%", cut.Markup);
        Assert.Contains(1.25, zooms);
        Assert.True(
            _module.Invocations.Count(i => i.Identifier == "renderPage") > before,
            "Zooming should trigger another renderPage.");
    }

    [Fact]
    public async Task ZoomOut_Lowers_The_Percent_Label_And_RaisesZoomChanged()
    {
        var zooms = new List<double>();
        var cut = RenderViewer(p =>
            p.Add(c => c.ZoomChanged, EventCallback.Factory.Create<double>(this, v => zooms.Add(v))));

        Assert.Contains("100%", cut.Markup);

        await cut.InvokeAsync(() => Button(cut, "Zoom out").Click());

        // One ZoomStep down from 1.0 → 0.75 → "75%".
        Assert.Contains("75%", cut.Markup);
        Assert.Contains(0.75, zooms);
    }

    [Fact]
    public async Task Disposing_Tears_Down_Via_The_Module_Destroy_Export()
    {
        var cut = RenderViewer();
        _module.VerifyInvoke("renderPage"); // module was actually acquired and used

        await cut.Instance.DisposeAsync();

        // Teardown must release the PDF.js render context for this canvas.
        Assert.Contains(_module.Invocations, i => i.Identifier == "destroy");
    }
}
