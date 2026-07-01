using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// Wave-2 state-on-data-change regressions: uncontrolled selection/check state must
/// NOT live in the [Parameter]. After an optimistic select/toggle, an unrelated
/// parent re-render that pushes the SAME (stale) parameter value must not revert the
/// user's choice. Both components now keep a private backing field seeded from the
/// parameter only when it actually changes (mirroring DropdownMenuSub's _open).
/// </summary>
public class DropdownMenuStateOnDataChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DropdownMenuStateOnDataChangeTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- n=79: DropdownMenuRadioGroup ---

    private static RenderFragment RadioItems => b =>
    {
        b.OpenComponent<L.DropdownMenuRadioItem>(0);
        b.AddAttribute(1, "Value", "a");
        b.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
        b.CloseComponent();

        b.OpenComponent<L.DropdownMenuRadioItem>(3);
        b.AddAttribute(4, "Value", "b");
        b.AddAttribute(5, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Banana")));
        b.CloseComponent();
    };

    [Fact]
    public void RadioGroup_Selection_Survives_Same_Value_Parent_Rerender()
    {
        // Uncontrolled group: starts with nothing selected, no ValueChanged binding.
        var cut = _ctx.Render<L.DropdownMenuRadioGroup>(p => p
            .Add(g => g.ChildContent, RadioItems));

        // User selects "b" — optimistic, lives in the backing field now.
        var banana = cut.FindAll("[role='menuitemradio']")[1];
        banana.Click();
        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[1].GetAttribute("aria-checked"));

        // An unrelated parent re-render pushes the SAME (stale, still-null) Value param.
        cut.Render(p => p.Add(g => g.ChildContent, RadioItems));

        // The selection must survive — the param did not change, so it must not clobber.
        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[1].GetAttribute("aria-checked"));
        Assert.Equal("false", cut.FindAll("[role='menuitemradio']")[0].GetAttribute("aria-checked"));
    }

    [Fact]
    public void RadioGroup_Controlled_Value_Param_Still_Wins_On_Change()
    {
        // When the parent actually changes Value, the group must adopt it.
        var cut = _ctx.Render<L.DropdownMenuRadioGroup>(p => p
            .Add(g => g.Value, "a")
            .Add(g => g.ChildContent, RadioItems));
        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[0].GetAttribute("aria-checked"));

        cut.Render(p => p.Add(g => g.Value, "b").Add(g => g.ChildContent, RadioItems));
        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[1].GetAttribute("aria-checked"));
    }

    // --- n=177: DropdownMenuCheckboxItem ---

    [Fact]
    public void CheckboxItem_Toggle_Survives_Same_Checked_Parent_Rerender()
    {
        // Uncontrolled item: starts unchecked, no CheckedChanged binding.
        var cut = _ctx.Render<L.DropdownMenuCheckboxItem>(p => p
            .Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Show grid"))));

        var btn = cut.Find("[role='menuitemcheckbox']");
        Assert.Equal("false", btn.GetAttribute("aria-checked"));

        // User toggles it on — optimistic, lives in the backing field now.
        btn.Click();
        Assert.Equal("true", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));

        // An unrelated parent re-render pushes the SAME (stale, still-false) Checked param.
        cut.Render(p => p.Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Show grid"))));

        // The toggle must survive — the param did not change.
        Assert.Equal("true", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));
    }

    [Fact]
    public void CheckboxItem_Controlled_Checked_Param_Still_Wins_On_Change()
    {
        // When the parent actually changes Checked, the item must adopt it.
        var cut = _ctx.Render<L.DropdownMenuCheckboxItem>(p => p
            .Add(i => i.Checked, false)
            .Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Show grid"))));
        Assert.Equal("false", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));

        cut.Render(p => p
            .Add(i => i.Checked, true)
            .Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Show grid"))));
        Assert.Equal("true", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));
    }
}
