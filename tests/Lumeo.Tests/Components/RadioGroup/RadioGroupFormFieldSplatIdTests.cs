using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.RadioGroup;

/// <summary>
/// Codex P2 — FormField label-for wiring for RadioGroup.
///
/// Inside a FormField the &lt;div role="radiogroup"&gt; renders the generated EffectiveContainerId,
/// but the splat (<c>@attributes</c>) used to render AFTER the explicit id, so a consumer
/// passing <c>id</c> through AdditionalAttributes silently overrode it. The label's
/// <c>for</c> (and the roving-nav JS keyed off the container id) then pointed at an id no
/// longer on the element. RadioGroup now strips a splatted id when inside a FormField.
/// </summary>
public class RadioGroupFormFieldSplatIdTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public RadioGroupFormFieldSplatIdTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Consumer_Splatted_Id_Inside_FormField_Does_Not_Break_Label_For()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Label", "Preference");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.RadioGroup>(0);
                b.AddAttribute(1, "AdditionalAttributes", new Dictionary<string, object> { ["id"] = "consumer-id" });
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var radiogroupId = cut.Find("[role=radiogroup]").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");

        Assert.False(string.IsNullOrEmpty(radiogroupId));
        // Stripped: the generated EffectiveContainerId owns the radiogroup; label `for` agrees.
        Assert.Equal(labelFor, radiogroupId);
        Assert.NotEqual("consumer-id", radiogroupId);
    }

    [Fact]
    public void Consumer_Splatted_Id_Outside_FormField_Still_Reaches_RadioGroup()
    {
        // Guard: id-stripping only applies inside a FormField. A standalone RadioGroup
        // with a consumer-supplied id must still receive it.
        var cut = _ctx.Render<L.RadioGroup>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "standalone-id" }));

        Assert.Equal("standalone-id", cut.Find("[role=radiogroup]").GetAttribute("id"));
    }
}
