using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.FileUpload;

public class FileUploadTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public FileUploadTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Label_Element()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>();

        Assert.NotNull(cut.Find("label"));
    }

    [Fact]
    public void Renders_Default_Upload_Text_When_No_Label()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>();

        Assert.Contains("Click to upload or drag and drop", cut.Markup);
    }

    [Fact]
    public void Renders_Custom_Label_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Label, "Drop files here"));

        Assert.Contains("Drop files here", cut.Markup);
        Assert.DoesNotContain("Click to upload or drag and drop", cut.Markup);
    }

    [Fact]
    public void Default_Upload_Text_Not_Shown_When_Label_Provided()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Label, "Custom Label"));

        Assert.DoesNotContain("Click to upload or drag and drop", cut.Markup);
    }

    [Fact]
    public void Description_Not_Shown_When_Not_Provided()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>();

        // Description paragraph element should not be present
        var paras = cut.FindAll("p");
        Assert.DoesNotContain(paras, p =>
        {
            var cls = p.GetAttribute("class") ?? "";
            return cls.Contains("text-muted-foreground") && cls.Contains("text-xs");
        });
    }

    [Fact]
    public void Description_Shown_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Description, "SVG, PNG, JPG up to 10MB"));

        Assert.Contains("SVG, PNG, JPG up to 10MB", cut.Markup);
    }

    [Fact]
    public void Has_Input_File_Element()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>();

        // InputFile renders as an input element (type file)
        Assert.NotEmpty(cut.FindAll("input"));
    }

    [Fact]
    public void Multiple_Attribute_Not_Set_By_Default()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>();

        var input = cut.Find("input");
        // multiple should not be present when Multiple = false
        Assert.Null(input.GetAttribute("multiple"));
    }

    [Fact]
    public void Multiple_Attribute_Set_When_True()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Multiple, true));

        var input = cut.Find("input");
        Assert.True(input.HasAttribute("multiple"));
    }

    [Fact]
    public void Accept_Attribute_Forwarded_To_Input()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Accept, "image/*"));

        var input = cut.Find("input");
        Assert.Equal("image/*", input.GetAttribute("accept"));
    }

    [Fact]
    public void Label_Has_Base_Classes()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>();

        var label = cut.Find("label");
        var cls = label.GetAttribute("class") ?? "";
        Assert.Contains("rounded-lg", cls);
        Assert.Contains("border-dashed", cls);
        Assert.Contains("cursor-pointer", cls);
    }

    [Fact]
    public void Custom_Class_Appended_To_Label()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Class, "my-uploader"));

        var label = cut.Find("label");
        Assert.Contains("my-uploader", label.GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forwarded_To_Label()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "file-upload-zone"
            }));

        var label = cut.Find("label");
        Assert.Equal("file-upload-zone", label.GetAttribute("data-testid"));
    }
}
