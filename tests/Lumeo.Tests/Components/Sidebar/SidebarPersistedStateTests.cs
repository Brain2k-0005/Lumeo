using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// Battle-test #11 (high) — PersistCollapsed / uncontrolled collapse state must NOT
/// live in the [Parameter] IsCollapsed. Blazor re-applies [Parameter] values from the
/// parent on every render, so when the provider stored its self-managed collapse state
/// back into IsCollapsed, any unrelated parent re-render that re-pushed the original
/// literal value silently clobbered it — most damagingly wiping a value just restored
/// from localStorage on first render.
///
/// The fix mirrors Collapsible: a private <c>_isCollapsed</c> backing field that the
/// cascade renders from when uncontrolled (no <c>IsCollapsedChanged</c> delegate), seeded
/// from the parameter only on first set or a genuine parameter change. These tests
/// reproduce the exact state sequence (toggle / restore → same-literal parent re-render →
/// assert the state survived) and fail against the old param-stored implementation.
///
/// Observable: SidebarComponent renders the Push variant at <c>w-0</c> when collapsed and
/// <c>w-64</c> when expanded, driven by the cascaded SidebarState.IsCollapsed.
/// </summary>
public class SidebarPersistedStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly BunitJSModuleInterop _module;

    public SidebarPersistedStateTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        // SidebarProvider's PersistCollapsed restore calls loadFromLocalStorage on the
        // versioned components.js module. Set up the SAME versioned path the
        // ComponentInteropService imports so we can return a concrete stored value.
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

    // Renders SidebarProvider (the unit under test) as the typed root so we can re-render
    // it with cut.Render(p => p.Add(...)). A Push-variant SidebarComponent inside exposes
    // the collapsed state as the aside width class.
    private IRenderedComponent<L.SidebarProvider> RenderProvider(
        bool isCollapsed,
        bool persist = false,
        EventCallback<bool>? isCollapsedChanged = null)
    {
        return _ctx.Render<L.SidebarProvider>(p =>
        {
            p.Add(s => s.IsCollapsed, isCollapsed);
            p.Add(s => s.PersistCollapsed, persist);
            if (isCollapsedChanged is { } cb)
                p.Add(s => s.IsCollapsedChanged, cb);
            p.Add(s => s.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarTrigger>(0);
                b.CloseComponent();

                b.OpenComponent<L.SidebarComponent>(1);
                b.AddAttribute(2, "ChildContent",
                    (RenderFragment)(inner => inner.AddContent(0, "Nav")));
                b.CloseComponent();
            }));
        });
    }

    private static bool IsCollapsed(IRenderedComponent<L.SidebarProvider> cut)
    {
        var cls = cut.Find("aside").GetAttribute("class") ?? "";
        // Push variant: collapsed = w-0, expanded = w-64.
        return cls.Contains("w-0");
    }

    [Fact]
    public void Uncontrolled_Toggle_Survives_Same_Literal_Parent_Rerender()
    {
        // Uncontrolled (no IsCollapsedChanged): the provider self-manages.
        var cut = RenderProvider(isCollapsed: false);
        Assert.False(IsCollapsed(cut)); // starts expanded (w-64)

        // User toggles -> collapsed.
        cut.Find("button[aria-label='Toggle sidebar']").Click();
        Assert.True(IsCollapsed(cut)); // now w-0

        // A parent re-render re-pushes the SAME literal IsCollapsed=false (e.g. an
        // unrelated state change higher up). Pre-fix this clobbered the toggled state
        // back to expanded; the backing field must keep it collapsed.
        cut.Render(p => p.Add(s => s.IsCollapsed, false));

        Assert.True(IsCollapsed(cut));
    }

    [Fact]
    public void PersistCollapsed_Restored_State_Survives_Same_Literal_Parent_Rerender()
    {
        // localStorage holds "true" (collapsed) while the consumer passes the default
        // IsCollapsed=false. On first render the provider restores collapsed=true.
        _module.Setup<string>("loadFromLocalStorage", _ => true).SetResult("true");

        var cut = RenderProvider(isCollapsed: false, persist: true);

        // First-render restore flips the sidebar to collapsed.
        cut.WaitForAssertion(() => Assert.True(IsCollapsed(cut)));

        // The headline bug: a later parent re-render re-applies the literal
        // IsCollapsed=false and (pre-fix) wipes the restored localStorage state.
        cut.Render(p => p.Add(s => s.IsCollapsed, false));

        Assert.True(IsCollapsed(cut));
    }

    [Fact]
    public void Controlled_Mode_Still_Mirrors_Parent_Supplied_Value()
    {
        // Guard the controlled contract: when IsCollapsedChanged is bound the parent
        // owns the state, so the cascade must mirror the [Parameter] verbatim and react
        // to parent-driven changes (no backing-field divergence).
        var cb = EventCallback.Factory.Create<bool>(this, (bool _) => { });

        var cut = RenderProvider(isCollapsed: false, isCollapsedChanged: cb);
        Assert.False(IsCollapsed(cut));

        // Parent drives collapsed=true.
        cut.Render(p => p.Add(s => s.IsCollapsed, true));
        Assert.True(IsCollapsed(cut));

        // Parent drives it back to expanded.
        cut.Render(p => p.Add(s => s.IsCollapsed, false));
        Assert.False(IsCollapsed(cut));
    }

    [Fact]
    public void Uncontrolled_Initial_Collapsed_True_Is_Honoured()
    {
        // The backing field must seed from the initial parameter so IsCollapsed="true"
        // still renders collapsed on first paint (no regression from the controlled path).
        var cut = RenderProvider(isCollapsed: true);

        Assert.True(IsCollapsed(cut));
    }
}
