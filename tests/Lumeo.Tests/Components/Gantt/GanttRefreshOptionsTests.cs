using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Gantt;

/// <summary>
/// Battle-test wave 1 #2 (the part deferred from the earlier Gantt fix): the
/// init-only render options (Readonly / TodayHighlight / BarHeight / ColumnWidth)
/// were folded into the GanttInitAsync options bag ONCE, so flipping any of them
/// after the chart initialized was silently dropped. The component now fingerprints
/// those four and, on a change, pushes the new bag to the live instance via
/// gantt.refresh (which updates the option + re-renders).
///
/// Mirrors <see cref="GanttTasksHashTests"/>: the Gantt module is pre-registered in
/// Loose mode and gantt.init is stubbed to return a non-empty instance id so the
/// component reaches _initialized=true / a non-null _instanceId — required before
/// OnParametersSetAsync will push anything.
/// </summary>
public class GanttRefreshOptionsTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Gantt/js/gantt-v2.js";
    private const string InstanceId = "gantt-instance-1";

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        _module = _ctx.JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
        _module.Setup<string>("gantt.init", _ => true).SetResult(InstanceId);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static int RefreshCount(BunitJSModuleInterop module) =>
        module.Invocations.Count(i => i.Identifier == "gantt.refresh");

    private static L.GanttTask Task1 =>
        new("t1", "Design", new DateTime(2026, 1, 1), new DateTime(2026, 1, 5), 20);

    [Fact]
    public void Toggling_Readonly_After_Init_Refreshes_The_Live_Instance()
    {
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.Readonly, false));

        cut.Render(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.Readonly, true));

        Assert.True(
            RefreshCount(_module) > 0,
            "Flipping Readonly after init must push the new options to the live instance via gantt.refresh.");
    }

    [Fact]
    public void Toggling_TodayHighlight_After_Init_Refreshes_The_Live_Instance()
    {
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.TodayHighlight, true));

        cut.Render(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.TodayHighlight, false));

        Assert.True(
            RefreshCount(_module) > 0,
            "Toggling TodayHighlight after init must refresh the live instance via gantt.refresh.");
    }

    [Fact]
    public void Unchanged_Options_Do_Not_Refresh()
    {
        // A re-render that does NOT touch any of the four init-only options must not
        // issue a spurious gantt.refresh (the fingerprint is unchanged).
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.Readonly, false));

        cut.Render(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.Readonly, false)
            .Add(c => c.Class, "extra-class"));

        Assert.Equal(0, RefreshCount(_module));
    }
}
