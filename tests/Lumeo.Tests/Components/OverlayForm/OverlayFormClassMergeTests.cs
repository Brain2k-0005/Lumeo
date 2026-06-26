using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.OverlayForm;

/// <summary>
/// Edge-data regression for triage #46: a caller-supplied <c>class</c> — whether via the
/// typed <c>Class</c> parameter OR splatted through <c>AdditionalAttributes</c> — must
/// COMPOSE with OverlayForm's baked-in structural classes (<c>flex flex-col h-full
/// min-h-0</c>) rather than CLOBBER them. The structural classes are the component's whole
/// reason to exist (the independent-scroll + sticky-footer shape), so if the
/// <c>@attributes</c> splat overwrote the explicit <c>class</c> attribute, every consumer
/// that passed a layout class would silently lose the shape. Mirrors OverlayFormTests.cs.
/// </summary>
public class OverlayFormClassMergeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OverlayFormClassMergeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class EditModel
    {
        public string? Name { get; set; }
    }

    [Fact]
    public void Splatted_Class_Does_Not_Clobber_Structural_Classes_On_EditForm()
    {
        // Caller splats a `class` through AdditionalAttributes. Before the fix the
        // @attributes splat (rendered after class="@CssClass") overwrote the form's
        // class attribute, wiping the baked-in structural classes. After the fix the
        // splatted class is routed through Cx.Merge and composes with them.
        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.Model, new EditModel())
            .Add(o => o.AdditionalAttributes, new Dictionary<string, object>
            {
                ["class"] = "my-custom-class"
            }));

        var form = cut.Find("form");
        var cls = form.GetAttribute("class") ?? string.Empty;

        // Structural classes survive...
        Assert.Contains("flex", cls);
        Assert.Contains("flex-col", cls);
        Assert.Contains("min-h-0", cls);
        // ...and the caller's class is composed in, not dropped.
        Assert.Contains("my-custom-class", cls);

        // The splat must not also emit a second class attribute (it was merged in).
        Assert.DoesNotContain("class=\"my-custom-class\"", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Typed_Class_Parameter_Composes_With_Structural_Classes_On_EditForm()
    {
        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.Model, new EditModel())
            .Add(o => o.Class, "typed-class"));

        var cls = cut.Find("form").GetAttribute("class") ?? string.Empty;

        Assert.Contains("flex-col", cls);
        Assert.Contains("min-h-0", cls);
        Assert.Contains("typed-class", cls);
    }

    [Fact]
    public void Splatted_Class_Does_Not_Clobber_Structural_Classes_On_Diagnostic_Shell()
    {
        // Same hazard on the no-Model diagnostic root (line 39), which also carries
        // class="@CssClass" @attributes=... ordering.
        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.AdditionalAttributes, new Dictionary<string, object>
            {
                ["class"] = "shell-custom"
            }));

        // The diagnostic alert renders; locate its parent root div.
        var alert = cut.Find("[role='alert']");
        var root = alert.ParentElement!;
        var cls = root.GetAttribute("class") ?? string.Empty;

        Assert.Contains("flex-col", cls);
        Assert.Contains("min-h-0", cls);
        Assert.Contains("shell-custom", cls);
    }

    [Fact]
    public void Non_Class_Splatted_Attributes_Are_Still_Forwarded()
    {
        // Stripping `class` from the splat must not drop the OTHER splatted attributes.
        var cut = _ctx.Render<L.OverlayForm>(p => p
            .Add(o => o.Model, new EditModel())
            .Add(o => o.AdditionalAttributes, new Dictionary<string, object>
            {
                ["class"] = "x-class",
                ["data-testid"] = "of-form"
            }));

        var form = cut.Find("form");
        Assert.Equal("of-form", form.GetAttribute("data-testid"));
        Assert.Contains("x-class", form.GetAttribute("class") ?? string.Empty);
    }
}
