using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Field report #464, finding A: <c>Scheduler</c> used to declare
/// <c>[Parameter] public EventCallback&lt;string&gt; OnInitError</c> (see <c>Chart</c>/<c>Gantt</c>/
/// <c>RichTextEditor</c>, which still do — it surfaced their own JS bundle-load / constructor
/// failures). It was dropped when the FullCalendar-era JS bridge was replaced by the first-party
/// Blazor views (<c>SchedulerDependencyGuardTests</c> proves no such library is loaded any more),
/// but the parameter was never re-declared. A consumer still binding it therefore gets no compile
/// error — <c>AdditionalAttributes</c>' catch-all silently swallows it — and no runtime signal
/// either: the handler simply never fires.
///
/// The chosen fix is "fail loudly instead of silently" (per the field report), not "re-add the
/// callback": nothing in Scheduler.razor's init gates rendering on a JS call any more, so there
/// is no path left for OnInitError to ever observe. <see cref="OnParametersSet"/> throws when it
/// detects the attribute.
/// </summary>
public class SchedulerOnInitErrorGuardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerOnInitErrorGuardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Binding_OnInitError_Throws_Instead_Of_Being_Silently_Swallowed()
    {
        var ex = Record.Exception(() => _ctx.Render<L.Scheduler>(p => p
            .AddUnmatched("OnInitError", EventCallback.Factory.Create<string>(this, _ => { }))));

        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("OnInitError", ex!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Scheduler_With_No_Stray_Attributes_Renders_Normally()
    {
        // The guard must be SPECIFIC to the removed parameter, not a blanket rejection of every
        // unmatched attribute (data-*, aria-*, etc. all still legitimately flow through
        // AdditionalAttributes to the root card).
        var ex = Record.Exception(() => _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialDate, new DateTime(2026, 3, 15))
            .AddUnmatched("data-testid", "my-scheduler")));

        Assert.Null(ex);
    }

    /// <summary>
    /// Simulates the ONLY genuine JS-interop failure mode left in the first-party engine (a
    /// standalone install missing <c>scheduler-views.js</c>, so the drag-registration call
    /// itself throws a plain <see cref="Microsoft.JSInterop.JSException"/> rather than a
    /// <see cref="Microsoft.JSInterop.JSDisconnectedException"/>) — proving the render survives
    /// it. This is the evidence for WHY finding A's answer is "guard + throw" rather than
    /// "re-add OnInitError": the failure degrades gracefully on its own, so there is genuinely
    /// nothing left for that callback to report.
    /// </summary>
    private sealed class ThrowingDragRegistrationInterop : TrackingInteropService, IComponentInteropService
    {
        public new Task SchedulerViewsRegisterMonthDragAsync<T>(ElementReference el, DotNetObjectReference<T> dotNetRef, object options) where T : class =>
            throw new JSException("scheduler-views.js: registerMonthDrag is not a function");
    }

    [Fact]
    public async Task A_Broken_Drag_Registration_Call_Does_Not_Crash_The_Render()
    {
        var ctx = new BunitContext();
        try
        {
            ctx.AddLumeoServices();
            ctx.Services.AddSingleton<IComponentInteropService>(new ThrowingDragRegistrationInterop());

            var ex = Record.Exception(() => ctx.Render<L.Scheduler>(p => p
                .Add(c => c.InitialView, L.SchedulerView.Month)
                .Add(c => c.InitialDate, new DateTime(2026, 3, 15))
                .Add(c => c.Events, Array.Empty<L.SchedulerEvent>())));

            Assert.Null(ex);
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }

    /// <summary>
    /// Round-1 fix-review finding (Critical): the initial re-inventory of remaining
    /// <c>Interop.Scheduler*</c> calls missed <see cref="L.SchedulerTimeGridView"/>'s
    /// now-indicator registration — <c>SyncNowIndicatorAsync</c> only caught
    /// <see cref="JSDisconnectedException"/> around <c>SchedulerViewsRegisterNowIndicatorAsync</c>,
    /// so the identical "scheduler-views.js missing/broken" scenario the drag-registration sites
    /// already guard against would throw uncaught there instead — and <c>NowIndicator</c> defaults
    /// to true, so Week/Day views hit this on an ordinary render, not an edge case.
    /// </summary>
    private sealed class ThrowingNowIndicatorInterop : TrackingInteropService, IComponentInteropService
    {
        public new Task SchedulerViewsRegisterNowIndicatorAsync(ElementReference el, object options) =>
            throw new JSException("scheduler-views.js: registerNowIndicator is not a function");
    }

    [Fact]
    public async Task A_Broken_Now_Indicator_Registration_Call_Does_Not_Crash_The_Render()
    {
        var ctx = new BunitContext();
        try
        {
            ctx.AddLumeoServices();
            ctx.Services.AddSingleton<IComponentInteropService>(new ThrowingNowIndicatorInterop());

            // Week view: NowIndicator defaults to true, so this exercises the ordinary render
            // path (not an opt-in edge case) — matches the reviewer's "runs on every render...
            // NowIndicator defaults to true" concern.
            var ex = Record.Exception(() => ctx.Render<L.Scheduler>(p => p
                .Add(c => c.InitialView, L.SchedulerView.Week)
                .Add(c => c.InitialDate, new DateTime(2026, 3, 15))
                .Add(c => c.Events, Array.Empty<L.SchedulerEvent>())));

            Assert.Null(ex);
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }
}
