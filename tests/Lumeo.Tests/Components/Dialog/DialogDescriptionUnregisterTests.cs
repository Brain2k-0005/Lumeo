using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Dialog;

/// <summary>
/// Codex P2 — DialogDescriptionRegistry was a one-way-latching bool: RegisterDescription set
/// HasDescription=true and nothing ever reset it. If a consumer conditionally renders
/// &lt;DialogDescription&gt; and later removes it while the dialog stays mounted, DialogContent's
/// aria-describedby kept pointing at an id no longer in the DOM (a dangling IDREF). The registry is
/// now a count, and DialogDescription un-registers via IDisposable on removal.
/// </summary>
public class DialogDescriptionUnregisterTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DialogDescriptionUnregisterTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Toggles whether <DialogDescription> renders, so the test can remove it after the dialog mounted.
    private sealed class DialogHost : ComponentBase
    {
        [Parameter] public bool ShowDescription { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<L.Dialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    if (ShowDescription)
                    {
                        inner.OpenComponent<L.DialogDescription>(0);
                        inner.AddAttribute(1, "ChildContent", (RenderFragment)(d => d.AddContent(0, "My Description")));
                        inner.CloseComponent();
                    }
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    [Fact]
    public void Removing_DialogDescription_Clears_Aria_Describedby()
    {
        var cut = _ctx.Render<DialogHost>(p => p.Add(x => x.ShowDescription, true));

        var content = cut.Find("[role='dialog']");
        Assert.False(string.IsNullOrEmpty(content.GetAttribute("aria-describedby")));

        // Consumer removes the description while the dialog stays mounted.
        cut.Render(p => p.Add(x => x.ShowDescription, false));

        content = cut.Find("[role='dialog']");
        // Without the fix this still points at the no-longer-rendered description's id.
        Assert.True(string.IsNullOrEmpty(content.GetAttribute("aria-describedby")));
    }

    [Fact]
    public void ReAdding_DialogDescription_After_Removal_Restores_Aria_Describedby()
    {
        // Guard against over-correction: the count must also recover when a description is added back.
        var cut = _ctx.Render<DialogHost>(p => p.Add(x => x.ShowDescription, true));
        cut.Render(p => p.Add(x => x.ShowDescription, false));
        cut.Render(p => p.Add(x => x.ShowDescription, true));

        var content = cut.Find("[role='dialog']");
        Assert.False(string.IsNullOrEmpty(content.GetAttribute("aria-describedby")));
    }
}
