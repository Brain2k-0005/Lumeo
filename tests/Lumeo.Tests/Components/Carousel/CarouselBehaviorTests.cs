using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Carousel;

/// <summary>
/// Behaviour/a11y tests for the Carousel slide interaction. These drive the
/// component through its public surface — the Next/Previous nav buttons, the
/// keyboard arrow handler on the region, and the JS scroll-position callback —
/// and assert on the user-observable state: which indicator dot is current
/// (aria-current), the visually-hidden live-region announcement ("Slide N of M"),
/// the CanScrollPrev/Next button disabled state, and the carouselScrollTo
/// interop contract.
///
/// Loose-mode JSInterop records (but does not execute) the JS calls, so the
/// real scroll position never advances on its own. The component models the
/// active index in C# off the nav clicks, and learns CanScrollPrev/Next only
/// from the OnScrollPosition callback that JS would fire — which we simulate by
/// invoking the registered handler on the real ComponentInteropService.
/// </summary>
public class CarouselBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CarouselBehaviorTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.Carousel> RenderDeck(
        int slideCount = 3,
        bool showIndicators = true,
        L.Orientation orientation = L.Orientation.Horizontal)
    {
        return _ctx.Render<L.Carousel>(p =>
        {
            p.Add(c => c.Orientation, orientation);
            p.Add(c => c.ShowIndicators, showIndicators);
            p.Add(c => c.ChildContent, (RenderFragment)(b =>
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
            }));
        });
    }

    // Indicator dots — one button per slide, current one carries aria-current="true".
    private static IReadOnlyList<AngleSharp.Dom.IElement> Dots(IRenderedComponent<L.Carousel> cut) =>
        cut.FindAll("button")
            .Where(b => (b.GetAttribute("aria-label") ?? "").StartsWith("Go to slide"))
            .ToList();

    private static AngleSharp.Dom.IElement NextButton(IRenderedComponent<L.Carousel> cut) =>
        cut.FindAll("button").First(b => b.GetAttribute("aria-label") == "Next slide");

    private static AngleSharp.Dom.IElement PrevButton(IRenderedComponent<L.Carousel> cut) =>
        cut.FindAll("button").First(b => b.GetAttribute("aria-label") == "Previous slide");

    private static int CurrentDotIndex(IRenderedComponent<L.Carousel> cut)
    {
        var dots = Dots(cut);
        for (int i = 0; i < dots.Count; i++)
            if (dots[i].GetAttribute("aria-current") == "true")
                return i;
        return -1;
    }

    private static string LiveRegionText(IRenderedComponent<L.Carousel> cut) =>
        cut.Find("[aria-live='polite']").TextContent.Trim();

    // The scrollable content div carries the generated _contentId; JS keys its
    // scroll callbacks off it. Read it back so we can simulate OnScrollPosition.
    private static string ContentId(IRenderedComponent<L.Carousel> cut) =>
        cut.Find("[style*='scroll-snap-type']").Id!;

    // Simulate the JS scroll-position callback the carousel registered. This is
    // the only channel through which the deck learns CanScrollPrev/Next, so we
    // drive it on the real interop service exactly as the browser would.
    private async Task PushScrollPosition(IRenderedComponent<L.Carousel> cut,
        double scrollPos, double maxScroll, int nearestIndex)
    {
        var interop = _ctx.Services.GetRequiredService<ComponentInteropService>();
        await cut.InvokeAsync(() =>
            interop.OnScrollPosition(ContentId(cut), scrollPos, maxScroll, nearestIndex));
    }

    [Fact]
    public void First_Slide_Is_Current_And_Announced_Initially()
    {
        var cut = RenderDeck(slideCount: 3);

        Assert.Equal(0, CurrentDotIndex(cut));
        Assert.Equal("Slide 1 of 3", LiveRegionText(cut));
        // Nothing to go back to yet, but there are slides ahead.
        Assert.True(PrevButton(cut).HasAttribute("disabled"));
        Assert.False(NextButton(cut).HasAttribute("disabled"));
    }

    [Fact]
    public void Next_Button_Advances_Active_Slide_And_Issues_ScrollTo()
    {
        var cut = RenderDeck(slideCount: 3);

        NextButton(cut).Click();

        // Active indicator moved to the second dot; live region tracks it.
        Assert.Equal(1, CurrentDotIndex(cut));
        Assert.Equal("Slide 2 of 3", LiveRegionText(cut));

        NextButton(cut).Click();
        Assert.Equal(2, CurrentDotIndex(cut));
        Assert.Equal("Slide 3 of 3", LiveRegionText(cut));

        // Each advance must drive the JS scroll to the new index (1 then 2).
        var scrollTos = _ctx.JSInterop.Invocations
            .Where(i => i.Identifier == "carouselScrollTo")
            .Select(i => (int)i.Arguments[1]!)
            .ToList();
        Assert.Equal(new[] { 1, 2 }, scrollTos);
    }

    [Fact]
    public async Task Previous_Button_Steps_Active_Slide_Back()
    {
        var cut = RenderDeck(slideCount: 3);

        // Advance forward twice via the Next button.
        NextButton(cut).Click();
        NextButton(cut).Click();
        Assert.Equal(2, CurrentDotIndex(cut));

        // The browser reports we've scrolled away from the start, so going back
        // becomes possible — this is what enables the Previous button.
        await PushScrollPosition(cut, scrollPos: 200, maxScroll: 300, nearestIndex: 2);
        Assert.False(PrevButton(cut).HasAttribute("disabled"));

        PrevButton(cut).Click();

        Assert.Equal(1, CurrentDotIndex(cut));
        Assert.Equal("Slide 2 of 3", LiveRegionText(cut));

        // Last recorded scroll target is the slide we stepped back to.
        var lastScrollTo = _ctx.JSInterop.Invocations
            .Last(i => i.Identifier == "carouselScrollTo");
        Assert.Equal(1, (int)lastScrollTo.Arguments[1]!);
    }

    [Fact]
    public async Task Keyboard_Arrows_Advance_And_Retreat_The_Active_Slide()
    {
        var cut = RenderDeck(slideCount: 3);
        var region = cut.Find("[role='region']");

        // LTR horizontal deck: ArrowRight = forward.
        region.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal(1, CurrentDotIndex(cut));
        Assert.Equal("Slide 2 of 3", LiveRegionText(cut));

        // The browser reports the deck has scrolled off the start, enabling the
        // backward direction; ArrowLeft then steps the active slide back.
        await PushScrollPosition(cut, scrollPos: 150, maxScroll: 300, nearestIndex: 1);
        region.KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.Equal(0, CurrentDotIndex(cut));
        Assert.Equal("Slide 1 of 3", LiveRegionText(cut));
    }

    [Fact]
    public async Task ScrollPosition_Callback_Drives_CanScrollPrev_And_CanScrollNext()
    {
        var cut = RenderDeck(slideCount: 3);

        // At the start of the track: cannot go back, can go forward.
        await PushScrollPosition(cut, scrollPos: 0, maxScroll: 300, nearestIndex: 0);
        Assert.True(PrevButton(cut).HasAttribute("disabled"));
        Assert.False(NextButton(cut).HasAttribute("disabled"));
        Assert.Equal(0, CurrentDotIndex(cut));

        // In the middle: both directions available, active dot tracks nearest.
        await PushScrollPosition(cut, scrollPos: 150, maxScroll: 300, nearestIndex: 1);
        Assert.False(PrevButton(cut).HasAttribute("disabled"));
        Assert.False(NextButton(cut).HasAttribute("disabled"));
        Assert.Equal(1, CurrentDotIndex(cut));

        // At the end of the track: can go back, cannot go forward.
        await PushScrollPosition(cut, scrollPos: 300, maxScroll: 300, nearestIndex: 2);
        Assert.False(PrevButton(cut).HasAttribute("disabled"));
        Assert.True(NextButton(cut).HasAttribute("disabled"));
        Assert.Equal(2, CurrentDotIndex(cut));
    }

    [Fact]
    public void Clicking_Indicator_Dot_Jumps_To_That_Slide()
    {
        var cut = RenderDeck(slideCount: 4);

        // Jump straight to the third slide via its indicator dot.
        Dots(cut)[2].Click();

        Assert.Equal(2, CurrentDotIndex(cut));
        Assert.Equal("Slide 3 of 4", LiveRegionText(cut));

        var lastScrollTo = _ctx.JSInterop.Invocations
            .Last(i => i.Identifier == "carouselScrollTo");
        Assert.Equal(2, (int)lastScrollTo.Arguments[1]!);
    }
}
