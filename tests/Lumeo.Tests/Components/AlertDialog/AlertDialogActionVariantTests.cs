using Microsoft.AspNetCore.Components;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.AlertDialog;

/// <summary>
/// Field report 4.13. AlertDialogAction rendered the destructive variant unconditionally and
/// had no parameter to change it, so a "transfer the master date again?" confirmation got
/// the same red button as "delete?". shadcn's AlertDialogAction is buttonVariants() at its
/// default, i.e. primary; destructive is the caller's choice.
/// </summary>
public class AlertDialogActionVariantTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AlertDialogActionVariantTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private string ActionClass(L.Button.ButtonVariant? variant)
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.AlertDialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.AlertDialogAction>(0);
                    if (variant is { } v)
                        inner.AddAttribute(1, "Variant", v);
                    inner.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Confirm")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        return cut.Find("button").GetAttribute("class")!;
    }

    [Fact]
    public void Default_Is_Primary_Not_Destructive()
    {
        var cls = ActionClass(null);

        Assert.Contains("bg-primary", cls);
        Assert.DoesNotContain("bg-destructive", cls);
    }

    [Fact]
    public void Destructive_Is_An_Opt_In()
    {
        var cls = ActionClass(L.Button.ButtonVariant.Destructive);

        Assert.Contains("bg-destructive", cls);
        Assert.DoesNotContain("bg-primary", cls);
    }

    [Theory]
    [InlineData(L.Button.ButtonVariant.Outline, "border-input")]
    [InlineData(L.Button.ButtonVariant.Secondary, "bg-secondary")]
    [InlineData(L.Button.ButtonVariant.Ghost, "hover:bg-accent")]
    [InlineData(L.Button.ButtonVariant.Link, "underline-offset-4")]
    public void Every_Button_Variant_Is_Available(L.Button.ButtonVariant variant, string marker)
    {
        // The table mirrors Button's, so a confirmation can look like any button the
        // surrounding UI already uses.
        Assert.Contains(marker, ActionClass(variant));
    }
}
