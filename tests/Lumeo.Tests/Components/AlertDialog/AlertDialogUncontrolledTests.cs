using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.AlertDialog;

/// <summary>
/// #216 — AlertDialog gains uncontrolled support via DefaultOpen (Radix
/// <c>defaultOpen</c>), closing the last controlled-vs-uncontrolled parity gap
/// (cancel-focus / trap / restore / Esc were already in place).
/// </summary>
public class AlertDialogUncontrolledTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AlertDialogUncontrolledTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.AlertDialog> RenderUncontrolled(bool defaultOpen)
    {
        return _ctx.Render<L.AlertDialog>(p => p
            .Add(d => d.DefaultOpen, defaultOpen)
            .Add(d => d.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Are you sure?")));
                b.CloseComponent();
            })));
    }

    [Fact]
    public void DefaultOpen_True_Renders_Open()
    {
        var cut = RenderUncontrolled(defaultOpen: true);
        Assert.NotEmpty(cut.FindAll("[role='alertdialog']"));
    }

    [Fact]
    public void DefaultOpen_False_Renders_Closed()
    {
        var cut = RenderUncontrolled(defaultOpen: false);
        Assert.Empty(cut.FindAll("[role='alertdialog']"));
    }

    [Fact]
    public void Uncontrolled_Self_Closes_On_Escape()
    {
        var cut = RenderUncontrolled(defaultOpen: true);
        cut.Find("[role='alertdialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='alertdialog']")));
    }
}
