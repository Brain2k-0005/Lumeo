using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.PasswordInput;

public class PasswordInputTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PasswordInputTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_password_input_by_default()
    {
        var cut = _ctx.Render<L.PasswordInput>();
        var input = cut.Find("input");
        Assert.Equal("password", input.GetAttribute("type"));
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.PasswordInput>(p => p.Add(c => c.Class, "pw-cls"));
        Assert.Contains("pw-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.PasswordInput>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "pw-input" }));
        Assert.Contains("data-testid=\"pw-input\"", cut.Markup);
    }

    [Fact]
    public void Toggle_button_visible_when_show_toggle_true()
    {
        var cut = _ctx.Render<L.PasswordInput>(p => p.Add(c => c.ShowToggle, true));
        // The toggle button should render an svg (eye icon)
        Assert.NotEmpty(cut.FindAll("button"));
    }

    [Fact]
    public void Clicking_toggle_changes_input_type_to_text()
    {
        var cut = _ctx.Render<L.PasswordInput>(p => p
            .Add(c => c.ShowToggle, true)
            .Add(c => c.Value, "MyPassword123"));
        var toggleBtn = cut.Find("button");
        toggleBtn.Click();
        var input = cut.Find("input");
        Assert.Equal("text", input.GetAttribute("type"));
    }
}
