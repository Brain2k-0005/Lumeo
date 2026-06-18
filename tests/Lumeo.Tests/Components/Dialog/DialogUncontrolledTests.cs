using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Dialog;

/// <summary>
/// #215 — Dialog gains uncontrolled support via DefaultOpen (Radix
/// <c>defaultOpen</c>). When the consumer does not bind Open, the dialog owns its
/// own open state, seeded from DefaultOpen, and can self-close on Escape /
/// backdrop / close button without a controlling parent.
/// </summary>
public class DialogUncontrolledTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DialogUncontrolledTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.Dialog> RenderUncontrolled(bool defaultOpen)
    {
        return _ctx.Render<L.Dialog>(p => p
            .Add(d => d.DefaultOpen, defaultOpen)
            .Add(d => d.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body text")));
                b.CloseComponent();
            })));
    }

    [Fact]
    public void DefaultOpen_True_Renders_Open_Without_Binding_Open()
    {
        var cut = RenderUncontrolled(defaultOpen: true);
        Assert.NotEmpty(cut.FindAll("[role='dialog']"));
        Assert.Contains("Body text", cut.Markup);
    }

    [Fact]
    public void DefaultOpen_False_Renders_Closed()
    {
        var cut = RenderUncontrolled(defaultOpen: false);
        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void Uncontrolled_Dialog_Self_Closes_On_Escape()
    {
        var cut = RenderUncontrolled(defaultOpen: true);
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        // No controlling parent — the dialog must hide itself.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='dialog']")));
    }

    [Fact]
    public void Controlled_Open_Still_Wins_Over_DefaultOpen()
    {
        // When Open is bound it is authoritative; DefaultOpen is ignored.
        var cut = _ctx.Render<L.Dialog>(p => p
            .Add(d => d.Open, false)
            .Add(d => d.DefaultOpen, true)
            .Add(d => d.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body text")));
                b.CloseComponent();
            })));

        Assert.Empty(cut.FindAll("[role='dialog']"));
    }
}
