using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.ToolCallCard;

/// <summary>
/// Wave 4 composition audit — ToolCallCard's disclosure is a native
/// &lt;details&gt;/&lt;summary&gt; (native keyboard toggle, no custom key
/// handling needed) plus native "copy" &lt;button @onclick="CopyAsync"&gt;s.
/// ToolCallCardTests already covers the copy button flipping to "Copied" via
/// .Click(). This file fills the one remaining neededTests gap: the copy
/// button invokes CopyToClipboard with the RIGHT target text — verified
/// per-section (Input vs Output) so a wrong-target regression (e.g. the Output
/// button copying Input) would be caught.
/// </summary>
public class ToolCallCardKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToolCallCardKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Input_Copy_Button_Copies_The_Input_Text()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x")
            .Add(c => c.Input, "{\"q\":\"hello\"}"));

        var copyBtn = cut.FindAll("button")
            .First(b => b.GetAttribute("aria-label") == "Copy to clipboard");
        copyBtn.Click();

        Assert.Contains(_ctx.JSInterop.Invocations,
            i => i.Identifier == "copyToClipboard" && (i.Arguments[0] as string) == "{\"q\":\"hello\"}");
    }

    [Fact]
    public void Output_Copy_Button_Copies_The_Output_Text_Not_The_Input()
    {
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p
            .Add(c => c.ToolName, "x")
            .Add(c => c.Status, Lumeo.ToolCallCard.ToolCallStatus.Success)
            .Add(c => c.Input, "input-payload")
            .Add(c => c.Output, "output-payload"));

        var buttons = cut.FindAll("button").Where(b => b.GetAttribute("aria-label") == "Copy to clipboard").ToList();
        Assert.Equal(2, buttons.Count); // Input section + Output section
        buttons[1].Click(); // Output's copy button (renders after Input's)

        Assert.Contains(_ctx.JSInterop.Invocations,
            i => i.Identifier == "copyToClipboard" && (i.Arguments[0] as string) == "output-payload");
    }

    [Fact]
    public void Summary_Toggle_Carries_No_Tabindex_Override()
    {
        // <summary> is natively focusable/activatable (Enter/Space toggles the
        // parent <details>) — it must not carry an explicit tabindex that would
        // remove or reorder its native Tab stop.
        var cut = _ctx.Render<Lumeo.ToolCallCard>(p => p.Add(c => c.ToolName, "x"));

        Assert.False(cut.Find("summary").HasAttribute("tabindex"));
    }
}
