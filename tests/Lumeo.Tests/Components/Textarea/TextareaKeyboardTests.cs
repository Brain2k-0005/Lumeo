using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Textarea;

/// <summary>
/// Textarea has no custom @onkeydown — typing, newlines, selection are entirely native
/// &lt;textarea&gt; browser behavior. The only Lumeo-owned keyboard-adjacent surface is
/// that nothing pulls the element out of the natural Tab order, and that the @oninput
/// round-trip carries multi-line text (including the newlines a keyboard Enter produces)
/// through untouched.
/// </summary>
public class TextareaKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public TextareaKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Textarea_Has_No_TabIndex_Override_So_It_Stays_In_The_Natural_Tab_Order()
    {
        var cut = _ctx.Render<L.Textarea>();

        Assert.Null(cut.Find("textarea").GetAttribute("tabindex"));
    }

    [Fact]
    public void Typing_A_Newline_Round_Trips_Through_The_Native_Input_Event_Untouched()
    {
        string? received = null;
        var cut = _ctx.Render<L.Textarea>(p => p
            .Add(t => t.ValueChanged, v => received = v));

        // What the browser produces after Enter inside a <textarea>: an embedded '\n'.
        cut.Find("textarea").Input("line one\nline two");

        Assert.Equal("line one\nline two", received);
    }
}
