using Bunit;
using Lumeo;
using Lumeo.Docs.Pages.Demos;
using Lumeo.Docs.Tests.Helpers;
using Xunit;

namespace Lumeo.Docs.Tests.Demos;

// Meridian Ops "Terminal schedule" panel — migrated from the legacy v2 <Gantt>
// (Frappe-derived SVG engine) to the promoted v3 <GanttChart>. Regression guard:
// the panel must render GanttChart, never the legacy Gantt component, and the
// per-lane grouping + milestone data mapped from MeridianData.Schedule must
// actually reach the v3 render tree (not just compile).
public class EnterpriseGanttPanelTests
{
    private static BunitContext NewContext()
    {
        var ctx = new BunitContext();

        // Loose mode: ComponentInteropService/ThemeService make real
        // _content/Lumeo/js/components.js interop calls (GanttChart's drag/splitter
        // registration among them) that bUnit has no browser to satisfy — same
        // pattern EnterpriseDemoTests/SaasDemoTests use for the other demo panels.
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = ctx.JSInterop.SetupModule("./_content/Lumeo/js/components.js");
        module.Mode = JSRuntimeMode.Loose;

        ctx.AddDocsServices();

        return ctx;
    }

    [Fact]
    public async Task Renders_GanttChart_v3_not_the_legacy_Gantt()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<EnterpriseGanttPanel>();

        Assert.Single(cut.FindComponents<GanttChart>());
        Assert.Empty(cut.FindComponents<Lumeo.Gantt>());
    }

    [Fact]
    public async Task Groups_bars_by_lane_and_renders_the_milestone_as_a_v3_diamond()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<EnterpriseGanttPanel>();

        // GroupBy(t => t.GroupLabel ?? "") sorts tasks into MeridianData.Schedule's
        // Lane swim-lanes, and GanttRowModel.DefaultShowTreePane turns the tree pane
        // (which renders the group header text) on automatically once GroupBy is set.
        Assert.Contains("Rail Dispatch", cut.Markup);
        // "&" is HTML-entity-encoded in raw markup.
        Assert.Contains("Customs &amp; Docs", cut.Markup);

        // MeridianData's "ms-launch" bar (IsMilestone: true) must render through
        // GanttBar's milestone branch (a diamond), not a regular duration bar.
        Assert.Contains("lumeo-gantt-v3-milestone", cut.Markup);
    }
}
