using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Behavior/interop tests for the FullCalendar-backed <see cref="L.Scheduler"/>.
/// The calendar surface lives in the satellite module scheduler.js, so — like the
/// RichTextEditor and CodeEditor behavior suites — these assert the C# ⇄ JS contract
/// rather than the rendered calendar grid (which needs a real browser):
///   - mounting lazy-imports scheduler.js by its exact path and runs scheduler.init,
///   - the Events param is serialized into the init options the module receives,
///   - the view-switch toolbar (Month/Week/Day/List) dispatches scheduler.changeView
///     with the picked view and flips the active toggle button's aria-pressed state.
///
/// The fixture's JSInterop runs in Loose mode (calls are recorded, un-setup calls
/// return defaults). We additionally stub <c>scheduler.init</c> to return a non-empty
/// instance id so the component captures an _instanceId — otherwise loose-mode returns
/// a null id and every later toolbar interaction is swallowed by the
/// <c>if (_instanceId is null) return;</c> guard, so changeView would never fire.
/// </summary>
public class SchedulerBehaviorTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Scheduler/js/scheduler.js";
    private const string InstanceId = "sched-instance-1";

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();

        // Pre-register the Scheduler's own isolated module so we can drive the init
        // handshake and inspect changeView/getTitle invocations against it.
        _module = _ctx.JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;

        // scheduler.init returns the instance id used by every later call — a
        // non-empty string is what gives the component a non-null _instanceId so the
        // navigation/view-switch guards pass.
        _module.Setup<string>("scheduler.init", _ => true).SetResult(InstanceId);
        // getTitle is read right after init and after each view switch; give it a
        // stable value so RefreshTitleAsync doesn't depend on loose-mode defaults.
        _module.Setup<string>("scheduler.getTitle", _ => true).SetResult("June 2026");

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // The view toggle renders one <button> per ToggleGroupItem (Month/Week/Day/List)
    // plus the Prev/Today/Next Buttons. Select a view button by its visible label.
    private static AngleSharp.Dom.IElement ViewButton(IRenderedComponent<L.Scheduler> cut, string label) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == label);

    [Fact]
    public void Mounting_lazy_imports_scheduler_module_by_exact_path()
    {
        _ctx.Render<L.Scheduler>();

        // The dynamic import("./_content/Lumeo.Scheduler/js/scheduler.js") is the
        // load-bearing contract: it lazy-loads the >200kB FullCalendar bundle only for
        // apps that actually mount a scheduler. Assert it happened with the exact path.
        var import = Assert.Single(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "import" && i.Arguments.Contains(ModulePath));
        Assert.Contains(ModulePath, import.Arguments);
    }

    [Fact]
    public void Mounting_runs_scheduler_init_against_the_module()
    {
        _ctx.Render<L.Scheduler>();

        // After importing the module, the component hands its host element + options to
        // scheduler.init — the single entry point that boots the FullCalendar instance.
        Assert.Contains(_module.Invocations, i => i.Identifier == "scheduler.init");
    }

    [Fact]
    public void Init_options_carry_the_events_param_passed_to_the_component()
    {
        var events = new[]
        {
            new L.SchedulerEvent("e1", "Team Meeting",
                DateTime.Today.AddHours(10), DateTime.Today.AddHours(11)),
            new L.SchedulerEvent("e2", "1:1 Review",
                DateTime.Today.AddHours(14), DateTime.Today.AddHours(15)),
        };

        _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, events));

        // options is the 3rd arg to scheduler.init(el, dotNetRef, options); its `events`
        // property is the serialized event array the JS layer renders as chips.
        var init = Assert.Single(_module.Invocations, i => i.Identifier == "scheduler.init");
        var options = init.Arguments[2]!;
        var serialized = options.GetType().GetProperty("events")!.GetValue(options);
        var array = Assert.IsAssignableFrom<System.Collections.IEnumerable>(serialized);
        Assert.Equal(2, array.Cast<object>().Count());
    }

    [Fact]
    public void Init_options_reflect_the_InitialView_param()
    {
        _ctx.Render<L.Scheduler>(p => p.Add(c => c.InitialView, L.SchedulerView.Week));

        // The starting view name is handed to FullCalendar through the init options'
        // `view` field, so the calendar boots directly into the requested view.
        var init = Assert.Single(_module.Invocations, i => i.Identifier == "scheduler.init");
        var options = init.Arguments[2]!;
        var view = options.GetType().GetProperty("view")!.GetValue(options);
        Assert.Equal("Week", view);
    }

    [Fact]
    public void Switching_view_dispatches_changeView_with_the_picked_view()
    {
        var cut = _ctx.Render<L.Scheduler>();

        // Pick "Week" from the view toolbar — single-mode ToggleGroup raises
        // ValueChanged("Week") → OnViewChangedAsync → scheduler.changeView(id, "Week").
        ViewButton(cut, "Week").Click();

        var changeView = Assert.Single(
            _module.Invocations,
            i => i.Identifier == "scheduler.changeView");
        Assert.Equal(InstanceId, changeView.Arguments[0]);
        Assert.Equal("Week", changeView.Arguments[1]);
    }

    [Fact]
    public void Switching_view_updates_the_active_toggle_aria_pressed_state()
    {
        var cut = _ctx.Render<L.Scheduler>();

        // Month is the default active view (InitialView default), so its toggle is pressed
        // and Day's is not — the single-select view toolbar contract.
        Assert.Equal("true", ViewButton(cut, "Month").GetAttribute("aria-pressed"));
        Assert.Equal("false", ViewButton(cut, "Day").GetAttribute("aria-pressed"));

        ViewButton(cut, "Day").Click();

        // After switching, active state moves to Day (the picked view flows back through
        // _currentView → the ToggleGroup Value binding) and Month is no longer pressed.
        Assert.Equal("true", ViewButton(cut, "Day").GetAttribute("aria-pressed"));
        Assert.Equal("false", ViewButton(cut, "Month").GetAttribute("aria-pressed"));
    }
}
