using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Form;

/// <summary>
/// FormField auto-renders the validation error (the dominant simple-mode API). A child
/// &lt;FormMessage/&gt; must DEFER to it by default so the error never renders twice; with
/// <c>AutoRenderMessage="false"</c> the FormMessage owns it instead. One-or-the-other,
/// never both. (Before the fix, FormField + FormMessage rendered the error twice.)
/// </summary>
public class FormMessageDedupeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public FormMessageDedupeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string ErrorText = "Please enter a valid email.";

    private IRenderedComponent<IComponent> RenderFieldWithMessage(bool autoRenderMessage)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Error", ErrorText);
            builder.AddAttribute(2, "AutoRenderMessage", autoRenderMessage);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.FormMessage>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    // Count the rendered error <p> elements (leaf elements whose own text is the error
    // string and which carry the destructive colour) — excludes the wrapper div, which
    // shares the text but not the class.
    private static int ErrorElementCount(IRenderedComponent<IComponent> cut)
        => cut.FindAll("*").Count(e =>
            (e.TextContent ?? string.Empty).Trim() == ErrorText &&
            (e.GetAttribute("class") ?? string.Empty).Contains("text-destructive"));

    [Fact]
    public void Default_FormField_With_FormMessage_Renders_Error_Once()
    {
        var cut = RenderFieldWithMessage(autoRenderMessage: true);
        Assert.Equal(1, ErrorElementCount(cut)); // FormField auto-renders; FormMessage defers
    }

    [Fact]
    public void AutoRenderMessage_False_Lets_FormMessage_Own_The_Error_Once()
    {
        var cut = RenderFieldWithMessage(autoRenderMessage: false);
        Assert.Equal(1, ErrorElementCount(cut)); // FormField suppresses; FormMessage renders
    }
}
