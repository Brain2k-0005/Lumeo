using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scrollspy;

/// <summary>
/// ScrollspyLink renders a native &lt;button @onclick="HandleClick"&gt; (not an &lt;a
/// href&gt;) — Enter/Space activation is free via the browser's default button
/// semantics, so .Click() exercises the exact handler a synthesized keydown would run.
/// Previously untested (ScrollspyAriaCurrentTests only pins the aria-current marker):
/// activating a link actually invokes the cascaded Scrollspy context's OnNavigate with
/// its Target, and the button carries no tabindex override that would pull it out of
/// the native tab order.
/// </summary>
public class ScrollspyKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public ScrollspyKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderLink(string target, EventCallback<string> onNavigate)
    {
        var context = new L.Scrollspy.ScrollspyContext(null, onNavigate);
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<CascadingValue<L.Scrollspy.ScrollspyContext>>(0);
            builder.AddAttribute(1, "Value", context);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ScrollspyLink>(0);
                b.AddAttribute(1, "Target", target);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Section")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Activating_A_Link_Invokes_OnNavigate_With_Its_Target()
    {
        string? navigated = null;
        var cut = RenderLink("section-2", EventCallback.Factory.Create<string>(this, t => navigated = t));

        cut.Find("button").Click();

        Assert.Equal("section-2", navigated);
    }

    [Fact]
    public void Link_Button_Carries_No_Tabindex_Override()
    {
        var cut = RenderLink("section-2", EventCallback.Factory.Create<string>(this, _ => { }));

        Assert.False(cut.Find("button").HasAttribute("tabindex"));
    }
}
