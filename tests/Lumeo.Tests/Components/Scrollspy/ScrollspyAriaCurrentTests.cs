using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scrollspy;

/// <summary>
/// The scrollspy link for the section currently in view must expose
/// aria-current="location" (the section is the user's current location within the page),
/// alongside the existing data-active styling hook. Other links omit it.
/// </summary>
public class ScrollspyAriaCurrentTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public ScrollspyAriaCurrentTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderLinks(string activeId)
    {
        var ctx = new L.Scrollspy.ScrollspyContext(activeId, EventCallback.Factory.Create<string>(this, _ => { }));
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<CascadingValue<L.Scrollspy.ScrollspyContext>>(0);
            builder.AddAttribute(1, "Value", ctx);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ScrollspyLink>(0);
                b.AddAttribute(1, "Target", "intro");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Intro")));
                b.CloseComponent();

                b.OpenComponent<L.ScrollspyLink>(3);
                b.AddAttribute(4, "Target", "details");
                b.AddAttribute(5, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Details")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Active_Link_Exposes_AriaCurrent_Location()
    {
        var cut = RenderLinks(activeId: "intro");
        var intro = cut.FindAll("button").First(b => b.TextContent.Trim() == "Intro");
        Assert.Equal("location", intro.GetAttribute("aria-current"));
    }

    [Fact]
    public void Inactive_Link_Has_No_AriaCurrent()
    {
        var cut = RenderLinks(activeId: "intro");
        var details = cut.FindAll("button").First(b => b.TextContent.Trim() == "Details");
        Assert.False(details.HasAttribute("aria-current"));
    }
}
