using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Overlay;

/// <summary>
/// Permanent regression pin for the drawer-over-drawer stacking class (consumer report
/// B4, June 2026 against 3.19.0: "opening a second ShowDrawerAsync from an open drawer
/// breaks — the second doesn't open cleanly and/or the first can no longer be closed").
/// A maintainer investigation could NOT reproduce it on 3.19.0 or 4.0.4 (bUnit + real
/// Chromium, desktop + touch emulation) — the per-id architecture (GUID overlay ids,
/// per-element drawer/swipe/focus-trap registries, refcounted scroll lock, tiered
/// z-indexes) already stacked correctly, and the original failing call site was replaced
/// by a workaround the same day it was reported. These tests pin that correctness
/// permanently so the class can never regress silently; 4.1.0-preview.1 additionally
/// hardened the shells (pointer-events-none full-viewport wrappers, panel-first focus)
/// against the suspected real-device wedge mechanism.
/// </summary>
public class StackedServiceDrawerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StackedServiceDrawerTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>Overlay body that records the overlay id it was mounted under (via the
    /// cascaded <see cref="OverlayShellMarker"/>), so the test can close specific
    /// overlays through the service API exactly like consumer code does.</summary>
    private sealed class RecordingBody : ComponentBase
    {
        public static readonly List<string> MountedIds = new();
        [CascadingParameter] public OverlayShellMarker? Shell { get; set; }

        protected override void OnInitialized()
        {
            if (Shell is not null && !MountedIds.Contains(Shell.OverlayId))
                MountedIds.Add(Shell.OverlayId);
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(0, $"BODY:{Shell?.OverlayId}");
    }

    [Fact]
    public void Second_Service_Drawer_Opens_On_Top_Of_The_First()
    {
        RecordingBody.MountedIds.Clear();
        var service = _ctx.Services.GetRequiredService<OverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        _ = service.ShowDrawerAsync<RecordingBody>(title: "First");
        cut.WaitForState(() => RecordingBody.MountedIds.Count == 1);

        // Open the second drawer "from inside" the first (same service call a button
        // inside drawer 1 would make).
        _ = service.ShowDrawerAsync<RecordingBody>(title: "Second");
        cut.WaitForState(() => RecordingBody.MountedIds.Count == 2);

        // Both drawer panels are mounted simultaneously, with ascending z-tiers.
        var panels = cut.FindAll("[role='dialog']");
        Assert.Equal(2, panels.Count);
        Assert.Contains("First", cut.Markup);
        Assert.Contains("Second", cut.Markup);

        var z1 = int.Parse(System.Text.RegularExpressions.Regex.Match(panels[0].GetAttribute("style") ?? "", @"z-index:(\d+)").Groups[1].Value);
        var z2 = int.Parse(System.Text.RegularExpressions.Regex.Match(panels[1].GetAttribute("style") ?? "", @"z-index:(\d+)").Groups[1].Value);
        Assert.True(z2 > z1, $"second drawer must stack above the first (z {z2} > {z1})");
    }

    [Fact]
    public void Closing_The_Top_Drawer_Leaves_The_First_Open_And_Closable()
    {
        RecordingBody.MountedIds.Clear();
        var service = _ctx.Services.GetRequiredService<OverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        _ = service.ShowDrawerAsync<RecordingBody>(title: "First");
        cut.WaitForState(() => RecordingBody.MountedIds.Count == 1);
        _ = service.ShowDrawerAsync<RecordingBody>(title: "Second");
        cut.WaitForState(() => RecordingBody.MountedIds.Count == 2);

        // Close the TOP drawer via the service (what its own close button would do).
        service.Close(RecordingBody.MountedIds[1]);
        cut.WaitForState(() => cut.FindAll("[role='dialog']").Count == 1);
        Assert.Contains("First", cut.Markup);
        Assert.DoesNotContain("Second", cut.Markup);

        // The exact reported symptom: "the first can no longer be closed" — it can.
        service.Close(RecordingBody.MountedIds[0]);
        cut.WaitForState(() => cut.FindAll("[role='dialog']").Count == 0);
    }

    [Fact]
    public void Closing_Out_Of_Order_Bottom_First_Also_Works()
    {
        RecordingBody.MountedIds.Clear();
        var service = _ctx.Services.GetRequiredService<OverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        _ = service.ShowDrawerAsync<RecordingBody>(title: "First");
        cut.WaitForState(() => RecordingBody.MountedIds.Count == 1);
        _ = service.ShowDrawerAsync<RecordingBody>(title: "Second");
        cut.WaitForState(() => RecordingBody.MountedIds.Count == 2);

        // Close the BOTTOM drawer while the top one stays open.
        service.Close(RecordingBody.MountedIds[0]);
        cut.WaitForState(() => cut.FindAll("[role='dialog']").Count == 1);
        Assert.Contains("Second", cut.Markup);
        Assert.DoesNotContain("First", cut.Markup);

        service.Close(RecordingBody.MountedIds[1]);
        cut.WaitForState(() => cut.FindAll("[role='dialog']").Count == 0);
    }

    [Fact]
    public void Stacked_Drawer_Wrappers_Cannot_Eat_Input_When_A_Panel_Wedges()
    {
        RecordingBody.MountedIds.Clear();
        var service = _ctx.Services.GetRequiredService<OverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        _ = service.ShowDrawerAsync<RecordingBody>(title: "First");
        _ = service.ShowDrawerAsync<RecordingBody>(title: "Second");
        cut.WaitForState(() => RecordingBody.MountedIds.Count == 2);

        // The 4.1.0 hardening: every full-viewport wrapper is pointer-events-none, so
        // even a panel wedged off-screen leaves no invisible input-eating layer — the
        // suspected mechanism behind the original real-device report.
        var wrappers = cut.FindAll("div").Where(d =>
            d.ClassList.Contains("inset-0") && !d.ClassList.Contains("animate-fade-in")).ToList();
        Assert.Equal(2, wrappers.Count);
        Assert.All(wrappers, w => Assert.Contains("pointer-events-none", w.ClassList));
    }
}
