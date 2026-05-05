using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace Lumeo.Tests.Contract;

/// <summary>
/// Universal contract tests that every public Lumeo component must satisfy.
/// Runs once per registry entry — closes the "I forgot to splat AdditionalAttributes"
/// class of regression for the entire library at once.
///
/// What's tested per component:
/// - Renders without throwing with default parameters
/// - <c>AdditionalAttributes</c> splat lands on root markup (data-* attribute)
///
/// Some components require child content / context / specific parameters to render
/// — those are in the exclusion lists below with one-line reasons.
///
/// Components whose registry name doesn't match their C# class name (e.g. "Resizable"
/// → ResizablePanelGroup, "Overlay" → OverlayProvider) return null from ResolveType
/// and are silently skipped — they appear in <see cref="_classNameMismatches"/> for
/// documentation purposes.
/// </summary>
public class ComponentContractTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ComponentContractTests()
    {
        _ctx.AddLumeoServices();
        // ConsentService is registered by AddLumeo() but the test helper hasn't been updated.
        // Register it here so ConsentBanner and similar components don't throw on inject.
        _ctx.Services.AddScoped<ConsentService>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // -------------------------------------------------------------------------
    // Exclusion lists — components that can't or shouldn't be tested with defaults
    // -------------------------------------------------------------------------

    /// <summary>
    /// Components excluded from ALL theories. Every entry has a one-line reason.
    /// </summary>
    private static readonly HashSet<string> _cannotRenderWithDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── generic components (typeparam) ───────────────────────────────────
        // Open generic types can't be resolved via Type alone in the registry loop.
        "Form",         // @typeparam TModel — generic; requires concrete TModel to render
        "PickList",     // @typeparam TItem — generic
        "Sortable",     // SortableList<TItem> — generic
        "TreeView",     // @typeparam T — generic
        "DataGrid",     // @typeparam TItem — generic (Lumeo.DataGrid package)
        "DataTable",    // @typeparam TItem — generic (Lumeo.DataGrid package)
    };

    /// <summary>
    /// Components whose registry "name" field doesn't match a <c>Lumeo.{name}</c> class.
    /// <see cref="ResolveType"/> returns null for these and the theories skip them silently.
    /// Listed here purely for documentation so a developer knows why they're absent from results.
    /// </summary>
    private static readonly Dictionary<string, string> _classNameMismatches = new()
    {
        ["Resizable"] = "ResizablePanelGroup — no standalone Resizable class",
        ["Overlay"] = "OverlayProvider — no standalone Overlay class",
        ["Sidebar"] = "SidebarProvider — no standalone Sidebar class",
        ["Filter"] = "FilterBar — in Lumeo.DataGrid; class name differs from registry name",
        ["Progress"] = "Progress exists; but first file is CircularProgress — test runs fine",
    };

    /// <summary>
    /// Components that render correctly but cannot be asserted for AdditionalAttributes forwarding
    /// with default parameters because either:
    ///   (a) their root is a non-DOM wrapper (CascadingValue) so no DOM attribute can land there, or
    ///   (b) they render empty markup by default (conditional visibility) so no DOM is emitted.
    /// These are NOT contract failures — the AdditionalAttributes declaration is correct in all cases.
    /// </summary>
    private static readonly HashSet<string> _noRootSplat = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── (a) CascadingValue-only root — no DOM element exists to carry attributes ──
        // Renders only <CascadingValue> — there is no DOM root; no splat is correct by design.
        "Dialog",
        "Drawer",
        "Sheet",
        "AlertDialog",
        // Accordion renders <CascadingValue> wrapping ChildContent; splat goes on children, not outer wrapper.
        "Accordion",

        // ── (b) Conditionally visible — render empty markup until triggered ──
        // @if (_visible && !Disabled) — invisible by default; _visible starts false (set by scroll JS event)
        "BackToTop",
        // @if (Open && _currentStepConfig is not null) — invisible by default; Open defaults to false
        "Tour",
    };

    // -------------------------------------------------------------------------
    // Theory data — one object[] per testable registry entry
    // -------------------------------------------------------------------------

    public static IEnumerable<object[]> AllComponents()
    {
        var registryJson = File.ReadAllText(GetRegistryPath());
        var registry = System.Text.Json.JsonSerializer.Deserialize<Registry>(
            registryJson,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("registry.json failed to parse");

        foreach (var (_, comp) in registry.Components)
        {
            if (_cannotRenderWithDefaults.Contains(comp.Name))
                continue;
            yield return new object[] { comp.Name };
        }
    }

    // -------------------------------------------------------------------------
    // Theory 1 — every component renders without throwing with default parameters
    // -------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllComponents))]
    public void Component_renders_with_defaults(string componentName)
    {
        var type = ResolveType(componentName);
        if (type is null)
        {
            // Registry name doesn't map to a Lumeo.{name} IComponent — skip silently.
            // See _classNameMismatches for documentation on why.
            return;
        }

        RenderFragment fragment = builder =>
        {
            builder.OpenComponent(0, type);
            builder.CloseComponent();
        };

        // Throws = test fails, surfacing the real cause rather than hiding it.
        var cut = _ctx.Render(fragment);
        Assert.NotNull(cut.Markup);
    }

    // -------------------------------------------------------------------------
    // Theory 2 — every component declares and forwards AdditionalAttributes
    // -------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllComponents))]
    public void Component_declares_and_forwards_additional_attributes(string componentName)
    {
        // Components that intentionally don't splat to their DOM root — skip assertion only.
        if (_noRootSplat.Contains(componentName)) return;

        var type = ResolveType(componentName);
        if (type is null) return;

        // --- Step 1: the parameter must be declared ---
        var prop = type.GetProperty(
            "AdditionalAttributes",
            BindingFlags.Public | BindingFlags.Instance);

        var hasCapture = prop is not null
            && prop.GetCustomAttributes(typeof(ParameterAttribute), true)
                   .Cast<ParameterAttribute>()
                   .Any(a => a.CaptureUnmatchedValues);

        if (!hasCapture)
        {
            Assert.Fail(
                $"{componentName} does not declare " +
                $"[Parameter(CaptureUnmatchedValues = true)] " +
                $"public Dictionary<string, object>? AdditionalAttributes. " +
                $"Every public Lumeo component must implement this contract.");
            return; // unreachable, but keeps the compiler happy
        }

        // --- Step 2: the attribute must land in the rendered markup ---
        IRenderedComponent<IComponent> cut;
        try
        {
            var attrs = new Dictionary<string, object> { ["data-testid"] = "contract-test" };
            RenderFragment fragment = builder =>
            {
                builder.OpenComponent(0, type);
                builder.AddAttribute(1, "AdditionalAttributes", attrs);
                builder.CloseComponent();
            };
            cut = _ctx.Render(fragment);
        }
        catch
        {
            // Render failure is a separate concern — Theory 1 will surface it.
            // Don't cascade noise here.
            return;
        }

        Assert.Contains(
            "data-testid=\"contract-test\"",
            cut.Markup,
            StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Type? ResolveType(string componentName)
    {
        // Walk all loaded Lumeo.* assemblies for Lumeo.{componentName} : IComponent.
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.StartsWith("Lumeo") == true);

        foreach (var asm in assemblies)
        {
            var t = asm.GetType($"Lumeo.{componentName}");
            if (t is not null
                && typeof(IComponent).IsAssignableFrom(t)
                && !t.IsAbstract
                && !t.IsGenericTypeDefinition)
            {
                return t;
            }
        }

        return null;
    }

    private static string GetRegistryPath()
    {
        // Tests execute from: tests/Lumeo.Tests/bin/{Config}/net10.0/
        // Walk five levels up to the repo root.
        var here = AppContext.BaseDirectory;
        var repo = Path.GetFullPath(Path.Combine(here, "..", "..", "..", "..", ".."));
        return Path.Combine(repo, "src", "Lumeo", "registry", "registry.json");
    }

    // -------------------------------------------------------------------------
    // Registry deserialization models
    // -------------------------------------------------------------------------

    private sealed class Registry
    {
        public Dictionary<string, RegistryComponent> Components { get; set; } = new();
    }

    private sealed class RegistryComponent
    {
        public string Name { get; set; } = "";
    }
}
