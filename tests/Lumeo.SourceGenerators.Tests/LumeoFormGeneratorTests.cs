using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Lumeo.SourceGenerators;
using Xunit;

namespace Lumeo.SourceGenerators.Tests;

/// <summary>
/// Drives <see cref="LumeoFormGenerator"/> over hand-written models and asserts on
/// the emitted <c>RenderForm</c> source + the LMF001/LMF002 diagnostics. The
/// generator carries a lot of subtle, bug-fixed behaviour (nullable writeback,
/// enum select, DataType email/password, value-type Required inference) and shipped
/// with no tests; these pin it.
/// </summary>
public class LumeoFormGeneratorTests
{
    // Stub the marker + data-annotation attributes the generator matches by name,
    // so the test compilation is hermetic (no framework annotation package needed).
    private const string Preamble = """
        namespace Lumeo
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class LumeoFormAttribute : System.Attribute
            {
                public bool IncludeSubmitButton { get; set; } = true;
                public string SubmitLabel { get; set; } = "Submit";
                public string? Title { get; set; }
            }
            public sealed class LumeoFormIgnoreAttribute : System.Attribute { }
        }
        namespace System.ComponentModel.DataAnnotations
        {
            public sealed class RequiredAttribute : System.Attribute { }
            public sealed class DisplayAttribute : System.Attribute { public string? Name { get; set; } public string? Description { get; set; } }
            public enum DataType { Custom = 0, EmailAddress = 10, Password = 11 }
            public sealed class DataTypeAttribute : System.Attribute { public DataTypeAttribute(DataType t) { } }
        }
        namespace System.ComponentModel.DataAnnotations.Schema
        {
            public sealed class NotMappedAttribute : System.Attribute { }
        }
        """;

    private static (string Source, ImmutableArray<Diagnostic> Diagnostics) Run(string model)
    {
        var tree = CSharpSyntaxTree.ParseText(Preamble + "\n" + model,
            new CSharpParseOptions(LanguageVersion.Latest));

        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p));

        var compilation = CSharpCompilation.Create("GenTests",
            new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var result = CSharpGeneratorDriver.Create(new LumeoFormGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        var source = string.Concat(result.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString()));

        return (source, result.Diagnostics);
    }

    private static string Gen(string model) => Run(model).Source;

    // ------------------------------------------------------------------ basics

    [Fact]
    public void Emits_A_Partial_RenderForm_For_A_Marked_Class()
    {
        var src = Gen("""
            namespace App { [Lumeo.LumeoForm] public partial class Person { public string Name { get; set; } = ""; } }
            """);

        Assert.Contains("partial class Person", src);
        Assert.Contains("RenderForm(", src);
        Assert.Contains("global::Lumeo.Form<global::App.Person>", src);
        Assert.Contains("global::Lumeo.Input", src);
        Assert.Contains("\"Label\", \"Name\"", src);
    }

    [Fact]
    public void Label_Splits_PascalCase()
    {
        var src = Gen("""
            namespace App { [Lumeo.LumeoForm] public partial class M { public string FirstName { get; set; } = ""; } }
            """);

        Assert.Contains("\"Label\", \"First Name\"", src);
    }

    // ------------------------------------------------------- nullable writeback

    [Fact]
    public void NonNullable_String_Coalesces_Cleared_Input_To_Empty()
    {
        var src = Gen("""
            namespace App { [Lumeo.LumeoForm] public partial class M { public string Name { get; set; } = ""; } }
            """);

        Assert.Contains("= __v ?? string.Empty", src);
    }

    [Fact]
    public void Nullable_String_Writes_The_Value_As_Is()
    {
        var src = Gen("""
            namespace App { [Lumeo.LumeoForm] public partial class M { public string? Name { get; set; } } }
            """);

        Assert.Contains("global::Lumeo.Input", src);          // nullable string still maps to a text input
        Assert.Contains("model.Name = __v))", src);            // value written as-is (nullable)
        Assert.DoesNotContain("__v ?? string.Empty", src);     // no coalesce for a nullable target
    }

    // ------------------------------------------------------------- input kinds

    [Fact]
    public void Email_DataType_Renders_An_Email_Input()
    {
        var src = Gen("""
            namespace App { [Lumeo.LumeoForm] public partial class M {
                [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.EmailAddress)]
                public string Email { get; set; } = ""; } }
            """);

        Assert.Contains("global::Lumeo.Input", src);
        Assert.Contains("\"type\", \"email\"", src);
    }

    [Fact]
    public void Password_DataType_Renders_A_PasswordInput()
    {
        var src = Gen("""
            namespace App { [Lumeo.LumeoForm] public partial class M {
                [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
                public string Secret { get; set; } = ""; } }
            """);

        Assert.Contains("global::Lumeo.PasswordInput", src);
    }

    [Fact]
    public void Numeric_Property_Renders_A_NumberInput()
    {
        var src = Gen("""
            namespace App { [Lumeo.LumeoForm] public partial class M { public int Age { get; set; } } }
            """);

        Assert.Contains("global::Lumeo.NumberInput", src);
        Assert.Contains("(double?)model.Age", src);
    }

    [Fact]
    public void Bool_Property_Renders_A_Checkbox()
    {
        var src = Gen("""
            namespace App { [Lumeo.LumeoForm] public partial class M { public bool Active { get; set; } } }
            """);

        Assert.Contains("global::Lumeo.Checkbox", src);
        Assert.Contains("\"Checked\", model.Active", src);
    }

    [Fact]
    public void NonNullable_Enum_Uses_Plain_ToString_Not_Null_Conditional()
    {
        // Bug A: non-nullable enums are value types — `?.ToString()` is CS0023.
        var src = Gen("""
            namespace App {
                public enum Color { Red, Green }
                [Lumeo.LumeoForm] public partial class M { public Color Color { get; set; } }
            }
            """);

        Assert.Contains("global::Lumeo.Select", src);
        Assert.Contains("model.Color.ToString()", src);
        Assert.DoesNotContain("model.Color?.ToString()", src);
        // One SelectItem per member.
        Assert.Contains("\"Value\", \"Red\"", src);
        Assert.Contains("\"Value\", \"Green\"", src);
    }

    [Fact]
    public void Nullable_Enum_Is_Null_Safe_And_Clears_To_Null()
    {
        var src = Gen("""
            namespace App {
                public enum Color { Red, Green }
                [Lumeo.LumeoForm] public partial class M { public Color? Color { get; set; } }
            }
            """);

        Assert.Contains("model.Color?.ToString() ?? \"\"", src);
        Assert.Contains("model.Color = null;", src);
    }

    // ----------------------------------------------------------- required rule

    [Fact]
    public void NonNullable_Value_Type_Is_Implicitly_Required_With_A_Comment()
    {
        var src = Gen("""
            namespace App { [Lumeo.LumeoForm] public partial class M { public int Age { get; set; } } }
            """);

        Assert.Contains("\"Required\", true", src);
        Assert.Contains("non-nullable value type implies", src);
    }

    [Fact]
    public void Explicit_Required_Does_Not_Emit_The_ValueType_Comment()
    {
        var src = Gen("""
            namespace App { [Lumeo.LumeoForm] public partial class M {
                [System.ComponentModel.DataAnnotations.Required] public string Name { get; set; } = ""; } }
            """);

        Assert.Contains("\"Required\", true", src);
        Assert.DoesNotContain("non-nullable value type implies", src);
    }

    // -------------------------------------------------------------- diagnostics

    [Fact]
    public void ReadOnly_Property_Reports_LMF001_And_Is_Skipped()
    {
        var (src, diags) = Run("""
            namespace App { [Lumeo.LumeoForm] public partial class M { public string Id { get; } = ""; } }
            """);

        Assert.Contains(diags, d => d.Id == "LMF001");
        Assert.DoesNotContain("\"Label\", \"Id\"", src);
    }

    [Fact]
    public void InitOnly_Property_Reports_LMF001()
    {
        var (_, diags) = Run("""
            namespace App { [Lumeo.LumeoForm] public partial class M { public string Id { get; init; } = ""; } }
            """);

        Assert.Contains(diags, d => d.Id == "LMF001");
    }

    [Fact]
    public void Unsupported_Type_Reports_LMF002_And_Is_Skipped()
    {
        var (src, diags) = Run("""
            namespace App { public class Other {}
                [Lumeo.LumeoForm] public partial class M { public Other Thing { get; set; } = new(); } }
            """);

        Assert.Contains(diags, d => d.Id == "LMF002");
        Assert.DoesNotContain("\"Label\", \"Thing\"", src);
    }

    [Fact]
    public void Enumerable_Of_String_Gets_The_TagInput_Hint()
    {
        var (_, diags) = Run("""
            namespace App { [Lumeo.LumeoForm] public partial class M {
                public System.Collections.Generic.List<string> Tags { get; set; } = new(); } }
            """);

        var lmf002 = Assert.Single(diags, d => d.Id == "LMF002");
        Assert.Contains("TagInput", lmf002.GetMessage());
    }

    [Fact]
    public void Ignored_Properties_Are_Silently_Skipped()
    {
        var (src, diags) = Run("""
            namespace App { [Lumeo.LumeoForm] public partial class M {
                public string Keep { get; set; } = "";
                [Lumeo.LumeoFormIgnore] public string Hidden { get; set; } = "";
                [System.ComponentModel.DataAnnotations.Schema.NotMapped] public string Unmapped { get; set; } = ""; } }
            """);

        Assert.Contains("\"Label\", \"Keep\"", src);
        Assert.DoesNotContain("\"Label\", \"Hidden\"", src);
        Assert.DoesNotContain("\"Label\", \"Unmapped\"", src);
        Assert.Empty(diags); // ignore is silent — no LMF00x
    }

    // ----------------------------------------------------------- form options

    [Fact]
    public void Title_Emits_A_Heading()
    {
        var src = Gen("""
            namespace App { [Lumeo.LumeoForm(Title = "Sign up")] public partial class M { public string Name { get; set; } = ""; } }
            """);

        Assert.Contains("\"h3\"", src);
        Assert.Contains("Sign up", src);
    }

    [Fact]
    public void IncludeSubmitButton_False_Omits_The_Button()
    {
        var src = Gen("""
            namespace App { [Lumeo.LumeoForm(IncludeSubmitButton = false)] public partial class M { public string Name { get; set; } = ""; } }
            """);

        Assert.DoesNotContain("global::Lumeo.Button", src);
    }

    [Fact]
    public void Custom_SubmitLabel_Is_Emitted()
    {
        var src = Gen("""
            namespace App { [Lumeo.LumeoForm(SubmitLabel = "Create account")] public partial class M { public string Name { get; set; } = ""; } }
            """);

        Assert.Contains("global::Lumeo.Button", src);
        Assert.Contains("Create account", src);
    }
}
