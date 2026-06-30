using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Combobox;

/// <summary>
/// Codex P2 — ComboboxInput is a "picker input" whose rendered id, inside a FormField, is the
/// FormField's ControlId (the field &lt;Label For&gt; targets it, and the focus / prevent-default-key
/// interop keys off it). A consumer-splatted id on the input used to render AFTER the explicit id= and
/// silently override it, orphaning the label and the interop. ComboboxInput now strips a splatted id
/// when the parent Combobox is inside a FormField (Context.InsideFormField); standalone, it still applies.
/// </summary>
public class ComboboxInputFormFieldSplatIdTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ComboboxInputFormFieldSplatIdTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment ComboboxWithInput(Dictionary<string, object>? inputAttrs) => b =>
    {
        b.OpenComponent<L.Combobox>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(c =>
        {
            c.OpenComponent<L.ComboboxInput>(0);
            if (inputAttrs is not null)
                c.AddMultipleAttributes(1, inputAttrs!);
            c.CloseComponent();
            c.OpenComponent<L.ComboboxContent>(2);
            c.CloseComponent();
        }));
        b.CloseComponent();
    };

    [Fact]
    public void Consumer_Splatted_Input_Id_Inside_FormField_Does_Not_Break_Label_For()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Label", "Fruit");
            builder.AddAttribute(2, "ChildContent", ComboboxWithInput(
                new Dictionary<string, object> { ["id"] = "consumer-id" }));
            builder.CloseComponent();
        });

        var inputId = cut.Find("input[role='combobox']").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");

        Assert.False(string.IsNullOrEmpty(inputId));
        // The FormField ControlId owns the input id AND the label's `for`; the splatted id was stripped.
        Assert.Equal(labelFor, inputId);
        Assert.NotEqual("consumer-id", inputId);
    }

    [Fact]
    public void Consumer_Splatted_Input_Id_Outside_FormField_Still_Applies()
    {
        var cut = _ctx.Render(ComboboxWithInput(
            new Dictionary<string, object> { ["id"] = "standalone-id" }));

        Assert.Equal("standalone-id", cut.Find("input[role='combobox']").GetAttribute("id"));
    }
}
