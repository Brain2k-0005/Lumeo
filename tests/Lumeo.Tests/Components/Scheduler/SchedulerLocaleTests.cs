using System.Globalization;
using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Culture handling in <see cref="L.Scheduler"/>'s toolbar.
///
/// <para>
/// Most of this file described <c>ResolveFullCalendarLocale</c> — mapping a
/// <see cref="CultureInfo"/> onto a FullCalendar locale-pack code, including the "iv" trap
/// under invariant globalization. That mapping existed only to feed the JS bridge and went
/// with it; the first-party views format dates through <see cref="CultureInfo"/> directly, so
/// there is no locale code to get wrong any more.
/// </para>
/// <para>
/// What survives is the behaviour that was genuinely the component's own: the toolbar title
/// is cached, so a culture change arriving through an ordinary re-render has to invalidate
/// it. Without that a German toolbar sat over an English calendar until the user navigated.
/// </para>
/// </summary>
public class SchedulerLocaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    public SchedulerLocaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        CultureInfo.CurrentUICulture = _originalUiCulture;
        await _ctx.DisposeAsync();
    }

    [Fact]
    public void A_culture_change_refreshes_the_rendered_title_text()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, new DateTime(2026, 6, 15)));

        Assert.Contains("June 2026", cut.Markup, StringComparison.Ordinal);

        // The host flips the UI culture at runtime and re-renders for an unrelated reason.
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        cut.Render(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, new DateTime(2026, 6, 15))
            .Add(c => c.Height, "640px"));

        // Fails against a cached title that is never invalidated — it does not vacuously pass.
        Assert.Contains("Juni 2026", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("June 2026", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Invariant_culture_renders_without_throwing()
    {
        // The old "iv" trap was about a locale pack that did not exist. Nothing fetches a
        // pack now, but invariant globalization is still worth a render check.
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        var ex = Record.Exception(() => _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialDate, new DateTime(2026, 6, 15))));

        Assert.Null(ex);
    }

    /// <summary>
    /// Hands back a local date the test controls, and counts how often it was asked.
    /// </summary>
    // Re-implements the interface on purpose: the member has a DEFAULT interface
    // implementation, so a derived class that only declares the method never gets
    // dispatched to - the mapping belongs to whichever class states the interface.
    private sealed class CountingDateInterop : TrackingInteropService, IComponentInteropService
    {
        public string? LocalDate { get; set; }
        public int Asks { get; private set; }

        public Task<string?> SchedulerViewsGetLocalDateAsync()
        {
            Asks++;
            return Task.FromResult(LocalDate);
        }
    }

    [Fact]
    public async Task The_browser_date_is_re_read_rather_than_cached_from_the_first_render()
    {
        // A circuit that stays connected across the browser's local midnight kept marking
        // yesterday: the date was resolved once, on first render. The header inherited that the
        // moment it started reading this value (Codex review, PR #427).
        var ctx = new BunitContext();
        try
        {
            ctx.AddLumeoServices();
            var interop = new CountingDateInterop { LocalDate = "2026-04-15" };
            ctx.Services.AddSingleton<IComponentInteropService>(interop);

            var cut = ctx.Render<L.Scheduler>(p => p
                .Add(c => c.InitialView, L.SchedulerView.Week)
                .Add(c => c.InitialDate, new DateTime(2026, 4, 15))
                .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

            Assert.True(interop.Asks >= 1, "the date was never resolved at all");
            var afterFirst = interop.Asks;

            // Midnight passes in the browser while the circuit stays up.
            interop.LocalDate = "2026-04-16";
            cut.Render(p => p.Add(c => c.Events, new[]
            {
                new L.SchedulerEvent("e1", "Standup",
                    new DateTime(2026, 4, 16, 9, 0, 0), new DateTime(2026, 4, 16, 10, 0, 0)),
            }));

            Assert.True(interop.Asks > afterFirst,
                "the browser date was cached from the first render and never asked for again");

            var marked = cut.FindAll("[data-dayheader][data-today='true']");
            Assert.Single(marked);
            Assert.Equal("2026-04-16", marked[0].GetAttribute("data-dayheader"));
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }

}
