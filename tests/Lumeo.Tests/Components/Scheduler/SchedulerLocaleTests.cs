using System.Globalization;
using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Regression tests for the wave-0 Scheduler i18n bug: no locale was ever passed to
/// FullCalendar (scheduler.js's <c>calOpts</c> never set a <c>locale</c>/<c>locales</c>
/// option at all), so even a fully-translated Lumeo locale had zero effect on
/// FullCalendar's own rendered chrome (day/month names, its internal button text).
///
/// Fixed by resolving <see cref="CultureInfo.CurrentUICulture"/> into a FullCalendar
/// locale code in <c>Scheduler.razor</c>'s <c>ResolveFullCalendarLocale</c>, threading
/// it through the init options (<c>options["locale"]</c>), and re-pushing it via a new
/// <c>scheduler.setLocale</c> interop call whenever <c>OnParametersSetAsync</c> observes
/// the resolved locale changing — not just once at init.
/// </summary>
public class SchedulerLocaleTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Scheduler/js/scheduler.js";
    private const string InstanceId = "sched-locale-1";

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;
    private CultureInfo _originalUiCulture = null!;

    public Task InitializeAsync()
    {
        _originalUiCulture = CultureInfo.CurrentUICulture;
        _ctx.AddLumeoServices();

        _module = _ctx.JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
        _module.Setup<string>("scheduler.init", _ => true).SetResult(InstanceId);
        _module.Setup<string>("scheduler.getTitle", _ => true).SetResult("");

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        CultureInfo.CurrentUICulture = _originalUiCulture;
        await _ctx.DisposeAsync();
    }

    [Fact]
    public void Init_options_carry_the_locale_resolved_from_current_ui_culture()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

        _ctx.Render<L.Scheduler>();

        var init = Assert.Single(_module.Invocations, i => i.Identifier == "scheduler.init");
        // Init options are Dictionary<string, object?> — see Scheduler.razor's
        // OnAfterRenderAsync (trim-safety comment there for why).
        var options = (System.Collections.Generic.IDictionary<string, object?>)init.Arguments[2]!;

        // Predicted WRONG behavior on the buggy code: the "locale" key does not exist in
        // the options dict at all (this indexer access would throw KeyNotFoundException),
        // because no locale was ever computed or passed to FullCalendar.
        Assert.Equal("de", options["locale"]);
    }

    [Fact]
    public void Init_options_map_chinese_to_full_calendars_zh_cn_locale_code()
    {
        // FullCalendar's own locale-file naming doesn't match the plain ISO two-letter
        // code for simplified Chinese ("zh-cn", not "zh") — exercises the one special
        // case in ResolveFullCalendarLocale beyond a straight lowercase pass-through.
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");

        _ctx.Render<L.Scheduler>();

        var init = Assert.Single(_module.Invocations, i => i.Identifier == "scheduler.init");
        var options = (System.Collections.Generic.IDictionary<string, object?>)init.Arguments[2]!;
        Assert.Equal("zh-cn", options["locale"]);
    }

    [Fact]
    public void Culture_change_after_init_pushes_an_updated_locale_to_the_live_calendar()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var cut = _ctx.Render<L.Scheduler>();

        // Simulate the host app switching the UI culture at runtime and re-rendering —
        // the SAME trigger every other Lumeo component's L[...] text already relies on
        // to pick up a culture change (a fresh parameter-set / render pass). Note this
        // re-render does NOT change any Scheduler parameter value itself (Height is
        // re-supplied unchanged) — the only thing that changed is the ambient culture,
        // which is exactly the "not only read once at init" bug being regression-tested.
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        cut.Render(p => p.Add(c => c.Height, "640px"));

        // Predicted WRONG behavior on the buggy code: scheduler.setLocale doesn't exist
        // and is never invoked (0 invocations) — the locale was only ever read once at
        // init, with no path at all to react to a later culture change.
        var setLocale = Assert.Single(_module.Invocations, i => i.Identifier == "scheduler.setLocale");
        Assert.Equal(InstanceId, setLocale.Arguments[0]);
        Assert.Equal("de", setLocale.Arguments[1]);
    }
}
