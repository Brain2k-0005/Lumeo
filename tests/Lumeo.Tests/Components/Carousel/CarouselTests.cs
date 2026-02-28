using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Carousel;

public class CarouselTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CarouselTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderCarousel(
        bool loop = false,
        L.Carousel.CarouselOrientation orientation = L.Carousel.CarouselOrientation.Horizontal,
        int slideCount = 3,
        string? carouselClass = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Carousel>(0);
            builder.AddAttribute(1, "Loop", loop);
            builder.AddAttribute(2, "Orientation", orientation);
            if (carouselClass is not null)
                builder.AddAttribute(3, "Class", carouselClass);
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
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
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Renders_Region_With_Carousel_Role()
    {
        var cut = RenderCarousel();

        var region = cut.Find("[role='region']");
        Assert.NotNull(region);
        Assert.Equal("carousel", region.GetAttribute("aria-roledescription"));
    }

    [Fact]
    public void Renders_Slides_With_Group_Role()
    {
        var cut = RenderCarousel(slideCount: 3);

        var slides = cut.FindAll("[role='group']");
        Assert.Equal(3, slides.Count);
    }

    [Fact]
    public void Slide_Has_Aria_Roledescription_Slide()
    {
        var cut = RenderCarousel(slideCount: 1);

        var slide = cut.Find("[role='group']");
        Assert.Equal("slide", slide.GetAttribute("aria-roledescription"));
    }

    [Fact]
    public void Slide_Content_Is_Rendered()
    {
        var cut = RenderCarousel(slideCount: 2);

        Assert.Contains("Slide 1", cut.Markup);
        Assert.Contains("Slide 2", cut.Markup);
    }

    [Fact]
    public void Previous_Button_Is_Disabled_Initially()
    {
        var cut = RenderCarousel();

        // PreviousButton should be disabled when _canScrollPrev is false (initial state)
        var buttons = cut.FindAll("button");
        var prevBtn = buttons.FirstOrDefault(b =>
            b.InnerHtml.Contains("Previous slide"));
        Assert.NotNull(prevBtn);
        Assert.True(prevBtn!.HasAttribute("disabled"), "Previous button should be disabled initially");
    }

    [Fact]
    public void Next_Button_Is_Enabled_Initially()
    {
        var cut = RenderCarousel(slideCount: 3);

        var buttons = cut.FindAll("button");
        var nextBtn = buttons.FirstOrDefault(b =>
            b.InnerHtml.Contains("Next slide"));
        Assert.NotNull(nextBtn);
        Assert.False(nextBtn!.HasAttribute("disabled"), "Next button should be enabled initially");
    }

    [Fact]
    public void Horizontal_Carousel_Content_Has_Flex_Class()
    {
        var cut = RenderCarousel(orientation: L.Carousel.CarouselOrientation.Horizontal);

        // The inner scrollable div should have flex class
        var divs = cut.FindAll("div");
        Assert.True(divs.Any(d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("flex") && !cls.Contains("flex-col");
        }));
    }

    [Fact]
    public void Vertical_Carousel_Content_Has_Flex_Col_Class()
    {
        var cut = RenderCarousel(orientation: L.Carousel.CarouselOrientation.Vertical);

        var divs = cut.FindAll("div");
        Assert.True(divs.Any(d =>
        {
            var cls = d.GetAttribute("class") ?? "";
            return cls.Contains("flex-col");
        }));
    }

    [Fact]
    public void Carousel_Root_Has_Relative_Class()
    {
        var cut = RenderCarousel();

        var region = cut.Find("[role='region']");
        Assert.Contains("relative", region.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Custom_Class_On_Carousel_Root()
    {
        var cut = RenderCarousel(carouselClass: "my-carousel");

        var region = cut.Find("[role='region']");
        Assert.Contains("my-carousel", region.GetAttribute("class") ?? "");
    }

    [Fact]
    public void CarouselContent_Has_Overflow_Hidden_Class()
    {
        var cut = RenderCarousel();

        var divs = cut.FindAll("div");
        Assert.True(divs.Any(d =>
            (d.GetAttribute("class") ?? "").Contains("overflow-hidden")));
    }

    [Fact]
    public void Horizontal_Slide_Has_Pl4_Class()
    {
        var cut = RenderCarousel(orientation: L.Carousel.CarouselOrientation.Horizontal);

        var slides = cut.FindAll("[role='group']");
        Assert.NotEmpty(slides);
        Assert.Contains("pl-4", slides[0].GetAttribute("class") ?? "");
    }

    [Fact]
    public void Vertical_Slide_Has_Pt4_Class()
    {
        var cut = RenderCarousel(orientation: L.Carousel.CarouselOrientation.Vertical);

        var slides = cut.FindAll("[role='group']");
        Assert.NotEmpty(slides);
        Assert.Contains("pt-4", slides[0].GetAttribute("class") ?? "");
    }

    [Fact]
    public void Previous_Button_Has_Absolute_Positioning()
    {
        var cut = RenderCarousel();

        var buttons = cut.FindAll("button");
        var prevBtn = buttons.FirstOrDefault(b => b.InnerHtml.Contains("Previous slide"));
        Assert.NotNull(prevBtn);
        Assert.Contains("absolute", prevBtn!.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Next_Button_Has_Absolute_Positioning()
    {
        var cut = RenderCarousel();

        var buttons = cut.FindAll("button");
        var nextBtn = buttons.FirstOrDefault(b => b.InnerHtml.Contains("Next slide"));
        Assert.NotNull(nextBtn);
        Assert.Contains("absolute", nextBtn!.GetAttribute("class") ?? "");
    }
}
