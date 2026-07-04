using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Textarea;

/// <summary>
/// Regression: the &lt;div&gt; wrapping the &lt;textarea&gt; must be full-width, or
/// the textarea's own w-full resolves against a wrapper that shrink-wraps under the
/// root's `items-start` — collapsing the control. Mirrors the Select width fix.
/// </summary>
public class TextareaWidthTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public TextareaWidthTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Textarea_Wrapper_Div_Is_Full_Width()
    {
        var cut = _ctx.Render<Lumeo.Textarea>();
        // The textarea's direct parent is the inner wrapper div (textarea + optional counter).
        var wrapperDiv = cut.Find("textarea").ParentElement;
        Assert.NotNull(wrapperDiv);
        Assert.Contains("w-full", wrapperDiv!.GetAttribute("class") ?? "");
    }
}
