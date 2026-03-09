using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.Filter;

public class FilterPillTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FilterPillTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Label_And_Value()
    {
        var cut = _ctx.Render<FilterPill>(p => p
            .Add(c => c.Label, "Status")
            .Add(c => c.Value, "Active"));

        Assert.Contains("Status", cut.Markup);
        Assert.Contains("Active", cut.Markup);
    }

    [Fact]
    public void Renders_Dismiss_Button()
    {
        var cut = _ctx.Render<FilterPill>(p => p
            .Add(c => c.Label, "Priority")
            .Add(c => c.Value, "High"));

        var button = cut.Find("button");
        Assert.NotNull(button);
    }

    [Fact]
    public void Dismiss_Button_Triggers_OnDismiss()
    {
        var dismissed = false;
        var cut = _ctx.Render<FilterPill>(p => p
            .Add(c => c.Label, "Status")
            .Add(c => c.Value, "Active")
            .Add(c => c.OnDismiss, EventCallback.Factory.Create(this, () => { dismissed = true; })));

        cut.Find("button").Click();

        Assert.True(dismissed);
    }

    [Fact]
    public void Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<FilterPill>(p => p
            .Add(c => c.Label, "Status")
            .Add(c => c.Value, "Active")
            .Add(c => c.Class, "my-custom-class"));

        Assert.Contains("my-custom-class", cut.Markup);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<FilterPill>(p => p
            .Add(c => c.Label, "Status")
            .Add(c => c.Value, "Active")
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "filter-pill-status"
            }));

        Assert.Contains("data-testid=\"filter-pill-status\"", cut.Markup);
    }

    [Fact]
    public void Empty_Label_And_Value_Renders_Without_Error()
    {
        var cut = _ctx.Render<FilterPill>(p => p
            .Add(c => c.Label, "")
            .Add(c => c.Value, ""));

        Assert.NotNull(cut.Find("button"));
    }
}
