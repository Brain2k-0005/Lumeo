using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Carousel;

/// <summary>
/// Regression tests for the scroll-affordance state surviving a slide-count
/// change (Battle-wave1 finding #51, state-on-data-change).
///
/// The deck learns CanScrollPrev/Next from two channels: the JS
/// OnScrollPosition callback (real pixel measurements) and — newly — a
/// membership-change recompute. When the deck is scrolled to the end, Next goes
/// disabled. If more slides are then added (deck grows, or refills from empty),
/// Next must become operable again. Before the fix, _canScrollNext was only ever
/// updated from OnScrollPosition, so a grow left Next wrongly disabled until the
/// next browser scroll event — which may never come for a programmatic refill.
///
/// Mirrors <see cref="CarouselBehaviorTests"/>: loose-mode JSInterop records the
/// JS calls; the deck models its index/affordances in C#, and we simulate the
/// browser's OnScrollPosition callback on the real ComponentInteropService.
/// </summary>
public class CarouselItemsChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CarouselItemsChangeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ChildContent factory: a CarouselContent with `slideCount` items, plus the
    // Prev/Next nav buttons. Reused so the same deck can be re-rendered with a
    // different slide count (deck grow / refill-from-empty).
    private static RenderFragment DeckContent(int slideCount) => b =>
    {
        b.OpenComponent<L.CarouselContent>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(c =>
        {
            for (int i = 0; i < slideCount; i++)
            {
                int index = i;
                c.OpenComponent<L.CarouselItem>(index * 10);
                c.AddAttribute(index * 10 + 1, "ChildContent", (RenderFragment)(inner =>
                    inner.AddContent(0, $"Slide {index + 1}")));
                c.CloseComponent();
            }
        }));
        b.CloseComponent();

        b.OpenComponent<L.CarouselPrevious>(10);
        b.CloseComponent();

        b.OpenComponent<L.CarouselNext>(20);
        b.CloseComponent();
    };

    private IRenderedComponent<L.Carousel> RenderDeck(int slideCount)
        => _ctx.Render<L.Carousel>(p =>
        {
            p.Add(c => c.ShowIndicators, true);
            p.Add(c => c.ChildContent, DeckContent(slideCount));
        });

    private static AngleSharp.Dom.IElement NextButton(IRenderedComponent<L.Carousel> cut) =>
        cut.FindAll("button").First(b => b.GetAttribute("aria-label") == "Next slide");

    private static string ContentId(IRenderedComponent<L.Carousel> cut) =>
        cut.Find("[style*='scroll-snap-type']").Id!;

    private async Task PushScrollPosition(IRenderedComponent<L.Carousel> cut,
        double scrollPos, double maxScroll, int nearestIndex)
    {
        var interop = _ctx.Services.GetRequiredService<ComponentInteropService>();
        await cut.InvokeAsync(() =>
            interop.OnScrollPosition(ContentId(cut), scrollPos, maxScroll, nearestIndex));
    }

    [Fact]
    public async Task Growing_The_Deck_Reenables_Next_After_It_Went_Disabled_At_The_Old_End()
    {
        // Start with a 2-slide deck and scroll to its last slide. The browser
        // reports we're at the end of the track, so Next goes disabled.
        var cut = RenderDeck(slideCount: 2);
        await PushScrollPosition(cut, scrollPos: 200, maxScroll: 200, nearestIndex: 1);
        Assert.True(NextButton(cut).HasAttribute("disabled"));

        // Two more slides are appended (deck grows). _currentIndex stays at 1,
        // but there are now slides ahead of it again — Next must become operable
        // on the membership change alone, without waiting for a new JS scroll
        // event. Before the fix _canScrollNext stayed false here.
        cut.Render(p => p.Add(c => c.ChildContent, DeckContent(slideCount: 4)));

        Assert.False(NextButton(cut).HasAttribute("disabled"));
    }

    [Fact]
    public async Task Refilling_From_An_Empty_Deck_Leaves_Next_Operable()
    {
        // Deck mounts empty (async data not yet loaded). The browser reports an
        // empty track (maxScroll 0), so Next goes disabled through the normal
        // OnScrollPosition channel.
        var cut = RenderDeck(slideCount: 0);
        await PushScrollPosition(cut, scrollPos: 0, maxScroll: 0, nearestIndex: -1);
        Assert.True(NextButton(cut).HasAttribute("disabled"));

        // Data arrives and the deck refills to several slides. The membership
        // change must re-enable Next without waiting for a fresh JS scroll event.
        // Before the fix _canScrollNext stayed false from the empty-track report.
        cut.Render(p => p.Add(c => c.ChildContent, DeckContent(slideCount: 3)));

        Assert.False(NextButton(cut).HasAttribute("disabled"));
    }
}
