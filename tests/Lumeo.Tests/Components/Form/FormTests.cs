using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Form;

public class FormTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public FormTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    // Helper to render FormField with optional children
    private IRenderedComponent<IComponent> RenderFormField(
        string? name = null,
        string? error = null,
        string? customClass = null,
        RenderFragment? children = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            if (name != null)
                builder.AddAttribute(1, "Name", name);
            if (error != null)
                builder.AddAttribute(2, "Error", error);
            if (customClass != null)
                builder.AddAttribute(3, "Class", customClass);
            builder.AddAttribute(4, "ChildContent", children ?? (RenderFragment)(_ => { }));
            builder.CloseComponent();
        });
    }

    // --- FormField ---

    [Fact]
    public void FormField_Renders_Div()
    {
        var cut = RenderFormField();
        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void FormField_Has_Default_Classes()
    {
        var cut = RenderFormField();
        var cls = cut.Find("div").GetAttribute("class") ?? "";
        Assert.Contains("space-y-2", cls);
    }

    [Fact]
    public void FormField_Custom_Class_Appended()
    {
        var cut = RenderFormField(customClass: "my-field");
        var cls = cut.Find("div").GetAttribute("class") ?? "";
        Assert.Contains("my-field", cls);
        Assert.Contains("space-y-2", cls);
    }

    [Fact]
    public void FormField_Renders_ChildContent()
    {
        var cut = RenderFormField(children: b => b.AddContent(0, "field content"));
        Assert.Contains("field content", cut.Markup);
    }

    // --- FormLabel ---

    [Fact]
    public void FormLabel_Renders_Label_Element()
    {
        var cut = _ctx.Render<L.FormLabel>(p => p.AddChildContent("My Label"));
        Assert.NotNull(cut.Find("label"));
    }

    [Fact]
    public void FormLabel_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.FormLabel>(p => p.AddChildContent("My Label"));
        Assert.Contains("My Label", cut.Find("label").TextContent);
    }

    [Fact]
    public void FormLabel_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.FormLabel>(p => p.AddChildContent(""));
        var cls = cut.Find("label").GetAttribute("class") ?? "";
        Assert.Contains("text-sm", cls);
        Assert.Contains("font-medium", cls);
    }

    [Fact]
    public void FormLabel_No_Error_Class_Without_Field_Context()
    {
        var cut = _ctx.Render<L.FormLabel>(p => p.AddChildContent("Label"));
        var cls = cut.Find("label").GetAttribute("class") ?? "";
        Assert.DoesNotContain("text-destructive", cls);
    }

    [Fact]
    public void FormLabel_Has_Destructive_Class_When_Field_Has_Error()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Error", "Required field");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.FormLabel>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Name")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var label = cut.Find("label");
        Assert.Contains("text-destructive", label.GetAttribute("class") ?? "");
    }

    [Fact]
    public void FormLabel_No_Destructive_Class_When_No_Error()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.FormLabel>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Name")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var label = cut.Find("label");
        Assert.DoesNotContain("text-destructive", label.GetAttribute("class") ?? "");
    }

    [Fact]
    public void FormLabel_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.FormLabel>(p => p
            .Add(c => c.Class, "my-label")
            .AddChildContent("Label"));
        var cls = cut.Find("label").GetAttribute("class") ?? "";
        Assert.Contains("my-label", cls);
    }

    [Fact]
    public void FormLabel_Additional_Attributes_Forwarded()
    {
        var cut = _ctx.Render<L.FormLabel>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["for"] = "my-input" })
            .AddChildContent("Label"));
        Assert.Equal("my-input", cut.Find("label").GetAttribute("for"));
    }

    // --- FormMessage ---

    [Fact]
    public void FormMessage_Not_Rendered_When_No_Error()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.FormMessage>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Empty(cut.FindAll("p"));
    }

    [Fact]
    public void FormMessage_Rendered_When_Error_Exists()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Error", "This field is required");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.FormMessage>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.NotNull(cut.Find("p"));
    }

    [Fact]
    public void FormMessage_Displays_Error_Text()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Error", "This field is required");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.FormMessage>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("This field is required", cut.Find("p").TextContent);
    }

    [Fact]
    public void FormMessage_Has_Destructive_Class()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Error", "Error!");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.FormMessage>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var cls = cut.Find("p").GetAttribute("class") ?? "";
        Assert.Contains("text-destructive", cls);
    }

    [Fact]
    public void FormMessage_Custom_Class_Appended()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Error", "Error!");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.FormMessage>(0);
                b.AddAttribute(1, "Class", "custom-msg");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var cls = cut.Find("p").GetAttribute("class") ?? "";
        Assert.Contains("custom-msg", cls);
    }

    // --- FormItem ---

    [Fact]
    public void FormItem_Renders_Div()
    {
        var cut = _ctx.Render<L.FormItem>(p => p.AddChildContent(""));
        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void FormItem_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.FormItem>(p => p.AddChildContent(""));
        var cls = cut.Find("div").GetAttribute("class") ?? "";
        Assert.Contains("space-y-1", cls);
    }

    [Fact]
    public void FormItem_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.FormItem>(p => p
            .Add(c => c.Class, "my-item")
            .AddChildContent(""));
        var cls = cut.Find("div").GetAttribute("class") ?? "";
        Assert.Contains("my-item", cls);
    }

    [Fact]
    public void FormItem_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.FormItem>(p => p.AddChildContent("item content"));
        Assert.Contains("item content", cut.Markup);
    }

    [Fact]
    public void FormItem_Additional_Attributes_Forwarded()
    {
        var cut = _ctx.Render<L.FormItem>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "my-item" })
            .AddChildContent(""));
        Assert.Equal("my-item", cut.Find("div").GetAttribute("data-testid"));
    }

    // --- FormDescription ---

    [Fact]
    public void FormDescription_Renders_P_Element()
    {
        var cut = _ctx.Render<L.FormDescription>(p => p.AddChildContent("Hint text"));
        Assert.NotNull(cut.Find("p"));
    }

    [Fact]
    public void FormDescription_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.FormDescription>(p => p.AddChildContent("Enter your name"));
        Assert.Contains("Enter your name", cut.Find("p").TextContent);
    }

    [Fact]
    public void FormDescription_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.FormDescription>(p => p.AddChildContent(""));
        var cls = cut.Find("p").GetAttribute("class") ?? "";
        Assert.Contains("text-muted-foreground", cls);
    }

    [Fact]
    public void FormDescription_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.FormDescription>(p => p
            .Add(c => c.Class, "my-desc")
            .AddChildContent(""));
        var cls = cut.Find("p").GetAttribute("class") ?? "";
        Assert.Contains("my-desc", cls);
    }

    [Fact]
    public void FormDescription_Additional_Attributes_Forwarded()
    {
        var cut = _ctx.Render<L.FormDescription>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "my-desc" })
            .AddChildContent(""));
        Assert.Equal("my-desc", cut.Find("p").GetAttribute("data-testid"));
    }

    // --- Full Form Structure ---

    [Fact]
    public void Full_Form_With_Error_Shows_Label_Red_And_Message()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Name", "email");
            builder.AddAttribute(2, "Error", "Invalid email");
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.FormItem>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(item =>
                {
                    item.OpenComponent<L.FormLabel>(0);
                    item.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Email")));
                    item.CloseComponent();

                    item.OpenComponent<L.FormDescription>(1);
                    item.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Enter your email")));
                    item.CloseComponent();

                    item.OpenComponent<L.FormMessage>(2);
                    item.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var label = cut.Find("label");
        Assert.Contains("text-destructive", label.GetAttribute("class") ?? "");
        Assert.Contains("Invalid email", cut.Find("p.text-destructive, p[class*='text-destructive']").TextContent);
    }

    [Fact]
    public void Full_Form_Without_Error_Does_Not_Show_Message()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.FormField>(0);
            builder.AddAttribute(1, "Name", "username");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.FormMessage>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Empty(cut.FindAll("p"));
    }
}
