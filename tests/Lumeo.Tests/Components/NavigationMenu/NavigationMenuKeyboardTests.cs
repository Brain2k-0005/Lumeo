using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.NavigationMenu;

/// <summary>
/// Regression tests for #227 — NavigationMenu had no arrow-key roving between
/// top-level triggers, no trigger ARIA state, no focus-into-content on open,
/// and the &lt;nav&gt; had no accessible name.
/// </summary>
public class NavigationMenuKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public NavigationMenuKeyboardTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderMenu(string? label = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.NavigationMenu>(0);
            if (label is not null) builder.AddAttribute(1, "Label", label);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.NavigationMenuList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
                {
                    // Two items so roving has somewhere to move.
                    for (var i = 0; i < 2; i++)
                    {
                        var label2 = i == 0 ? "Products" : "Company";
                        var content = i == 0 ? "Products content" : "Company content";
                        list.OpenComponent<L.NavigationMenuItem>(i);
                        list.AddAttribute(1, "ChildContent", (RenderFragment)(item =>
                        {
                            item.OpenComponent<L.NavigationMenuTrigger>(0);
                            item.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, label2)));
                            item.CloseComponent();

                            item.OpenComponent<L.NavigationMenuContent>(1);
                            item.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, content)));
                            item.CloseComponent();
                        }));
                        list.CloseComponent();
                    }
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Nav_Has_Default_Aria_Label()
    {
        var cut = RenderMenu();
        Assert.Equal("Main", cut.Find("nav").GetAttribute("aria-label"));
    }

    [Fact]
    public void Nav_Aria_Label_Is_Configurable()
    {
        var cut = RenderMenu(label: "Docs");
        Assert.Equal("Docs", cut.Find("nav").GetAttribute("aria-label"));
    }

    [Fact]
    public void Trigger_Has_Menu_ARIA()
    {
        var cut = RenderMenu();
        var trigger = cut.FindAll("button")[0];
        Assert.Equal("menu", trigger.GetAttribute("aria-haspopup"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Open_Trigger_Reflects_Expanded_And_Controls()
    {
        var cut = RenderMenu();
        cut.FindAll("button")[0].Click();

        var trigger = cut.FindAll("button")[0];
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        var controls = trigger.GetAttribute("aria-controls");
        Assert.False(string.IsNullOrEmpty(controls));
        Assert.NotNull(cut.Find($"#{controls}"));
    }

    [Fact]
    public void ArrowRight_Focuses_Next_Trigger()
    {
        var cut = RenderMenu();
        var first = cut.FindAll("button")[0];
        var secondId = cut.FindAll("button")[1].GetAttribute("id");

        first.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        cut.WaitForAssertion(() => Assert.Contains(_interop.FocusElementCalls, id => id == secondId));
    }

    [Fact]
    public void ArrowLeft_Wraps_To_Last_Trigger()
    {
        var cut = RenderMenu();
        var first = cut.FindAll("button")[0];
        var secondId = cut.FindAll("button")[1].GetAttribute("id");

        // ArrowLeft from the first wraps to the last.
        first.KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        cut.WaitForAssertion(() => Assert.Contains(_interop.FocusElementCalls, id => id == secondId));
    }

    [Fact]
    public void ArrowDown_Opens_And_Focuses_Content()
    {
        var cut = RenderMenu();
        var first = cut.FindAll("button")[0];

        first.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Contains("Products content", cut.Markup);
        var content = cut.Find("[role='menu']");
        var contentId = content.GetAttribute("id");
        cut.WaitForAssertion(() => Assert.Contains(_interop.FocusElementCalls, id => id == contentId));
    }
}
