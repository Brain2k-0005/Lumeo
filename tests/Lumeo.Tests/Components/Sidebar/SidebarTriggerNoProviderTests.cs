using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// Edge-data guard (battle-wave2 #191): a <c>SidebarTrigger</c> placed outside any
/// <c>SidebarProvider</c> has its cascaded <c>State</c> resolve to null. The click
/// handler must tolerate that — previously <c>@onclick="State.Toggle"</c> dereferenced
/// a null <c>State</c> and threw a NullReferenceException on click (tearing down the
/// circuit), even though the same line already used the null-safe <c>State?.IsCollapsed</c>.
/// The fix declares <c>State</c> nullable and guards the click with
/// <c>State?.Toggle.InvokeAsync()</c>.
/// </summary>
public class SidebarTriggerNoProviderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SidebarTriggerNoProviderTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Trigger_Outside_Provider_Renders_Without_Throwing()
    {
        // No SidebarProvider ancestor → cascaded State is null. Must still render.
        var ex = Record.Exception(() => _ctx.Render<L.SidebarTrigger>());

        Assert.Null(ex);
    }

    [Fact]
    public void Trigger_Outside_Provider_Click_Does_Not_Throw()
    {
        var cut = _ctx.Render<L.SidebarTrigger>();

        var button = cut.Find("button[aria-label='Toggle sidebar']");

        // Without the fix, the unguarded @onclick="State.Toggle" dereferences the null
        // cascaded State and throws NullReferenceException here.
        var ex = Record.Exception(() => button.Click());

        Assert.Null(ex);
    }

    [Fact]
    public void Trigger_Outside_Provider_Reports_Expanded_State()
    {
        // Sanity: with a null State the null-safe aria-expanded falls back to "true"
        // (treated as not-collapsed), and the button is still operable markup.
        var cut = _ctx.Render<L.SidebarTrigger>();

        var button = cut.Find("button[aria-label='Toggle sidebar']");
        Assert.Equal("true", button.GetAttribute("aria-expanded"));
    }
}
