using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TimePicker;

/// <summary>
/// FormField label-for wiring for TimePicker.
///
/// Inside a FormField the trigger &lt;button&gt; renders the generated EffectiveTriggerId, but the
/// splat (<c>@attributes</c>) used to render AFTER the explicit id, so a consumer
/// passing <c>id</c> through AdditionalAttributes silently overrode it. The label's
/// <c>for</c> (and any interop keyed off the trigger id) then pointed at an id no
/// longer on the element. TimePicker now strips a splatted id when inside a FormField.
/// </summary>
public class TimePickerFormFieldSplatIdTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TimePickerFormFieldSplatIdTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Consumer_Splatted_Id_Inside_FormField_Does_Not_Break_Label_For()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Label", "Pick a time");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TimePicker>(0);
                b.AddAttribute(1, "AdditionalAttributes", new Dictionary<string, object> { ["id"] = "consumer-id" });
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var buttonId = cut.Find("button").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");

        Assert.False(string.IsNullOrEmpty(buttonId));
        // Stripped: the generated EffectiveTriggerId owns the button; label `for` agrees.
        Assert.Equal(labelFor, buttonId);
        Assert.NotEqual("consumer-id", buttonId);
    }

    [Fact]
    public void Consumer_Splatted_Id_Outside_FormField_Still_Reaches_Button()
    {
        // Guard: id-stripping only applies inside a FormField. A standalone TimePicker
        // with a consumer-supplied id must still receive it on the trigger button.
        var cut = _ctx.Render<L.TimePicker>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "standalone-id" }));

        Assert.Equal("standalone-id", cut.Find("button").GetAttribute("id"));
    }
}
