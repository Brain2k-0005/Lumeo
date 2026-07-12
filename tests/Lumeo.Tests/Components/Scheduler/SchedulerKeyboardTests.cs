using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Wave 4 composition audit — Scheduler's own DOM is a toolbar of native Lumeo
/// &lt;Button&gt;s (Prev/Today/Next) plus an already-tested &lt;ToggleGroup&gt;
/// for Month/Week/Day/List; the calendar surface is opaque JS (FullCalendar),
/// out of bUnit's reach. SchedulerBehaviorTests already covers the view-switch
/// ToggleGroup dispatching scheduler.changeView. This file fills the remaining
/// neededTests gap: activating the Prev/Today/Next buttons calls
/// scheduler.prev/today/next on the live instance. Enter/Space activation of a
/// native &lt;button&gt; is free via the browser's default semantics, so
/// .Click() exercises the exact handler a keydown would run.
/// </summary>
public class SchedulerKeyboardTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Scheduler/js/scheduler.js";
    private const string InstanceId = "sched-instance-1";

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        _module = _ctx.JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
        _module.Setup<string>("scheduler.init", _ => true).SetResult(InstanceId);
        _module.Setup<string>("scheduler.getTitle", _ => true).SetResult("June 2026");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static AngleSharp.Dom.IElement NavButton(IRenderedComponent<L.Scheduler> cut, string label) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == label || b.GetAttribute("aria-label") == label);

    [Fact]
    public void Activating_Previous_Calls_SchedulerPrev()
    {
        var cut = _ctx.Render<L.Scheduler>();
        cut.WaitForAssertion(() => Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "scheduler.init"));

        NavButton(cut, "Previous").Click();

        var call = Assert.Single(_ctx.JSInterop.Invocations, i => i.Identifier == "scheduler.prev");
        Assert.Equal(InstanceId, call.Arguments[0]);
    }

    [Fact]
    public void Activating_Today_Calls_SchedulerToday()
    {
        var cut = _ctx.Render<L.Scheduler>();
        cut.WaitForAssertion(() => Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "scheduler.init"));

        NavButton(cut, "Today").Click();

        var call = Assert.Single(_ctx.JSInterop.Invocations, i => i.Identifier == "scheduler.today");
        Assert.Equal(InstanceId, call.Arguments[0]);
    }

    [Fact]
    public void Activating_Next_Calls_SchedulerNext()
    {
        var cut = _ctx.Render<L.Scheduler>();
        cut.WaitForAssertion(() => Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "scheduler.init"));

        NavButton(cut, "Next").Click();

        var call = Assert.Single(_ctx.JSInterop.Invocations, i => i.Identifier == "scheduler.next");
        Assert.Equal(InstanceId, call.Arguments[0]);
    }
}
