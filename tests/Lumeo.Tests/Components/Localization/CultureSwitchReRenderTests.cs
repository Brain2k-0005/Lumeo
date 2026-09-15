using System.Globalization;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Localization;

/// <summary>
/// Field report #464 ("Component-internal localized defaults do not follow a runtime
/// language switch"): the reporter's app switches <see cref="CultureInfo.CurrentUICulture"/>
/// at runtime (no navigation, no remount) and expects already-mounted components to pick up
/// the new language on their next render.
///
/// This is a DIFFERENT scenario from <c>P2LocalizationSweepTests</c>, which mounts a FRESH
/// component instance per culture (a new instance always re-evaluates everything, so it can't
/// catch a value that was resolved once in <c>OnInitialized</c>/<c>OnParametersSet</c> and
/// cached in a field). Here every case mounts ONE instance under "en-US", flips
/// <see cref="CultureInfo.CurrentUICulture"/> to "de-DE" without touching any parameter, and
/// forces a re-render on that SAME instance via <c>cut.Render()</c> — bUnit's parameterless
/// re-render, which repaints the render tree without re-invoking
/// <c>OnInitialized</c>/<c>OnParametersSet</c>. A component whose default is only resolved in
/// one of those lifecycle methods and stored in a field would still show the English string
/// after this; a component that resolves the default live (inline in the render tree, or in a
/// property getter evaluated by the render tree) shows German.
///
/// Audit (T7, 5.10.0): grepped the whole component tree (`src/Lumeo/UI`, `src/Lumeo.DataGrid`,
/// `src/Lumeo.Scheduler`, `src/Lumeo.Gantt`, ...) for every pattern that could cache a resolved
/// localized string — field initializers, `OnInitialized`/`OnParametersSet` assignments,
/// `??=` against an `L[...]`/`Localizer[...]` lookup, static/`Lazy&lt;string&gt;` caches — and
/// found none: every consumer either interpolates `@L["..."]` directly in the render tree or
/// exposes it through a computed property getter (e.g. `DatePicker.EffectivePlaceholder`,
/// `Select.Placeholder ?? Context.Placeholder ?? L["Select.Placeholder"]`), both of which are
/// re-evaluated on every render. `LumeoLocalizer.TryGet` also reads
/// `CultureInfo.CurrentUICulture` on every lookup — no per-culture cache to invalidate. This
/// test proves that live behaviour end-to-end for the representative set the task brief calls
/// out (Dialog close label, DatePicker placeholder, Select placeholder, Pagination labels,
/// DataGrid empty text); Skeleton is intentionally excluded — another in-flight branch
/// (`fix/stepper-radioitem-skeleton`) owns its localization.
/// </summary>
public class CultureSwitchReRenderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CultureSwitchReRenderTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void SwitchTo(string culture)
        => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

    private async Task RunAsync(Func<Task> body)
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            SwitchTo("en-US");
            await body();
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public Task DialogContent_Close_Label_Follows_Runtime_Culture_Switch() => RunAsync(() =>
    {
        // Render the typed Dialog directly (not via the untyped _ctx.Render(RenderFragment)
        // overload) so `cut` wraps the Dialog instance itself. bUnit's parameterless
        // cut.Render() forces a re-render of the wrapped instance and genuinely re-executes
        // its BuildRenderTree — including the nested DialogContent it composes — matching how
        // a real ancestor's StateHasChanged() cascades in a running app. Going through the
        // untyped fragment overload instead makes `cut` wrap a hosting root one level ABOVE
        // Dialog; re-rendering that root leaves Dialog's own subtree untouched, which is an
        // artifact of the test harness, not a product behaviour worth asserting on.
        var cut = _ctx.Render<L.Dialog>(p => p
            .Add(d => d.Open, true)
            .Add(d => d.ChildContent, (Microsoft.AspNetCore.Components.RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.CloseComponent();
            })));

        Assert.Contains("Close", cut.Markup);
        Assert.DoesNotContain("Schließen", cut.Markup);

        SwitchTo("de-DE");
        cut.Render(); // force a re-render of the SAME instance, no parameter change

        Assert.Contains("Schließen", cut.Markup);
        return Task.CompletedTask;
    });

    [Fact]
    public Task DatePicker_Placeholder_Follows_Runtime_Culture_Switch() => RunAsync(() =>
    {
        var cut = _ctx.Render<L.DatePicker>((ComponentParameterCollectionBuilder<L.DatePicker> p) => { });

        Assert.Contains("Pick a date", cut.Markup);
        Assert.DoesNotContain("Datum wählen", cut.Markup);

        SwitchTo("de-DE");
        cut.Render();

        Assert.Contains("Datum wählen", cut.Markup);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Select_Placeholder_Follows_Runtime_Culture_Switch() => RunAsync(() =>
    {
        // See the note in DialogContent_Close_Label_Follows_Runtime_Culture_Switch above on why
        // this renders the typed Select directly rather than through the untyped fragment overload.
        var cut = _ctx.Render<L.Select>(p => p
            .Add(s => s.ChildContent, (Microsoft.AspNetCore.Components.RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
            })));

        Assert.Contains("Select an option", cut.Markup);
        Assert.DoesNotContain("Bitte auswählen", cut.Markup);

        SwitchTo("de-DE");
        cut.Render();

        Assert.Contains("Bitte auswählen", cut.Markup);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Pagination_Previous_Next_Labels_Follow_Runtime_Culture_Switch() => RunAsync(() =>
    {
        var prev = _ctx.Render<L.PaginationPrevious>((ComponentParameterCollectionBuilder<L.PaginationPrevious> p) => { });
        var next = _ctx.Render<L.PaginationNext>((ComponentParameterCollectionBuilder<L.PaginationNext> p) => { });

        Assert.Contains("Previous", prev.Markup);
        Assert.Contains("Next", next.Markup);

        SwitchTo("de-DE");
        prev.Render();
        next.Render();

        Assert.Contains("Zurück", prev.Markup);
        Assert.Contains("Weiter", next.Markup);
        return Task.CompletedTask;
    });

    private record Row(int Id, string Name);

    [Fact]
    public Task DataGrid_Empty_State_Text_Follows_Runtime_Culture_Switch() => RunAsync(() =>
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, new List<Row>())
            .Add(g => g.Columns, new List<Lumeo.DataGridColumn<Row>> { new() { Field = "Name", Title = "Name" } }));

        Assert.Contains("No data available", cut.Markup);
        Assert.DoesNotContain("Keine Daten vorhanden", cut.Markup);

        SwitchTo("de-DE");
        cut.Render();

        Assert.Contains("Keine Daten vorhanden", cut.Markup);
        return Task.CompletedTask;
    });
}
