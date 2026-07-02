using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Popover;

/// <summary>
/// Tests for B2 fix: Popover.Class parameter merges without destroying base classes.
/// </summary>
public class PopoverClassTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PopoverClassTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderPopover(string? cssClass = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Popover>(0);
            if (cssClass is not null)
                builder.AddAttribute(1, "Class", cssClass);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.PopoverTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Toggle")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Default_wrapper_has_relative_base_class()
    {
        var cut = RenderPopover();
        var wrapper = cut.Find(".relative");
        Assert.Contains("inline-block", wrapper.ClassName);
        Assert.Contains("outline-none", wrapper.ClassName);
    }

    [Fact]
    public void Default_wrapper_does_not_have_extra_classes()
    {
        var cut = RenderPopover();
        Assert.DoesNotContain("relative inline-block outline-none w-full", cut.Markup);
    }

    [Fact]
    public void Class_merges_without_destroying_relative()
    {
        var cut = RenderPopover(cssClass: "w-full");
        var wrapper = cut.Find(".relative");
        Assert.Contains("relative", wrapper.ClassName);
        Assert.Contains("inline-block", wrapper.ClassName);
        Assert.Contains("w-full", wrapper.ClassName);
    }

    [Fact]
    public void Class_w_full_appears_in_wrapper_markup()
    {
        var cut = RenderPopover(cssClass: "w-full");
        Assert.Contains("relative inline-block outline-none w-full", cut.Markup);
    }
}
