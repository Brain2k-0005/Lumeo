using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Switch;

/// <summary>
/// shadcn-parity Wave 2 (data-state on the switch button + thumb) and Wave 3
/// (hidden bubble input carrying Name/Value for native form POST) for
/// <see cref="Lumeo.Switch"/>.
/// </summary>
public class SwitchDataStateFormTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SwitchDataStateFormTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Button_DataState_Unchecked_By_Default()
    {
        var cut = _ctx.Render<Lumeo.Switch>();
        Assert.Equal("unchecked", cut.Find("button").GetAttribute("data-state"));
    }

    [Fact]
    public void Button_And_Thumb_DataState_Checked_When_Checked()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(s => s.Checked, true));
        Assert.Equal("checked", cut.Find("button").GetAttribute("data-state"));
        Assert.Equal("checked", cut.Find("span[data-state]").GetAttribute("data-state"));
    }

    [Fact]
    public void DataState_Flips_On_Click()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(s => s.Checked, false));
        Assert.Equal("unchecked", cut.Find("button").GetAttribute("data-state"));
        cut.Find("button").Click();
        Assert.Equal("checked", cut.Find("button").GetAttribute("data-state"));
    }

    [Fact]
    public void DataDisabled_Present_Only_When_Disabled()
    {
        Assert.False(_ctx.Render<Lumeo.Switch>().Find("button").HasAttribute("data-disabled"));
        var disabled = _ctx.Render<Lumeo.Switch>(p => p.Add(s => s.Disabled, true));
        Assert.True(disabled.Find("button").HasAttribute("data-disabled"));
    }

    // --- Wave 3 ---

    [Fact]
    public void No_Bubble_Input_When_Name_Absent()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(s => s.Checked, true));
        Assert.Empty(cut.FindAll("input[type=checkbox]"));
    }

    [Fact]
    public void Name_Is_On_Bubble_Input_Not_The_Button()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(s => s.Name, "notifications")
            .Add(s => s.Value, "yes")
            .Add(s => s.Checked, true));

        // A <button name> posts nothing, so the name must move to the bubble input.
        Assert.False(cut.Find("button").HasAttribute("name"));

        var input = cut.Find("input[type=checkbox]");
        Assert.Equal("notifications", input.GetAttribute("name"));
        Assert.Equal("yes", input.GetAttribute("value"));
        Assert.Equal("-1", input.GetAttribute("tabindex"));
        Assert.Equal("true", input.GetAttribute("aria-hidden"));
        Assert.True(input.HasAttribute("checked"));
    }

    [Fact]
    public void Bubble_Input_Defaults_Value_To_On()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(s => s.Name, "toggle"));
        Assert.Equal("on", cut.Find("input[type=checkbox]").GetAttribute("value"));
    }

    [Fact]
    public void Bubble_Input_Uses_Cascaded_FormField_Name_Without_Direct_Name()
    {
        // Round-3 P2: a Switch composed via <FormField Name="..."> — with no direct Name —
        // must still emit the bubble input so it posts in a native <form>. Pre-fix the
        // bubble input rendered only when the Switch's OWN Name was set, so this posted
        // nothing.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<FormField>(0);
            builder.AddAttribute(1, "Name", "terms");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<Lumeo.Switch>(0);
                b.AddAttribute(1, "Checked", true);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var input = cut.Find("input[type=checkbox]");
        Assert.Equal("terms", input.GetAttribute("name"));
        Assert.True(input.HasAttribute("checked"));
    }

    [Fact]
    public void Direct_Name_Wins_Over_Cascaded_FormField_Name()
    {
        // A direct Name takes precedence over the cascaded field name.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<FormField>(0);
            builder.AddAttribute(1, "Name", "field-name");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<Lumeo.Switch>(0);
                b.AddAttribute(1, "Name", "switch-name");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Equal("switch-name", cut.Find("input[type=checkbox]").GetAttribute("name"));
    }

    [Fact]
    public void Bubble_Input_Is_Disabled_While_Loading()
    {
        // Round-2 P2: the visible button is disabled by Loading, but the hidden bubble
        // input previously stayed enabled — so the switch could still submit its value in
        // a native form POST mid-load. The bubble input must mirror Disabled || Loading.
        var notLoading = _ctx.Render<Lumeo.Switch>(p => p
            .Add(s => s.Name, "toggle").Add(s => s.Checked, true));
        Assert.False(notLoading.Find("input[type=checkbox]").HasAttribute("disabled"));

        var loading = _ctx.Render<Lumeo.Switch>(p => p
            .Add(s => s.Name, "toggle").Add(s => s.Checked, true).Add(s => s.Loading, true));
        Assert.True(loading.Find("input[type=checkbox]").HasAttribute("disabled"));
        // The visible switch button is disabled too — the two now agree.
        Assert.True(loading.Find("button[role=switch]").HasAttribute("disabled"));
    }
}
