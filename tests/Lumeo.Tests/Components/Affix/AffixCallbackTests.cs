using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Affix;

/// <summary>
/// Affix pins its child on scroll via an IntersectionObserver/scroll watcher that lives
/// in components.js and never runs in bUnit's headless DOM — so we cannot actually scroll
/// to make it stick. The testable seam is the .NET&lt;-&gt;JS callback contract: on first
/// render the component calls <c>Interop.RegisterAffix(elementId, …, handler)</c>, which
/// stashes <paramref name="handler"/> keyed by the element id. The browser later reports a
/// state change by invoking the <c>[JSInvokable] ComponentInteropService.OnAffixChanged</c>,
/// which dispatches back to that handler. We simulate "became affixed" by resolving the
/// interop service from DI and invoking <c>OnAffixChanged(elementId, true/false)</c>
/// directly, then assert the component's <c>OnChange</c> EventCallback fires with the new
/// fixed state. The component does NOT toggle a CSS class for the affixed state (its only
/// rendered class is the user-supplied <c>Class</c>), so the callback is the observable.
/// </summary>
public class AffixCallbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public AffixCallbackTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        _module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        _module.Mode = JSRuntimeMode.Loose;
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // OnAffixChanged is declared on the concrete ComponentInteropService (the [JSInvokable]
    // the browser calls), not the IComponentInteropService interface. AddLumeoServices
    // registers the concrete and maps the interface to the SAME scoped instance, so the
    // handler the Affix registers via the interface is dispatched here.
    private ComponentInteropService Interop => _ctx.Services.GetRequiredService<ComponentInteropService>();

    [Fact]
    public void Mounting_registers_the_affix_with_its_element_id()
    {
        var cut = _ctx.Render<L.Affix>(p => p
            .Add(a => a.OffsetTop, 24)
            .AddChildContent("<span>pinned</span>"));

        var elementId = cut.Find("div").GetAttribute("id")!;
        Assert.False(string.IsNullOrEmpty(elementId));

        var invocation = _module.VerifyInvoke("registerAffix");
        Assert.Equal(elementId, invocation.Arguments[0]);
        Assert.Equal(24, invocation.Arguments[1]); // offsetTop is pushed through
    }

    [Fact]
    public async Task Becoming_affixed_fires_OnChange_with_true()
    {
        bool? changed = null;
        var cut = _ctx.Render<L.Affix>(p => p
            .Add(a => a.OnChange, isFixed => changed = isFixed)
            .AddChildContent("<span>pinned</span>"));

        var elementId = cut.Find("div").GetAttribute("id")!;

        // Simulate the browser reporting the element became sticky.
        await cut.InvokeAsync(() => Interop.OnAffixChanged(elementId, true));

        Assert.True(changed);
    }

    [Fact]
    public async Task Releasing_affix_fires_OnChange_with_false()
    {
        var states = new List<bool>();
        var cut = _ctx.Render<L.Affix>(p => p
            .Add(a => a.OnChange, isFixed => states.Add(isFixed))
            .AddChildContent("<span>pinned</span>"));

        var elementId = cut.Find("div").GetAttribute("id")!;

        await cut.InvokeAsync(() => Interop.OnAffixChanged(elementId, true));
        await cut.InvokeAsync(() => Interop.OnAffixChanged(elementId, false));

        Assert.Equal(new[] { true, false }, states);
    }

    [Fact]
    public async Task OnChange_is_edge_triggered_repeated_same_state_fires_once()
    {
        var fireCount = 0;
        var cut = _ctx.Render<L.Affix>(p => p
            .Add(a => a.OnChange, _ => fireCount++)
            .AddChildContent("<span>pinned</span>"));

        var elementId = cut.Find("div").GetAttribute("id")!;

        // The component guards on `_isFixed != isFixed`, so two identical reports
        // must only surface a single OnChange.
        await cut.InvokeAsync(() => Interop.OnAffixChanged(elementId, true));
        await cut.InvokeAsync(() => Interop.OnAffixChanged(elementId, true));

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public async Task Unknown_element_id_does_not_fire_OnChange()
    {
        var fired = false;
        _ctx.Render<L.Affix>(p => p
            .Add(a => a.OnChange, _ => fired = true)
            .AddChildContent("<span>pinned</span>"));

        // A callback for an id we never registered must be a no-op.
        await Interop.OnAffixChanged("affix-does-not-exist", true);

        Assert.False(fired);
    }
}
