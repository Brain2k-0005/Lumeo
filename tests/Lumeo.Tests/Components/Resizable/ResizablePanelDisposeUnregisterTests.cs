using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using L = Lumeo;

namespace Lumeo.Tests.Components.Resizable;

/// <summary>
/// Battle-test #109 (lifecycle): a <see cref="L.ResizablePanel"/> removed at
/// runtime must UNREGISTER from its group on dispose. Previously ResizablePanel
/// only had OnInitialized -> RegisterPanel and no IDisposable, so the group's
/// per-Order maps (_panelDefaultSizes / _panelConstraints / _collapseConfig /
/// _collapsed / _preCollapseSize) kept stale entries for an index no live panel
/// occupied: a collapsed flag survived removal and the group's shrink-reconcile
/// (which keys off _panelDefaultSizes.Count) never fired, so the survivors were
/// never renormalized. The fix adds @implements IDisposable to ResizablePanel +
/// an UnregisterPanel(Order) action on the cascading context.
/// </summary>
public class ResizablePanelDisposeUnregisterTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ResizablePanelDisposeUnregisterTests()
    {
        _ctx.AddLumeoServices();
        // HandleDrag converts pixel deltas to % via getElementDimension — pin it
        // to 1000px so 10px == 1%. Mirror the module identifier the interop
        // service computes (assembly version cache-buster).
        var version = typeof(Lumeo.Services.ComponentInteropService).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? typeof(Lumeo.Services.ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        var module = _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={version}");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<double>("getElementDimension", _ => true).SetResult(1000.0);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Builds the group's ChildContent (panels + interleaved handles) from a spec
    // so the same group can be re-rendered with a different panel count.
    private static RenderFragment BuildPanels((double Default, double Min, double Max, bool Collapsible)[] panels) => b =>
    {
        var seq = 0;
        for (var i = 0; i < panels.Length; i++)
        {
            var idx = i;
            b.OpenComponent<L.ResizablePanel>(seq++);
            b.AddAttribute(seq++, "Order", idx);
            b.AddAttribute(seq++, "DefaultSize", panels[idx].Default);
            b.AddAttribute(seq++, "MinSize", panels[idx].Min);
            b.AddAttribute(seq++, "MaxSize", panels[idx].Max);
            b.AddAttribute(seq++, "Collapsible", panels[idx].Collapsible);
            b.AddAttribute(seq++, "ChildContent", (RenderFragment)(inner =>
                inner.AddMarkupContent(0, $"<span data-testid='panel-{idx}'>panel {idx}</span>")));
            b.CloseComponent();
            if (idx < panels.Length - 1)
            {
                b.OpenComponent<L.ResizableHandle>(seq++);
                b.AddAttribute(seq++, "PanelIndex", idx);
                b.CloseComponent();
            }
        }
    };

    private IRenderedComponent<L.ResizablePanelGroup> RenderGroup(
        (double Default, double Min, double Max, bool Collapsible)[] panels) =>
        _ctx.Render<L.ResizablePanelGroup>(p => p
            .Add(g => g.Direction, L.Orientation.Horizontal)
            .AddChildContent(BuildPanels(panels)));

    private static double PanelFlexBasis(IRenderedComponent<L.ResizablePanelGroup> cut, int idx)
    {
        var style = cut.Find($"[data-testid='panel-{idx}']").ParentElement!.GetAttribute("style") ?? "";
        var marker = "flex: ";
        var start = style.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = style.IndexOf(' ', start);
        return double.Parse(style.Substring(start, end - start),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static readonly (double Default, double Min, double Max, bool Collapsible) Plain = (50.0, 10.0, 90.0, false);
    private static readonly (double Default, double Min, double Max, bool Collapsible) Collapser = (50.0, 10.0, 90.0, true);

    // A panel collapsed then removed must not leave its collapsed flag behind in
    // the group. WITHOUT the fix Dispose never runs, so _collapsed keeps the dead
    // index and IsPanelCollapsed(2) stays true after the panel is gone.
    [Fact]
    public async Task Removing_A_Collapsed_Panel_Clears_Its_Stale_Collapsed_Flag()
    {
        // Three panels; the trailing one is collapsible.
        var cut = RenderGroup(new[] { Plain, Plain, Collapser });
        var ctx = cut.FindComponent<L.ResizablePanel>().Instance.Context;

        // Collapse the trailing panel (Order 2) via the context toggle.
        await cut.InvokeAsync(() => ctx.ToggleCollapse.InvokeAsync(2));
        Assert.True(ctx.IsPanelCollapsed(2));

        // Consumer removes the trailing panel at runtime -> the ResizablePanel
        // with Order 2 is disposed by the renderer.
        cut.Render(p => p.AddChildContent(BuildPanels(new[] { Plain, Plain })));

        // Re-read the (rebuilt, IsFixed=false) cascading context from a surviving
        // panel and assert the dead index's collapsed flag is gone.
        var ctxAfter = cut.FindComponent<L.ResizablePanel>().Instance.Context;
        Assert.False(ctxAfter.IsPanelCollapsed(2));
    }

    // Removing a panel must let the group's shrink-reconcile fire: that only
    // happens when _panelDefaultSizes shrinks, which only happens if the removed
    // panel unregistered on dispose. The two survivors must then renormalize to
    // sum 100 (no ghost third share). WITHOUT the fix _panelDefaultSizes.Count
    // stays 3 != _sizes.Count(==2 after reconcile would never trigger), so the
    // reconcile path is never reached and the layout is left inconsistent.
    [Fact]
    public void Removing_A_Panel_Renormalizes_Survivors_To_100()
    {
        var cut = RenderGroup(new[] { Plain, Plain, Plain });

        // 3 panels seed to ~33.33 each.
        Assert.Equal(100.0,
            PanelFlexBasis(cut, 0) + PanelFlexBasis(cut, 1) + PanelFlexBasis(cut, 2),
            precision: 1);

        // Drop the trailing panel.
        cut.Render(p => p.AddChildContent(BuildPanels(new[] { Plain, Plain })));

        var s0 = PanelFlexBasis(cut, 0);
        var s1 = PanelFlexBasis(cut, 1);
        // Survivors renormalize to sum 100 (50/50 after dropping one of three
        // equal shares). WITHOUT unregister the shrink-reconcile never fires.
        Assert.Equal(100.0, s0 + s1, precision: 1);
        Assert.Equal(s0, s1, precision: 1);
    }

    // Disposing the whole group (all panels) must not throw — the unregister path
    // is null-safe when the cascading context is already being torn down.
    [Fact]
    public async Task Disposing_The_Group_Does_Not_Throw()
    {
        var cut = RenderGroup(new[] { Plain, Plain, Collapser });
        var ctx = cut.FindComponent<L.ResizablePanel>().Instance.Context;
        await cut.InvokeAsync(() => ctx.ToggleCollapse.InvokeAsync(2));

        var ex = await Record.ExceptionAsync(async () => await _ctx.DisposeAsync());
        Assert.Null(ex);
    }
}
