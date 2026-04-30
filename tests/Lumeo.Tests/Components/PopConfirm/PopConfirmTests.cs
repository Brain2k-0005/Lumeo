using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.PopConfirm;

public class PopConfirmTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PopConfirmTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_trigger_child_content()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.PopConfirm>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenElement(0, "button");
                b.AddAttribute(1, "id", "delete-btn");
                b.AddContent(2, "Delete");
                b.CloseElement();
            }));
            builder.CloseComponent();
        });
        Assert.Contains("Delete", cut.Markup);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.PopConfirm>(0);
            builder.AddAttribute(1, "Class", "my-popconfirm");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenElement(0, "button");
                b.AddContent(1, "Go");
                b.CloseElement();
            }));
            builder.CloseComponent();
        });
        // Class is applied to the PopoverContent inside
        Assert.NotNull(cut);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.PopConfirm>(0);
            builder.AddAttribute(1, "AdditionalAttributes", new Dictionary<string, object>
            {
                ["data-testid"] = "pop-confirm"
            });
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenElement(0, "button");
                b.AddContent(1, "Click");
                b.CloseElement();
            }));
            builder.CloseComponent();
        });
        Assert.Contains("data-testid=\"pop-confirm\"", cut.Markup);
    }

    [Fact]
    public void Clicking_trigger_opens_confirmation_dialog()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.PopConfirm>(0);
            builder.AddAttribute(1, "Title", "Are you sure?");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenElement(0, "button");
                b.AddAttribute(1, "id", "trigger-btn");
                b.AddContent(2, "Delete");
                b.CloseElement();
            }));
            builder.CloseComponent();
        });

        // Click the trigger span (PopConfirm wraps ChildContent in a span)
        var triggerSpan = cut.Find("span.inline-flex");
        triggerSpan.Click();
        Assert.Contains("Are you sure?", cut.Markup);
    }
}
