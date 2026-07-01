using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.PdfViewer;

/// <summary>
/// battle-wave2 #140 (medium, state-on-data-change) — "Stale search query and gone
/// highlights after Src changes to a new document."
///
/// Mechanism: when <c>Src</c> changes to a new document, the Src-change reset block
/// in <c>OnAfterRenderAsync</c> cleared <c>_searchResult</c>, <c>_highlights</c>,
/// <c>_activeMatchIndex</c> and <c>_activeMatchOnPage</c> — but NOT
/// <c>_searchQuery</c>. The search box binds <c>value="@_searchQuery"</c>, so the
/// previous document's typed query stayed visible in the input while its results
/// panel and highlight overlays were already gone — a mismatched, stale state. A
/// subsequent edit/Enter then re-ran a search seeded with the old document's text.
///
/// The fix also resets <c>_searchQuery = string.Empty;</c> in that block, so the
/// box empties together with the results when a fresh document loads.
///
/// bUnit can't run the real PDF.js canvas, so — mirroring PdfViewerBehaviorTests —
/// the fixture stubs the isolated, version-stamped module's <c>load</c> export
/// (loose mode otherwise returns a null LoadResult, which the component treats as a
/// load failure) and drives the C# ⇄ JS contract. The C#-observable symptom of the
/// bug is the search input's bound value: it must be empty after the document
/// changes. This FAILS against the pre-fix component (old query stranded) and
/// PASSES with the reset.
/// </summary>
public class PdfViewerSearchResetTests : IAsyncLifetime
{
    private const string SrcA = "https://example.test/doc-a.pdf";
    private const string SrcB = "https://example.test/doc-b.pdf";
    private const int TotalPages = 3;

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    // The component caches the module URL with a ?v= suffix derived from its OWN
    // assembly's informational version. Recompute it so SetupModule registers the
    // exact import path the component asks for.
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

        // load(canvasId, src) → LoadResult { TotalPages }. The private nested result
        // type is stubbed reflectively (mirrors PdfViewerBehaviorTests) so a non-zero
        // page count arms the toolbar instead of looking like a load failure.
        StubLoadResult(TotalPages);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Build and register a planned `load` invocation whose result is a real instance
    // of PdfViewer's private LoadResult with the given page count.
    private void StubLoadResult(int totalPages)
    {
        var loadResultType = typeof(L.PdfViewer)
            .GetNestedType("LoadResult", BindingFlags.NonPublic)
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

        var handler = setupOpen.MakeGenericMethod(loadResultType)
            .Invoke(null, [_module, "load", matchAll])!;

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

    private IRenderedComponent<L.PdfViewer> RenderViewer(string src)
        => _ctx.Render<L.PdfViewer>(p =>
        {
            p.Add(c => c.Src, src);
            p.Add(c => c.ShowSearch, true);
        });

    private static IElement SearchInput(IRenderedComponent<L.PdfViewer> cut)
        => cut.Find("[aria-label='Search PDF']");

    [Fact]
    public async Task SrcChange_Clears_The_Search_Query_Box()
    {
        var cut = RenderViewer(SrcA);

        // User runs a search against document A — typing into the search box fires
        // @onchange="HandleSearchInput", which binds the text into _searchQuery
        // (rendered back as value="@_searchQuery").
        await cut.InvokeAsync(() => SearchInput(cut).Change("invoice"));
        Assert.Equal("invoice", SearchInput(cut).GetAttribute("value"));

        // The consumer swaps in a brand-new document. The Src-change reset must empty
        // the search box alongside its (already-cleared) results/highlights. Pre-fix
        // the old query stayed stranded in the input.
        cut.Render(p =>
        {
            p.Add(c => c.Src, SrcB);
            p.Add(c => c.ShowSearch, true);
        });

        Assert.Equal(string.Empty, SearchInput(cut).GetAttribute("value"));
    }

    [Fact]
    public async Task SameSrc_ReRender_Keeps_The_Search_Query()
    {
        // Guards against an over-broad reset: an unrelated re-render that does NOT
        // change Src must leave the in-progress query alone.
        var cut = RenderViewer(SrcA);

        await cut.InvokeAsync(() => SearchInput(cut).Change("invoice"));

        // Re-render with the SAME Src (e.g. a parent state change).
        cut.Render(p =>
        {
            p.Add(c => c.Src, SrcA);
            p.Add(c => c.ShowSearch, true);
        });

        Assert.Equal("invoice", SearchInput(cut).GetAttribute("value"));
    }
}
