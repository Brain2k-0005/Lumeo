using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.AlertDialog;

/// <summary>
/// Battle-test regression (n=171, keyboard-a11y): AlertDialogContent used to
/// emit <c>aria-describedby="@Context.DescriptionId"</c> unconditionally, but
/// the matching id only exists when an &lt;AlertDialogDescription&gt; is
/// rendered. With no description the attribute pointed at a non-existent element
/// (a dangling IDREF that confuses assistive tech).
///
/// The fix wires aria-describedby only when a description registers with the
/// context. These tests assert the OBSERVABLE markup: the attribute is absent
/// without a description and present (matching the &lt;p&gt; id) with one.
/// </summary>
public class AlertDialogDescribedByTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AlertDialogDescribedByTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void No_Description_Omits_Aria_Describedby()
    {
        // Open content with only plain child text — no AlertDialogDescription.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.AlertDialog>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var dialog = cut.Find("[role='alertdialog']");
        // Without the fix this attribute is present (pointing at a missing id).
        Assert.False(dialog.HasAttribute("aria-describedby"));
    }

    [Fact]
    public void With_Description_Aria_Describedby_Matches_The_Description_Id()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.AlertDialog>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.AlertDialogDescription>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(d => d.AddContent(0, "This action cannot be undone.")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var dialog = cut.Find("[role='alertdialog']");
        var describedBy = dialog.GetAttribute("aria-describedby");

        Assert.False(string.IsNullOrEmpty(describedBy));

        // The IDREF must resolve to the rendered description <p> (not dangle).
        var description = cut.Find("p");
        Assert.Equal(description.GetAttribute("id"), describedBy);
    }
}
