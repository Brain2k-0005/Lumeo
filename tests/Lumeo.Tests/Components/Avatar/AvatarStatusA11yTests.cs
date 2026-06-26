using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Avatar;

/// <summary>
/// The avatar status indicator is a coloured dot; to avoid conveying meaning by colour
/// alone (WCAG 1.4.1) it exposes role="img" + an aria-label (the status name, or a
/// consumer override for localization). No status → no dot at all.
/// </summary>
public class AvatarStatusA11yTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public AvatarStatusA11yTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Status_Dot_Exposes_Role_Img_And_Default_Status_Label()
    {
        var cut = _ctx.Render<L.Avatar>(p => p.Add(x => x.Status, L.Avatar.AvatarStatus.Away));
        var dot = cut.Find("span[role='img']");
        Assert.Equal("Away", dot.GetAttribute("aria-label"));
    }

    [Fact]
    public void StatusLabel_Override_Is_Used_For_Localization()
    {
        var cut = _ctx.Render<L.Avatar>(p => p
            .Add(x => x.Status, L.Avatar.AvatarStatus.Online)
            .Add(x => x.StatusLabel, "En ligne"));
        Assert.Equal("En ligne", cut.Find("span[role='img']").GetAttribute("aria-label"));
    }

    [Fact]
    public void No_Status_Renders_No_Status_Dot()
    {
        var cut = _ctx.Render<L.Avatar>(p => p.Add(x => x.Status, L.Avatar.AvatarStatus.None));
        Assert.Empty(cut.FindAll("span[role='img']"));
    }
}
