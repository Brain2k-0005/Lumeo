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

    // --- #230 keyboard operability + ARIA ---

    private IRenderedComponent<L.PopConfirm> RenderPopConfirm(string? description = null)
        => _ctx.Render<L.PopConfirm>(p =>
        {
            p.Add(c => c.Title, "Are you sure?");
            if (description is not null) p.Add(c => c.Description, description);
            p.Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenElement(0, "span");
                b.AddContent(1, "Delete");
                b.CloseElement();
            }));
        });

    [Fact]
    public void Trigger_Is_Keyboard_Operable()
    {
        var cut = RenderPopConfirm();
        var trigger = cut.Find("span[role='button']");
        Assert.Equal("0", trigger.GetAttribute("tabindex"));
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void Trigger_Keydown_Opens_Dialog(string key)
    {
        var cut = RenderPopConfirm();
        cut.Find("span[role='button']").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = key });
        Assert.Contains("Are you sure?", cut.Markup);
    }

    [Fact]
    public void Reclicking_Trigger_Toggles_Closed()
    {
        var cut = RenderPopConfirm();
        var trigger = cut.Find("span[role='button']");

        trigger.Click();
        Assert.Contains("Are you sure?", cut.Markup);

        cut.Find("span[role='button']").Click();
        Assert.DoesNotContain("Are you sure?", cut.Markup);
    }

    [Fact]
    public void Confirm_Surface_Has_AlertDialog_Role_And_Labelling()
    {
        var cut = RenderPopConfirm(description: "This cannot be undone.");
        cut.Find("span[role='button']").Click();

        var dialog = cut.Find("[role='alertdialog']");
        var labelledBy = dialog.GetAttribute("aria-labelledby");
        var describedBy = dialog.GetAttribute("aria-describedby");
        Assert.False(string.IsNullOrEmpty(labelledBy));
        Assert.False(string.IsNullOrEmpty(describedBy));
        // The referenced elements must exist.
        Assert.NotNull(cut.Find($"#{labelledBy}"));
        Assert.NotNull(cut.Find($"#{describedBy}"));
    }
}
