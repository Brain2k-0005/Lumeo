using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.PdfViewer;

/// <summary>
/// battle-wave2 #139 (medium, edge-data) — "Page-number input strands an
/// out-of-range / unchanged typed value (no revert on rejected change)."
///
/// Mechanism: the page-number &lt;input&gt; committed via @onchange="HandlePageInput"
/// → SetPageAsync. SetPageAsync clamps the typed value to [1, _totalPages] and then
/// early-returns on <c>if (clamped == Page) return;</c> (and HandlePageInput
/// returned Task.CompletedTask on a TryParse failure) WITHOUT re-rendering. The
/// input was bound <c>value="@Page"</c>, and Blazor never patches an input whose
/// model value is unchanged across renders, so a rejected entry — an out-of-range
/// page number that clamps back to the current page, or a non-numeric string —
/// stayed stranded in the box, disagreeing with the page actually shown.
///
/// The fix binds the input to a dedicated <c>_pageInput</c> text field that every
/// commit path resets to the real <c>Page</c> (SyncPageInput), so the box snaps
/// back to the actual number whenever an entry is rejected.
///
/// bUnit can't run the real PDF.js canvas, so — mirroring PdfViewerBehaviorTests /
/// PdfViewerSearchResetTests — the fixture stubs the isolated, version-stamped
/// module's <c>load</c> export (a non-zero page count arms the toolbar) and drives
/// the C# ⇄ JS contract. The C#-observable symptom is the page input's bound value:
/// after a rejected entry it must equal the current page, not the rejected text.
/// This FAILS against the pre-fix component (rejected value stranded) and PASSES
/// with the revert.
/// </summary>
public class PdfViewerPageInputRevertTests : IAsyncLifetime
{
    private const string Src = "https://example.test/doc.pdf";
    private const int TotalPages = 5;

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
        // page count arms the toolbar / nav clamps instead of looking like a load
        // failure.
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

    private IRenderedComponent<L.PdfViewer> RenderViewer()
        => _ctx.Render<L.PdfViewer>(p => p.Add(c => c.Src, Src));

    private static IElement PageInput(IRenderedComponent<L.PdfViewer> cut)
        => cut.Find("[aria-label='Page number']");

    [Fact]
    public async Task OutOfRange_Below_Minimum_Reverts_The_Box_To_The_Current_Page()
    {
        var cut = RenderViewer();

        // Default page is 1. The user types 0 (below the minimum), which clamps back
        // to page 1 — the page we're already on. Pre-fix SetPageAsync early-returned
        // on `clamped == Page` with no render, stranding "0" in the box.
        await cut.InvokeAsync(() => PageInput(cut).Change("0"));

        // The box must snap back to the real page number (1), not show the rejected 0.
        Assert.Equal("1", PageInput(cut).GetAttribute("value"));
    }

    [Fact]
    public async Task NonNumeric_Entry_Reverts_The_Box_To_The_Current_Page()
    {
        var cut = RenderViewer();

        // A non-numeric entry fails TryParse. Pre-fix HandlePageInput returned without
        // re-rendering, leaving the garbage text in the input.
        await cut.InvokeAsync(() => PageInput(cut).Change("abc"));

        Assert.Equal("1", PageInput(cut).GetAttribute("value"));
    }

    [Fact]
    public async Task ValidEntry_Still_Commits_The_New_Page()
    {
        // Guards the normal path: a valid in-range page must still be accepted and
        // shown in the box (the revert must not clobber a real change).
        var cut = RenderViewer();

        await cut.InvokeAsync(() => PageInput(cut).Change("3"));

        Assert.Equal("3", PageInput(cut).GetAttribute("value"));
    }
}
