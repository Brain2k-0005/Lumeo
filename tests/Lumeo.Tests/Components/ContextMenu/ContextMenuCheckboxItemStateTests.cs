using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// #74 (state-on-data-change) — ContextMenuCheckboxItem must NOT store its
/// uncontrolled checked state in the [Parameter] Checked. Blazor re-applies
/// [Parameter] values from the parent on every render, so a parent re-render that
/// pushes the original Checked value back down silently reverted a just-made
/// toggle. The fix keeps a private backing field seeded only on a real Checked
/// param change, mirroring the Collapsible controlled/uncontrolled fix (#246).
/// </summary>
public class ContextMenuCheckboxItemStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ContextMenuCheckboxItemStateTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Uncontrolled (no CheckedChanged binding): a toggle must survive a parent
    // re-render that re-pushes the original Checked="false" literal. Without the
    // backing-field fix the second Render reverts aria-checked back to "false".
    [Fact]
    public void Uncontrolled_Toggle_Survives_Same_Value_Parent_ReRender()
    {
        var cut = _ctx.Render<L.ContextMenuCheckboxItem>(p => p
            .Add(c => c.Checked, false)
            .Add(c => c.ChildContent, (RenderFragment)(b => b.AddContent(0, "Show toolbar"))));

        var button = cut.Find("button[role='menuitemcheckbox']");
        Assert.Equal("false", button.GetAttribute("aria-checked"));

        // User toggles it on (fires @onclick="Toggle").
        button.Click();
        Assert.Equal("true", cut.Find("button[role='menuitemcheckbox']").GetAttribute("aria-checked"));

        // Parent re-renders and re-applies the ORIGINAL Checked="false" literal.
        cut.Render(p => p
            .Add(c => c.Checked, false)
            .Add(c => c.ChildContent, (RenderFragment)(b => b.AddContent(0, "Show toolbar"))));

        // Toggle must survive: the backing field, not the parameter, drives state.
        Assert.Equal("true", cut.Find("button[role='menuitemcheckbox']").GetAttribute("aria-checked"));
    }

    // Controlled (CheckedChanged bound): the parent owns the value, so a genuine
    // param change still drives the rendered state.
    [Fact]
    public void Controlled_Checked_Param_Change_Updates_Rendered_State()
    {
        var changed = EventCallback.Factory.Create<bool>(this, (bool _) => { });

        var cut = _ctx.Render<L.ContextMenuCheckboxItem>(p => p
            .Add(c => c.Checked, false)
            .Add(c => c.CheckedChanged, changed)
            .Add(c => c.ChildContent, (RenderFragment)(b => b.AddContent(0, "Show toolbar"))));
        Assert.Equal("false", cut.Find("button[role='menuitemcheckbox']").GetAttribute("aria-checked"));

        cut.Render(p => p
            .Add(c => c.Checked, true)
            .Add(c => c.CheckedChanged, changed)
            .Add(c => c.ChildContent, (RenderFragment)(b => b.AddContent(0, "Show toolbar"))));
        Assert.Equal("true", cut.Find("button[role='menuitemcheckbox']").GetAttribute("aria-checked"));
    }
}
