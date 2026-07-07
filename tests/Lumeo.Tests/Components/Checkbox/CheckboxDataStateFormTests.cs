using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Checkbox;

/// <summary>
/// shadcn-parity Wave 2 (data-state styling hooks) + Wave 3 (native form
/// participation via a hidden bubble input) for <see cref="Lumeo.Checkbox"/>.
/// </summary>
public class CheckboxDataStateFormTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CheckboxDataStateFormTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void DataState_Unchecked_By_Default()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>();
        Assert.Equal("unchecked", cut.Find("[role=checkbox]").GetAttribute("data-state"));
    }

    [Fact]
    public void DataState_Checked_When_Checked()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p.Add(c => c.Checked, true));
        Assert.Equal("checked", cut.Find("[role=checkbox]").GetAttribute("data-state"));
    }

    [Fact]
    public void DataState_Indeterminate_When_Indeterminate()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p.Add(c => c.IsIndeterminate, true));
        Assert.Equal("indeterminate", cut.Find("[role=checkbox]").GetAttribute("data-state"));
    }

    [Fact]
    public void DataDisabled_Present_Only_When_Disabled()
    {
        var enabled = _ctx.Render<Lumeo.Checkbox>();
        Assert.False(enabled.Find("[role=checkbox]").HasAttribute("data-disabled"));

        var disabled = _ctx.Render<Lumeo.Checkbox>(p => p.Add(c => c.Disabled, true));
        Assert.True(disabled.Find("[role=checkbox]").HasAttribute("data-disabled"));
    }

    // --- Wave 3: hidden bubble input for native <form> POST ---

    [Fact]
    public void No_Bubble_Input_When_Name_Absent()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p.Add(c => c.Checked, true));
        Assert.Empty(cut.FindAll("input[type=checkbox]"));
    }

    [Fact]
    public void Bubble_Input_Carries_Name_And_Default_Value()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(c => c.Name, "terms")
            .Add(c => c.Checked, true));

        var input = cut.Find("input[type=checkbox]");
        Assert.Equal("terms", input.GetAttribute("name"));
        Assert.Equal("on", input.GetAttribute("value"));      // native checkbox default
        Assert.Equal("-1", input.GetAttribute("tabindex"));   // non-focusable
        Assert.Equal("true", input.GetAttribute("aria-hidden"));
        Assert.True(input.HasAttribute("checked"));
    }

    [Fact]
    public void Bubble_Input_Custom_Value_And_Unchecked_State()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(c => c.Name, "plan")
            .Add(c => c.Value, "pro")
            .Add(c => c.Checked, false));

        var input = cut.Find("input[type=checkbox]");
        Assert.Equal("pro", input.GetAttribute("value"));
        Assert.False(input.HasAttribute("checked"));          // unchecked posts nothing
    }

    [Fact]
    public void Bubble_Input_Uses_Cascaded_FormField_Name_Without_Direct_Name()
    {
        // Round-3 P2: a Checkbox composed via <FormField Name="..."> — with no direct Name —
        // must still emit the bubble input so it posts in a native <form>. Pre-fix the
        // bubble input rendered only when the Checkbox's OWN Name was set.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<FormField>(0);
            builder.AddAttribute(1, "Name", "terms");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<Lumeo.Checkbox>(0);
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
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<FormField>(0);
            builder.AddAttribute(1, "Name", "field-name");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<Lumeo.Checkbox>(0);
                b.AddAttribute(1, "Name", "box-name");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Equal("box-name", cut.Find("input[type=checkbox]").GetAttribute("name"));
    }

    [Fact]
    public void Bubble_Input_Indeterminate_Posts_As_Unchecked()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(c => c.Name, "opt")
            .Add(c => c.Checked, true)
            .Add(c => c.IsIndeterminate, true));

        Assert.False(cut.Find("input[type=checkbox]").HasAttribute("checked"));
    }
}
