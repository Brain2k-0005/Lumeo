using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.BackToTop;

/// <summary>
/// Regression coverage for the edge-data bug (#98) where the documented/demoed
/// <c>Target</c> parameter did not exist on the component. Because
/// <see cref="Lumeo.BackToTop.AdditionalAttributes"/> uses
/// <c>CaptureUnmatchedValues = true</c>, a <c>Target="#sel"</c> usage was silently
/// captured and splatted onto the &lt;button&gt; as a bogus <c>target</c> HTML
/// attribute, while scroll tracking and scroll-to-top always operated on the
/// window — never the container — so the documented usage was broken.
///
/// The fix declares <c>Target</c> as a real [Parameter] and threads the selector
/// through RegisterBackToTop / ScrollToTop (and the JS). These tests assert the
/// MECHANISM via the recorded JSInterop invocations and the absence of the leaked
/// attribute. Arg order: registerBackToTop = [0]=id, [1]=DotNetObjectReference,
/// [2]=threshold, [3]=target; scrollToTop = [0]=target.
/// </summary>
public class BackToTopTargetTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BackToTopTargetTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Same concrete service the component injected, used to drive the visibility
    // callback the JS scroll observer would normally fire.
    private ComponentInteropService Interop =>
        _ctx.Services.GetRequiredService<ComponentInteropService>();

    private string RegisteredId()
    {
        var reg = Assert.Single(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "registerBackToTop");
        return Assert.IsType<string>(reg.Arguments[0]);
    }

    private async Task SetVisible(IRenderedComponent<Lumeo.BackToTop> cut, string id) =>
        await cut.InvokeAsync(() => Interop.OnScrollVisibilityChanged(id, true));

    [Fact]
    public void Target_Selector_Is_Threaded_To_Register_Observer()
    {
        _ctx.Render<Lumeo.BackToTop>(p => p
            .Add(b => b.Target, "#scroll-container")
            .Add(b => b.VisibilityThreshold, 250));

        var reg = Assert.Single(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "registerBackToTop");

        // Without the fix Target was not a parameter, so the selector never reached
        // the JS observer (registerBackToTop only ever received 3 args / window).
        Assert.Equal(250, reg.Arguments[2]);
        Assert.Equal("#scroll-container", reg.Arguments[3]);
    }

    [Fact]
    public async Task Target_Is_Not_Leaked_As_A_Bogus_Button_Attribute()
    {
        var cut = _ctx.Render<Lumeo.BackToTop>(p => p
            .Add(b => b.Target, "#scroll-container"));

        await SetVisible(cut, RegisteredId());

        var button = cut.Find("button");
        // Without the fix, CaptureUnmatchedValues splatted Target onto the button
        // as target="#scroll-container". As a real [Parameter] it must not leak.
        Assert.False(button.HasAttribute("target"));
        Assert.DoesNotContain("scroll-container", button.OuterHtml);
    }

    [Fact]
    public async Task Clicking_Scrolls_The_Targeted_Container()
    {
        var cut = _ctx.Render<Lumeo.BackToTop>(p => p
            .Add(b => b.Target, "#scroll-container"));

        await SetVisible(cut, RegisteredId());
        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        var scroll = Assert.Single(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "scrollToTop");

        // Without the fix scrollToTop took no args and always scrolled the window;
        // now the container selector is forwarded so the right element scrolls.
        Assert.Equal("#scroll-container", scroll.Arguments[0]);
    }

    [Fact]
    public void Null_Target_Still_Registers_Window_Scoped_Without_Throwing()
    {
        // The normal (no-Target) path must be unchanged: registration still
        // happens, just with a null target selector (window-scoped in JS).
        var exception = Record.Exception(() =>
            _ctx.Render<Lumeo.BackToTop>());

        Assert.Null(exception);

        var reg = Assert.Single(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "registerBackToTop");
        Assert.Null(reg.Arguments[3]);
    }
}
