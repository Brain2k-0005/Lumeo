using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Form;

/// <summary>
/// G12 — Base UI Field-style a11y wiring: FormField generates a control id its
/// child input adopts (so &lt;label for&gt; resolves to it) and wires the input's
/// aria-describedby to the rendered help OR error text.
/// </summary>
public class FormFieldA11yWiringTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public FormFieldA11yWiringTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderFieldWithInput(string? label = null, string? helpText = null, string? error = null)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            if (label != null) builder.AddAttribute(1, "Label", label);
            if (helpText != null) builder.AddAttribute(2, "HelpText", helpText);
            if (error != null) builder.AddAttribute(3, "Error", error);
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.Input>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Label_For_Matches_The_Input_Id()
    {
        var cut = RenderFieldWithInput(label: "Email");
        var inputId = cut.Find("input").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(inputId));
        Assert.Equal(inputId, cut.Find("label").GetAttribute("for"));
    }

    [Fact]
    public void HelpText_Is_Wired_To_Input_Via_AriaDescribedby()
    {
        var cut = RenderFieldWithInput(helpText: "Use your work email");
        var helpId = cut.Find("p.text-muted-foreground").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(helpId));
        Assert.Equal(helpId, cut.Find("input").GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Error_Is_Wired_To_Input_Via_AriaDescribedby_And_AriaInvalid()
    {
        var cut = RenderFieldWithInput(error: "Required");
        var errorId = cut.Find("p[role='alert']").GetAttribute("id");
        var input = cut.Find("input");
        Assert.False(string.IsNullOrEmpty(errorId));
        Assert.Equal(errorId, input.GetAttribute("aria-describedby"));
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
    }

    [Fact]
    public void Error_Takes_Precedence_Over_HelpText_For_Describedby()
    {
        var cut = RenderFieldWithInput(helpText: "hint", error: "bad");
        var errorId = cut.Find("p[role='alert']").GetAttribute("id");
        Assert.Equal(errorId, cut.Find("input").GetAttribute("aria-describedby"));
        Assert.Empty(cut.FindAll("p.text-muted-foreground")); // help is replaced by the error
    }

    [Fact]
    public void Plain_Input_Outside_A_FormField_Has_No_Generated_Id_Or_Describedby()
    {
        // Byte-identity guard: the wiring is FormField-scoped — a standalone Input
        // must not gain an id="" or aria-describedby from the new plumbing.
        var cut = _ctx.Render<L.Input>();
        var input = cut.Find("input");
        Assert.False(input.HasAttribute("id"));
        Assert.False(input.HasAttribute("aria-describedby"));
    }
}
