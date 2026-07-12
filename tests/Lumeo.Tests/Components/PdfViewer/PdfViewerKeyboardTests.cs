using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.PdfViewer;

/// <summary>
/// Keyboard coverage for PdfViewer's custom document-viewer key handler
/// (HandleKeyDown on the `role="document"` scroll container):
/// PageDown/ArrowRight → next page, PageUp/ArrowLeft → previous page (both
/// clamped at the document bounds), "+"/"=" → zoom in, "-"/"_" → zoom out.
/// The page-number and search &lt;input&gt;s carry `@onkeydown:stopPropagation`
/// so typing digits/characters never leaks into page/zoom navigation — bUnit
/// surfaces that as <see cref="MissingEventHandlerException"/> when the event
/// can't bubble to the container's handler (see
/// DataGridColumnFilterKeyboardA11yTests for the same pattern).
///
/// Mirrors PdfViewerBehaviorTests' module-stub setup: pdf.js runs off-page, so
/// `load` is stubbed (via reflection, LoadResult is a private nested type) to
/// return a fixed TotalPages — that's what arms the page-clamp math in
/// SetPageAsync (a zero-page document treats every navigation as a no-op).
/// </summary>
public class PdfViewerKeyboardTests : IAsyncLifetime
{
    private const string Src = "https://example.test/sample.pdf";
    private const int TotalPages = 5;

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

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
        _module = _ctx.JSInterop.SetupModule(ModulePath());
        _module.Mode = JSRuntimeMode.Loose;
        StubLoadResult(TotalPages);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private void StubLoadResult(int totalPages)
    {
        var loadResultType = typeof(L.PdfViewer).GetNestedType("LoadResult", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PdfViewer.LoadResult nested type not found.");

        var result = Activator.CreateInstance(loadResultType)!;
        loadResultType.GetProperty("TotalPages")!.SetValue(result, totalPages);

        var setupOpen = typeof(BunitJSInteropSetupExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "Setup"
                      && m.IsGenericMethodDefinition
                      && m.GetParameters().Length == 3
                      && m.GetParameters()[1].ParameterType == typeof(string)
                      && m.GetParameters()[2].ParameterType.Name == "InvocationMatcher");

        var matcherType = setupOpen.GetParameters()[2].ParameterType;
        var matchAll = BuildMatchAll(matcherType);

        var handler = setupOpen.MakeGenericMethod(loadResultType).Invoke(null, [_module, "load", matchAll])!;
        handler.GetType().GetMethod("SetResult")!.Invoke(handler, [result]);
    }

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
        => _ctx.Render<L.PdfViewer>(p =>
        {
            p.Add(c => c.Src, Src);
            extra?.Invoke(p);
        });

    private static IElement Container(IRenderedComponent<L.PdfViewer> cut)
        => cut.Find("[role='document']");

    private static int LastRenderedPage(BunitJSModuleInterop module)
        => (int)module.Invocations.Last(i => i.Identifier == "renderPage").Arguments[1]!;

    // --- PageDown / ArrowRight — next page ---

    [Theory]
    [InlineData("PageDown")]
    [InlineData("ArrowRight")]
    public async Task Key_Advances_To_Next_Page(string key)
    {
        var cut = RenderViewer();

        await cut.InvokeAsync(() => Container(cut).KeyDown(key));

        Assert.Equal(2, LastRenderedPage(_module));
    }

    [Fact]
    public async Task ArrowRight_Clamps_At_The_Last_Page()
    {
        var cut = RenderViewer();

        // 4 presses reach the last page (5); a 5th must NOT overshoot.
        for (var i = 0; i < 4; i++)
            await cut.InvokeAsync(() => Container(cut).KeyDown("ArrowRight"));
        Assert.Equal(TotalPages, LastRenderedPage(_module));

        var rendersBeforeOverflow = _module.Invocations.Count(x => x.Identifier == "renderPage");
        await cut.InvokeAsync(() => Container(cut).KeyDown("ArrowRight"));

        // No new renderPage call — already-at-bound navigation is a no-op.
        Assert.Equal(rendersBeforeOverflow, _module.Invocations.Count(x => x.Identifier == "renderPage"));
        Assert.Equal(TotalPages, LastRenderedPage(_module));
    }

    // --- PageUp / ArrowLeft — previous page ---

    [Theory]
    [InlineData("PageUp")]
    [InlineData("ArrowLeft")]
    public async Task Key_Goes_To_Previous_Page(string key)
    {
        var cut = RenderViewer();
        await cut.InvokeAsync(() => Container(cut).KeyDown("ArrowRight")); // land on page 2
        Assert.Equal(2, LastRenderedPage(_module));

        await cut.InvokeAsync(() => Container(cut).KeyDown(key));

        Assert.Equal(1, LastRenderedPage(_module));
    }

    [Fact]
    public async Task ArrowLeft_Clamps_At_The_First_Page()
    {
        var cut = RenderViewer();
        var rendersAtFirstPage = _module.Invocations.Count(x => x.Identifier == "renderPage");

        await cut.InvokeAsync(() => Container(cut).KeyDown("ArrowLeft"));

        // Already on page 1 — going further back is a clamped no-op, no re-render.
        Assert.Equal(rendersAtFirstPage, _module.Invocations.Count(x => x.Identifier == "renderPage"));
    }

    // --- +/= zoom in, -/_ zoom out ---

    [Theory]
    [InlineData("+")]
    [InlineData("=")]
    public async Task Key_Zooms_In_By_ZoomStep(string key)
    {
        var cut = RenderViewer();
        Assert.Contains("100%", cut.Markup);

        await cut.InvokeAsync(() => Container(cut).KeyDown(key));

        Assert.Contains("125%", cut.Markup);
    }

    [Theory]
    [InlineData("-")]
    [InlineData("_")]
    public async Task Key_Zooms_Out_By_ZoomStep(string key)
    {
        var cut = RenderViewer();
        Assert.Contains("100%", cut.Markup);

        await cut.InvokeAsync(() => Container(cut).KeyDown(key));

        Assert.Contains("75%", cut.Markup);
    }

    // --- stopPropagation guards on the page-number / search inputs ---

    [Fact]
    public void Typing_In_Page_Number_Input_Does_Not_Bubble_To_The_Container()
    {
        var cut = RenderViewer();
        var input = cut.Find("input[aria-label='Page number']");

        // The input has @onkeydown:stopPropagation and no own keydown handler,
        // so bUnit can't route the event to the container's HandleKeyDown —
        // it throws instead of silently reaching it (see
        // DataGridColumnFilterKeyboardA11yTests for the same assertion shape).
        Assert.Throws<MissingEventHandlerException>(() => input.KeyDown("ArrowRight"));

        // No page navigation happened as a side effect of the (blocked) bubble.
        Assert.Equal(1, LastRenderedPage(_module));
    }

    [Fact]
    public void Typing_In_Search_Input_Does_Not_Bubble_To_The_Container()
    {
        var cut = RenderViewer(p => p.Add(c => c.ShowSearch, true));
        var input = cut.Find("input[aria-label='Search PDF']");

        Assert.Throws<MissingEventHandlerException>(() => input.KeyDown("+"));

        // Zoom must be unaffected by the (blocked) bubble.
        Assert.Contains("100%", cut.Markup);
    }
}
