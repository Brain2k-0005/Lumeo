using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Rating;

/// <summary>
/// FormField label-for wiring for Rating.
///
/// Inside a FormField the radiogroup &lt;div&gt; renders the generated EffectiveId, but the
/// splat (<c>@attributes</c>) used to render AFTER the explicit id, so a consumer
/// passing <c>id</c> through AdditionalAttributes silently overrode it. The FormField's
/// label <c>for</c> (and the preventDefault-keys JS interop keyed off EffectiveId) then
/// pointed at an id no longer on the element. Rating now strips a splatted id when inside
/// a FormField.
/// </summary>
public class RatingFormFieldSplatIdTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public RatingFormFieldSplatIdTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Consumer_Splatted_Id_Inside_FormField_Does_Not_Break_Label_For()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Label", "Rating");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.Rating>(0);
                b.AddAttribute(1, "AdditionalAttributes", new Dictionary<string, object> { ["id"] = "consumer-id" });
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var ratingId = cut.Find("[role='radiogroup']").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");

        Assert.False(string.IsNullOrEmpty(ratingId));
        // Stripped: the generated EffectiveId owns the radiogroup; label `for` agrees.
        Assert.Equal(labelFor, ratingId);
        Assert.NotEqual("consumer-id", ratingId);
    }

    [Fact]
    public void Consumer_Splatted_Id_Outside_FormField_Still_Reaches_Rating()
    {
        // Guard: id-stripping only applies inside a FormField. A standalone rating
        // with a consumer-supplied id must still receive it.
        var cut = _ctx.Render<L.Rating>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["id"] = "standalone-id" }));

        Assert.Equal("standalone-id", cut.Find("[role='radiogroup']").GetAttribute("id"));
    }
}
