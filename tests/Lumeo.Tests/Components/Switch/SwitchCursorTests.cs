using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Switch;

/// <summary>
/// UX-audit regression tests for Switch / Checkbox / Toggle cursor +
/// focus-visible rings. These prevent future regressions from silently
/// dropping the interactive affordance on these tactile primitives.
/// </summary>
public class SwitchCursorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SwitchCursorTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Switch_Has_Cursor_Pointer_And_Focus_Ring()
    {
        var cut = _ctx.Render<Lumeo.Switch>();

        var cls = cut.Find("button").GetAttribute("class");
        Assert.Contains("cursor-pointer", cls);
        Assert.Contains("focus-visible:ring-2", cls);
        Assert.Contains("focus-visible:ring-ring", cls);
    }

    [Fact]
    public void Checkbox_Button_Has_Cursor_Pointer_And_Focus_Ring()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>();

        var cls = cut.Find("button").GetAttribute("class");
        Assert.Contains("cursor-pointer", cls);
        Assert.Contains("focus-visible:ring-2", cls);
    }

    [Fact]
    public void Checkbox_Label_Has_Cursor_Pointer()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p.Add(c => c.Label, "Agree"));

        Assert.Contains("cursor-pointer", cut.Find("label").GetAttribute("class"));
    }

    [Fact]
    public void Toggle_Has_Cursor_Pointer_And_Focus_Ring()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p.AddChildContent("B"));

        var cls = cut.Find("button").GetAttribute("class");
        Assert.Contains("cursor-pointer", cls);
        Assert.Contains("focus-visible:ring-2", cls);
    }
}
