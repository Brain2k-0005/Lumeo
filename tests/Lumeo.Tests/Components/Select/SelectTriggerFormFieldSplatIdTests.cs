using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// Codex P2 — SelectTrigger is a "picker trigger" whose rendered id, inside a FormField, is the
/// FormField's ControlId (the field &lt;Label For&gt; targets it, and focus interop keys off it). A
/// consumer-splatted id on the trigger used to render AFTER the explicit id= and silently override it,
/// orphaning the label. SelectTrigger now strips a splatted id when the parent Select is inside a
/// FormField (Context.InsideFormField); standalone, the consumer id still applies.
/// </summary>
public class SelectTriggerFormFieldSplatIdTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SelectTriggerFormFieldSplatIdTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment SelectWithTrigger(Dictionary<string, object>? triggerAttrs) => b =>
    {
        b.OpenComponent<L.Select>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(c =>
        {
            c.OpenComponent<L.SelectTrigger>(0);
            c.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
            if (triggerAttrs is not null)
                c.AddMultipleAttributes(2, triggerAttrs!);
            c.CloseComponent();
            c.OpenComponent<L.SelectContent>(3);
            c.CloseComponent();
        }));
        b.CloseComponent();
    };

    [Fact]
    public void Consumer_Splatted_Trigger_Id_Inside_FormField_Does_Not_Break_Label_For()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Label", "Country");
            builder.AddAttribute(2, "ChildContent", SelectWithTrigger(
                new Dictionary<string, object> { ["id"] = "consumer-id" }));
            builder.CloseComponent();
        });

        var triggerId = cut.Find("button[role='combobox']").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");

        Assert.False(string.IsNullOrEmpty(triggerId));
        // The FormField ControlId owns the trigger id AND the label's `for`; the splatted id was stripped.
        Assert.Equal(labelFor, triggerId);
        Assert.NotEqual("consumer-id", triggerId);
    }

    [Fact]
    public void Consumer_Splatted_Trigger_Id_Outside_FormField_Still_Applies()
    {
        var cut = _ctx.Render(SelectWithTrigger(
            new Dictionary<string, object> { ["id"] = "standalone-id" }));

        Assert.Equal("standalone-id", cut.Find("button[role='combobox']").GetAttribute("id"));
    }
}
