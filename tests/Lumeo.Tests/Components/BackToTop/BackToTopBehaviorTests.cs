using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.BackToTop;

/// <summary>
/// Behaviour/a11y coverage for the BackToTop scroll affordance. The component
/// renders nothing until the JS scroll-position observer reports it visible:
/// on first render it registers a handler (RegisterBackToTop) keyed by a
/// generated id, and the browser later pushes visibility changes back via the
/// service's [JSInvokable] OnScrollVisibilityChanged. bUnit can't fire a real
/// scroll event, so these tests drive that callback through the concrete
/// ComponentInteropService (resolved from DI, the same instance the component
/// injected) and then assert the rendered affordance and its scroll-to-top JS
/// contract via the recorded JSInterop invocations (loose mode).
/// </summary>
public class BackToTopBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BackToTopBehaviorTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // The same concrete service the component injected (AddLumeoServices binds
    // IComponentInteropService to this instance), so its registered handler is
    // the one the component wired up in OnAfterRenderAsync.
    private ComponentInteropService Interop =>
        _ctx.Services.GetRequiredService<ComponentInteropService>();

    // Id the component generated and passed as registerBackToTop's first arg.
    private string RegisteredId()
    {
        var reg = Assert.Single(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "registerBackToTop");
        return Assert.IsType<string>(reg.Arguments[0]);
    }

    // Flip the affordance visible by invoking the service callback the JS scroll
    // observer would normally call. Marshalled onto the renderer's dispatcher via
    // cut.InvokeAsync so the handler's StateHasChanged settles before we assert.
    private static async Task SetVisible(
        IRenderedComponent<Lumeo.BackToTop> cut,
        ComponentInteropService interop,
        string id,
        bool visible) =>
        await cut.InvokeAsync(() => interop.OnScrollVisibilityChanged(id, visible));

    [Fact]
    public void Hidden_Until_Visibility_Event_Marks_It_Visible()
    {
        var cut = _ctx.Render<Lumeo.BackToTop>();

        // Nothing rendered yet — the affordance starts hidden.
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void Registers_Visibility_Observer_With_Threshold_On_First_Render()
    {
        _ctx.Render<Lumeo.BackToTop>(p => p
            .Add(b => b.VisibilityThreshold, 500));

        var reg = Assert.Single(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "registerBackToTop");
        // Arg order: [0]=id, [1]=DotNetObjectReference, [2]=threshold.
        Assert.False(string.IsNullOrWhiteSpace(reg.Arguments[0] as string));
        Assert.Equal(500, reg.Arguments[2]);
    }

    [Fact]
    public async Task Renders_Labelled_Button_When_Visibility_Callback_Fires()
    {
        var cut = _ctx.Render<Lumeo.BackToTop>();
        var id = RegisteredId();

        await SetVisible(cut, Interop, id, true);

        var button = cut.Find("button");
        Assert.Equal("Back to top", button.GetAttribute("aria-label"));
        Assert.Equal("button", button.GetAttribute("type"));
    }

    [Fact]
    public async Task Clicking_Invokes_ScrollToTop_Interop()
    {
        var cut = _ctx.Render<Lumeo.BackToTop>();
        var id = RegisteredId();
        await SetVisible(cut, Interop, id, true);

        // No scrollToTop call before the user clicks.
        Assert.DoesNotContain(_ctx.JSInterop.Invocations, i => i.Identifier == "scrollToTop");

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "scrollToTop");
    }

    [Fact]
    public async Task Stays_Hidden_When_Disabled_Even_After_Visibility_Callback()
    {
        var cut = _ctx.Render<Lumeo.BackToTop>(p => p
            .Add(b => b.Disabled, true));
        var id = RegisteredId();

        await SetVisible(cut, Interop, id, true);

        // Disabled gates the render even though the observer reports visible.
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public async Task Hides_Again_When_Visibility_Callback_Reports_False()
    {
        var cut = _ctx.Render<Lumeo.BackToTop>();
        var id = RegisteredId();

        await SetVisible(cut, Interop, id, true);
        Assert.Single(cut.FindAll("button"));

        await SetVisible(cut, Interop, id, false);
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public async Task Renders_Custom_ChildContent_Instead_Of_Default_Icon()
    {
        var cut = _ctx.Render<Lumeo.BackToTop>(p => p
            .AddChildContent("<span class=\"marker\">Up</span>"));
        var id = RegisteredId();

        await SetVisible(cut, Interop, id, true);

        var button = cut.Find("button");
        Assert.NotNull(button.QuerySelector("span.marker"));
        Assert.Contains("Up", button.TextContent);
    }
}
