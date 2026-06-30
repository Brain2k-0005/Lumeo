using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Textarea;

/// <summary>
/// Codex P2 — FormField label-for wiring for Textarea.
///
/// Inside a FormField the &lt;textarea&gt; renders the generated EffectiveId, but the
/// splat (<c>@attributes</c>) used to render AFTER the explicit id, so a consumer
/// passing <c>id</c> through AdditionalAttributes silently overrode it. The label's
/// <c>for</c> (and the auto-resize JS keyed off EffectiveId) then pointed at an id no
/// longer on the element. Textarea now strips a splatted id when inside a FormField.
/// </summary>
public class TextareaFormFieldTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TextareaFormFieldTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Consumer_Splatted_Id_Inside_FormField_Does_Not_Break_Label_For()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Label", "Biography");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.Textarea>(0);
                b.AddAttribute(1, "AdditionalAttributes", new Dictionary<string, object> { ["id"] = "consumer-id" });
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var textareaId = cut.Find("textarea").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");

        Assert.False(string.IsNullOrEmpty(textareaId));
        // Stripped: the generated EffectiveId owns the textarea; label `for` agrees.
        Assert.Equal(labelFor, textareaId);
        Assert.NotEqual("consumer-id", textareaId);
    }

    [Fact]
    public void Consumer_Splatted_Id_Outside_FormField_Still_Reaches_Textarea()
    {
        // Guard: id-stripping only applies inside a FormField. A standalone textarea
        // with a consumer-supplied id must still receive it.
        var cut = _ctx.Render<L.Textarea>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "standalone-id" }));

        Assert.Equal("standalone-id", cut.Find("textarea").GetAttribute("id"));
    }
}
