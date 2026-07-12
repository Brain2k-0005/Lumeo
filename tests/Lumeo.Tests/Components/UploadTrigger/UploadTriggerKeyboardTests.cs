using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.UploadTrigger;

/// <summary>
/// UploadTrigger's focusable control is a native &lt;InputFile&gt; hidden with
/// <c>class="sr-only"</c> — a visually-hidden-but-focusable technique, NOT
/// <c>display:none</c> or <c>tabindex="-1"</c>. That distinction is the entire
/// keyboard-accessibility surface: Enter/Space opens the native file picker for free
/// once the input has focus, exactly as it would for a plain, visible
/// &lt;input type="file"&gt;. UploadTriggerTests.cs already pins the sr-only class and
/// the Disabled case; this file pins the ENABLED case (no tabindex override at all) and
/// that Multiple/Accept — which shape what Enter/Space actually opens — land on that
/// same native, focusable element rather than some other node.
/// </summary>
public class UploadTriggerKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public UploadTriggerKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Enabled_Trigger_Input_Has_No_TabIndex_Override_So_It_Stays_In_The_Tab_Order()
    {
        var cut = _ctx.Render<L.UploadTrigger>();

        Assert.Null(cut.Find("input[type='file']").GetAttribute("tabindex"));
    }

    [Fact]
    public void Multiple_And_Accept_Land_On_The_Same_Focusable_Native_Input_Enter_Space_Opens()
    {
        var cut = _ctx.Render<L.UploadTrigger>(p => p
            .Add(t => t.Multiple, true)
            .Add(t => t.Accept, "image/*"));

        var input = cut.Find("input[type='file']");
        Assert.True(input.HasAttribute("multiple"));
        Assert.Equal("image/*", input.GetAttribute("accept"));
        Assert.Contains("sr-only", input.GetAttribute("class"));
    }
}
