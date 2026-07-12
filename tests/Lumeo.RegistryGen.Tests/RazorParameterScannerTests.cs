using Xunit;
using Lumeo.RegistryGen;

namespace Lumeo.RegistryGen.Tests;

public class RazorParameterScannerTests
{
    private static string WriteTempRazor(string contents)
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumeo-rg-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "TestComponent.razor");
        File.WriteAllText(path, contents);
        return path;
    }

    [Fact]
    public void Extracts_basic_parameters_with_defaults()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
<button>@ChildContent</button>
@code {
    [Parameter] public string? Class { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public int Count { get; set; } = 42;
    [Parameter] public RenderFragment? ChildContent { get; set; }
}");
        var s = RazorParameterScanner.Scan(path);
        Assert.False(s.ParseFailed, s.ParseError);
        Assert.Equal("Lumeo", s.Namespace);
        Assert.Equal(4, s.Parameters.Length);
        var count = s.Parameters.Single(p => p.Name == "Count");
        Assert.Equal("int", count.Type);
        Assert.Equal("42", count.Default);
        Assert.False(count.IsCascading);
    }

    [Fact]
    public void Detects_capture_unmatched_and_cascading()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    [CascadingParameter] public string? Theme { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }
}");
        var s = RazorParameterScanner.Scan(path);
        Assert.False(s.ParseFailed);
        var theme = s.Parameters.Single(p => p.Name == "Theme");
        Assert.True(theme.IsCascading);
        var add = s.Parameters.Single(p => p.Name == "AdditionalAttributes");
        Assert.True(add.CaptureUnmatched);
    }

    [Fact]
    public void Surfaces_event_callbacks_separately()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
}");
        var s = RazorParameterScanner.Scan(path);
        Assert.Equal(2, s.Events.Length);
        Assert.Contains(s.Events, e => e.Name == "OpenChanged" && e.Type.StartsWith("EventCallback"));
        Assert.Contains(s.Events, e => e.Name == "OnClick");
    }

    [Fact]
    public void Captures_nested_enums_and_records()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    [Parameter] public Variant V { get; set; }

    public enum Variant { Default, Outline, Ghost }

    public record Ctx(string Id, bool IsOpen);
}");
        var s = RazorParameterScanner.Scan(path);
        Assert.Single(s.Enums);
        Assert.Equal("Variant", s.Enums[0].Name);
        Assert.Equal(new[] { "Default", "Outline", "Ghost" }, s.Enums[0].Values);
        Assert.Single(s.Records);
        Assert.Equal("Ctx", s.Records[0].Name);
        Assert.Contains("string Id", s.Records[0].Signature);
        // The [Parameter] has no initializer — default(Variant) is its zero-valued member.
        Assert.Equal("Default", s.Parameters.Single(p => p.Name == "V").Default);
    }

    [Fact]
    public void Qualified_enum_type_resolves_implicit_default_from_sibling_file()
    {
        // Mirrors DataTableSortableHeader.SortDirection: a sub-component parameter typed
        // through its PARENT's nested enum, qualified as "Owner<object>.Status", with no
        // enum declared anywhere in the file being scanned itself. Without siblingEnumMembers
        // this falls through to null/"—" (Codex P2, PR #358 round 3).
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    [Parameter] public Owner<object>.Status CurrentStatus { get; set; }
}");
        var siblingEnums = new Dictionary<string, string[]>
        {
            ["Status"] = new[] { "None", "Active", "Done" },
        };

        var withoutSiblings = RazorParameterScanner.Scan(path);
        Assert.Null(withoutSiblings.Parameters.Single(p => p.Name == "CurrentStatus").Default);

        var withSiblings = RazorParameterScanner.Scan(path, siblingEnums);
        Assert.Equal("None", withSiblings.Parameters.Single(p => p.Name == "CurrentStatus").Default);
    }

    [Fact]
    public void Local_enum_wins_over_a_same_named_sibling_enum_on_name_clash()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    [Parameter] public Status CurrentStatus { get; set; }

    public enum Status { Local, Other }
}");
        // A differently-ordered sibling enum sharing the same simple name must NOT override
        // the file's own declaration — local always wins.
        var siblingEnums = new Dictionary<string, string[]>
        {
            ["Status"] = new[] { "Remote", "Other" },
        };

        var s = RazorParameterScanner.Scan(path, siblingEnums);
        Assert.Equal("Local", s.Parameters.Single(p => p.Name == "CurrentStatus").Default);
    }

    [Fact]
    public void Obsolete_alias_parameter_with_no_xml_summary_uses_the_Obsolete_message()
    {
        // Mirrors ContextMenu.IsOpen: [Obsolete("...")] forwards to a live parameter and
        // carries no /// <summary> of its own. The removed hand-written table showed the
        // Obsolete message as the row description; PropsTable must not render it blank
        // (Codex P2, PR #358 round 3).
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    [Parameter] public bool Open { get; set; }

    [Obsolete(""Use Open instead. IsOpen will be removed in a future release."")]
    [Parameter] public bool IsOpen { get => Open; set => Open = value; }
}");
        var s = RazorParameterScanner.Scan(path);
        var isOpen = s.Parameters.Single(p => p.Name == "IsOpen");
        Assert.Equal("Use Open instead. IsOpen will be removed in a future release.", isOpen.Description);
    }

    [Fact]
    public void Xml_summary_wins_over_Obsolete_message_when_both_are_present()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    /// <summary>The authored summary.</summary>
    [Obsolete(""The obsolete message."")]
    [Parameter] public bool Legacy { get; set; }
}");
        var s = RazorParameterScanner.Scan(path);
        Assert.Equal("The authored summary.", s.Parameters.Single(p => p.Name == "Legacy").Description);
    }

    [Fact]
    public void CollectPublicEnums_returns_only_public_enums_from_a_file()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    public enum Status { None, Active }
    private enum Hidden { A, B }
}");
        var enums = RazorParameterScanner.CollectPublicEnums(path);
        Assert.True(enums.ContainsKey("Status"));
        Assert.Equal(new[] { "None", "Active" }, enums["Status"]);
        Assert.False(enums.ContainsKey("Hidden"));
    }

    [Fact]
    public void Excludes_private_and_internal_nested_types_from_the_api()
    {
        // The public API surface must expose only PUBLIC nested types. Private/internal helper
        // enums and records (e.g. TreeView's private PendingCarry carry) are implementation
        // detail and must never leak into components-api.json.
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    [Parameter] public Variant V { get; set; }

    public enum Variant { Default, Outline }
    private enum InternalMode { A, B }

    public record Ctx(string Id);
    private sealed record PendingCarry(int Index);
}");
        var s = RazorParameterScanner.Scan(path);

        var variant = Assert.Single(s.Enums);
        Assert.Equal("Variant", variant.Name);
        Assert.DoesNotContain(s.Enums, e => e.Name == "InternalMode");

        var ctx = Assert.Single(s.Records);
        Assert.Equal("Ctx", ctx.Name);
        Assert.DoesNotContain(s.Records, r => r.Name == "PendingCarry");
    }

    [Fact]
    public void Extracts_xml_doc_summary_into_description()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
@code {
    /// <summary>Visual variant of the button.</summary>
    [Parameter] public string Variant { get; set; } = ""Default"";
}");
        var s = RazorParameterScanner.Scan(path);
        var v = Assert.Single(s.Parameters);
        Assert.Equal("Visual variant of the button.", v.Description);
    }

    [Fact]
    public void Inherits_and_implements_directives_are_captured()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
@inherits LayoutComponentBase
@implements IAsyncDisposable
@implements IDisposable
@code {
    [Parameter] public string? Class { get; set; }
}");
        var s = RazorParameterScanner.Scan(path);
        Assert.Equal("LayoutComponentBase", s.InheritsFrom);
        Assert.Contains("IAsyncDisposable", s.Implements);
        Assert.Contains("IDisposable", s.Implements);
    }

    [Fact]
    public void Markup_only_razor_is_not_a_failure()
    {
        var path = WriteTempRazor(@"@namespace Lumeo
<div>just markup</div>");
        var s = RazorParameterScanner.Scan(path);
        Assert.False(s.ParseFailed);
        Assert.Empty(s.Parameters);
    }
}
