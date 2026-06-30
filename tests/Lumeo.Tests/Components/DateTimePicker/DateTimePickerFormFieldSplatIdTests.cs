using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateTimePicker;

/// <summary>
/// Codex P2 — FormField label-for wiring for DateTimePicker.
///
/// Inside a FormField the trigger &lt;button&gt; renders the generated FormFieldControlId, but the
/// splat (<c>@attributes</c>) used to render AFTER the explicit id, so a consumer passing
/// <c>id</c> through AdditionalAttributes silently overrode it. The label's <c>for</c> then
/// pointed at an id no longer on the element. DateTimePicker now strips a splatted id when
/// inside a FormField.
/// </summary>
public class DateTimePickerFormFieldSplatIdTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DateTimePickerFormFieldSplatIdTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Consumer_Splatted_Id_Inside_FormField_Does_Not_Break_Label_For()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Label", "Pick date");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DateTimePicker>(0);
                b.AddAttribute(1, "AdditionalAttributes", new Dictionary<string, object> { ["id"] = "consumer-id" });
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var buttonId = cut.Find("button").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");

        Assert.False(string.IsNullOrEmpty(buttonId));
        // The FormField-generated id must own the button; label `for` must agree.
        Assert.Equal(labelFor, buttonId);
        Assert.NotEqual("consumer-id", buttonId);
    }

    [Fact]
    public void Consumer_Splatted_Id_Outside_FormField_Still_Reaches_Button()
    {
        // Guard: id-stripping only applies inside a FormField. A standalone
        // DateTimePicker with a consumer-supplied id must still receive it.
        var cut = _ctx.Render<L.DateTimePicker>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "standalone-id" }));

        Assert.Equal("standalone-id", cut.Find("button").GetAttribute("id"));
    }
}
