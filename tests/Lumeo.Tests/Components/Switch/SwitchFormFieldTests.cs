using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Switch;

/// <summary>
/// Codex P2 — FormField label-for wiring for Switch.
///
/// A consumer-supplied id in AdditionalAttributes used to render AFTER the
/// generated FormFieldControlId and silently win, leaving the FormField label's
/// <c>for</c> pointing at an id that no longer existed on the button. Switch now
/// strips the splatted id when inside a FormField so the generated id is the one
/// both the label and the button use.
/// </summary>
public class SwitchFormFieldTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SwitchFormFieldTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Consumer_Splatted_Id_Inside_FormField_Does_Not_Break_Label_For()
    {
        // Regression (Codex P2): a consumer-splatted id in AdditionalAttributes must be
        // stripped when the Switch is inside a FormField, so the label's `for` always
        // matches the button's generated id. Before the fix the splat rendered last and
        // overrode the generated id, orphaning the label.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Label", "Enable notifications");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.Switch>(0);
                b.AddAttribute(1, "AdditionalAttributes", new Dictionary<string, object> { ["id"] = "consumer-id" });
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var buttonId = cut.Find("button[role='switch']").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");

        Assert.False(string.IsNullOrEmpty(buttonId));
        // The button id and the label for must agree — the consumer-splatted "consumer-id"
        // must have been stripped in favour of the generated FormFieldControlId.
        Assert.Equal(labelFor, buttonId);
        Assert.NotEqual("consumer-id", buttonId);
    }

    [Fact]
    public void Consumer_Splatted_Id_Outside_FormField_Still_Reaches_Button()
    {
        // Guard: the id-stripping only applies inside a FormField. A standalone
        // Switch with a consumer-supplied id must still receive that id.
        var cut = _ctx.Render<L.Switch>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "standalone-id" }));

        Assert.Equal("standalone-id", cut.Find("button[role='switch']").GetAttribute("id"));
    }
}
