using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Switch;

/// <summary>
/// Regression (battle-wave2 #65, state-on-data-change): a user toggle was
/// silently reverted by any unrelated parent re-render when <c>Checked</c> was
/// used WITHOUT a write-back binding (uncontrolled). The live checked state used
/// to live in the <c>[Parameter] Checked</c> itself, so a re-render that
/// re-supplied the same <c>Checked</c> literal clobbered the user's toggle.
/// The live state now lives in a private <c>_checked</c> backing field reseeded
/// only on a genuine parent change, so a same-value re-render survives while a
/// genuinely new <c>Checked</c> value from the parent still wins.
/// </summary>
public class SwitchUncontrolledStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SwitchUncontrolledStateTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void User_Toggle_Survives_Unrelated_ReRender_With_Same_Checked()
    {
        // Uncontrolled: Checked supplied but NO CheckedChanged write-back.
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(b => b.Checked, false));

        // User toggles the switch ON.
        cut.Find("button").Click();
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-checked"));

        // An unrelated parent re-render re-supplies the SAME (original) Checked
        // literal. This must NOT revert the user's toggle.
        cut.Render(p => p.Add(b => b.Checked, false));

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-checked"));
        Assert.Contains("bg-primary", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Genuinely_New_Checked_Value_From_Parent_Still_Wins()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(b => b.Checked, false));

        // User toggles ON locally.
        cut.Find("button").Click();
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-checked"));

        // Parent genuinely changes Checked to false -> parent regains authority.
        cut.Render(p => p.Add(b => b.Checked, false));
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-checked"));

        // Parent now pushes a genuinely different value (true): it wins.
        cut.Render(p => p.Add(b => b.Checked, true));
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-checked"));

        // And pushing a genuinely different value back to false also wins.
        cut.Render(p => p.Add(b => b.Checked, false));
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-checked"));
    }
}
