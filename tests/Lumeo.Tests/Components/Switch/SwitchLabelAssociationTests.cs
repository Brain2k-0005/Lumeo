using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Switch;

/// <summary>
/// Regression: <c>Label</c> rendered a bare &lt;label&gt; with no <c>for</c>
/// while the switch button had no <c>id</c> — clicking the label did nothing
/// and AT could not associate the two. The Checkbox <c>for="@_id"</c> pattern
/// is now applied.
/// </summary>
public class SwitchLabelAssociationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SwitchLabelAssociationTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Label_For_Matches_Button_Id()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(s => s.Label, "Notifications"));

        var label = cut.Find("label");
        var button = cut.Find("button[role='switch']");

        var forAttr = label.GetAttribute("for");
        var id = button.GetAttribute("id");

        Assert.False(string.IsNullOrEmpty(forAttr));
        Assert.False(string.IsNullOrEmpty(id));
        Assert.Equal(id, forAttr);
    }

    [Fact]
    public void Button_Has_Id_Even_Without_Label()
    {
        var cut = _ctx.Render<Lumeo.Switch>();
        Assert.False(string.IsNullOrEmpty(cut.Find("button[role='switch']").GetAttribute("id")));
    }
}
