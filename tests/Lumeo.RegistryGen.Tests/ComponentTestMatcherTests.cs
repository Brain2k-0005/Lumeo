using Xunit;

namespace Lumeo.RegistryGen.Tests;

/// <summary>
/// Focused unit-test matrix for <see cref="ComponentTestMatcher"/> — pins down
/// every known false-positive/false-negative case surfaced across the review
/// waves that used to patch this matching logic piecemeal (comment stripping,
/// the LINQ <c>.Select(...)</c> collision, suffixed test-class ids, the
/// "Input"/"InputMask" sibling-prefix collision, and the "Text"/.TextContent
/// member-access collision), so a future change can't silently reintroduce any
/// of them.
/// </summary>
public class ComponentTestMatcherTests
{
    private static readonly HashSet<string> KnownNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sheet", "Select", "Input", "InputMask", "Text", "TextReveal", "Textarea", "DataGrid", "List",
    };

    // ----- 1. dedicated folder ownership -----

    [Fact]
    public void Folder_ownership_counts_with_zero_content_signal()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Components/Sheet/WhateverTests.cs",
            "namespace Lumeo.Tests.Components.Sheet;\npublic class WhateverTests { }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void Folder_ownership_is_case_insensitive()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Components/sheet/WhateverTests.cs",
            "public class WhateverTests { }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void Folder_ownership_does_not_leak_to_sibling_component()
    {
        // A file that lives in Select's own folder must not also be counted as
        // Input coverage just because Input happens to have real content
        // references elsewhere in the same repo.
        var ok = ComponentTestMatcher.IsCoverage(
            "Input", "tests/Lumeo.Tests/Components/Select/SelectTests.cs",
            "public class SelectTests { var x = L.Select(new()); }",
            KnownNames);

        Assert.False(ok);
    }

    // ----- 2a. real type references -----

    [Fact]
    public void Generic_render_call_counts_as_real_reference()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/SomeOtherTests.cs",
            "var cut = ctx.Render<Lumeo.Sheet>(p => p.Add(x => x.Open, true));",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void Namespace_alias_reference_counts_as_real_reference()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/SomeOtherTests.cs",
            "using L = Lumeo;\nvar cut = ctx.Render<L.Sheet>();",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void Bare_constructor_reference_counts_as_real_reference()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Select", "tests/Lumeo.Tests/Misc/SomeOtherTests.cs",
            "var s = new Select();",
            KnownNames);

        Assert.True(ok);
    }

    // ----- LINQ .Select(...) collision (regressed once already: PR #357 fix, PR #361-round-1 dropped it) -----

    [Fact]
    public void Linq_select_call_on_unrelated_receiver_does_not_count()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Select", "tests/Lumeo.Tests/Misc/UnrelatedTests.cs",
            "public class UnrelatedTests { void M() { var ys = xs.Select(x => x.Id).ToList(); } }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void Linq_select_call_via_lumeo_alias_still_counts()
    {
        // L.Select(...) — a real static/extension call on the Lumeo namespace
        // alias — must still count; only OTHER receivers are excluded.
        var ok = ComponentTestMatcher.IsCoverage(
            "Select", "tests/Lumeo.Tests/Misc/UnrelatedTests.cs",
            "using L = Lumeo;\nvar x = L.Select(opts);",
            KnownNames);

        Assert.True(ok);
    }

    // ----- Text / .TextContent member-access collision (new PR #361 finding) -----

    [Fact]
    public void Member_access_property_does_not_count()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Text", "tests/Lumeo.Tests/Components/Avatar/AvatarTests.cs",
            "public class AvatarTests { void M() { Assert.Equal(\"Hi\", cut.Find(\"span\").TextContent); } }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void Member_access_on_lumeo_alias_still_counts()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Text", "tests/Lumeo.Tests/Misc/UnrelatedTests.cs",
            "using L = Lumeo;\nvar t = L.Text;",
            KnownNames);

        Assert.True(ok);
    }

    // ----- Input / InputFile / InputMask prefix collision (new PR #361 finding) -----

    [Fact]
    public void Prefix_of_unrelated_bcl_type_does_not_count()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Input", "tests/Lumeo.Tests/Components/FileUpload/FileUploadTests.cs",
            "public class FileUploadTests { void M(InputFileChangeEventArgs e) { } }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void Filename_prefix_of_longer_sibling_component_does_not_count_for_shorter_name()
    {
        // "InputMask" is itself a known component — a test file named after IT
        // must not also be attributed to the shorter, unrelated "Input".
        var ok = ComponentTestMatcher.IsCoverage(
            "Input", "tests/Lumeo.Tests.E2E/Smokes/InputMaskDisplayTests.cs",
            "public class InputMaskDisplayTests { }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void Filename_prefix_of_longer_sibling_component_counts_for_the_longer_name()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "InputMask", "tests/Lumeo.Tests.E2E/Smokes/InputMaskDisplayTests.cs",
            "public class InputMaskDisplayTests { }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void Exact_bare_word_still_counts_for_the_shorter_name()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Input", "tests/Lumeo.Tests/Misc/SomeOtherTests.cs",
            "var i = new Input();",
            KnownNames);

        Assert.True(ok);
    }

    // ----- suffixed test-class ids (PR #361-wave-7 fix, kept — but now scoped correctly) -----

    [Fact]
    public void Suffixed_test_class_name_counts_as_coverage()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "DataGrid", "tests/Lumeo.Tests.E2E/Smokes/DataGridSmokeTests.cs",
            "namespace Lumeo.Tests.E2E.Smokes;\npublic class DataGridSmokeTests : PlaywrightTestBase { }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void Suffixed_test_file_name_counts_even_without_matching_class_name()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Select", "tests/Lumeo.Tests.E2E/Smokes/SelectInteractionTests.cs",
            "namespace Lumeo.Tests.E2E.Smokes;\npublic class SelectInteractionTests : PlaywrightTestBase { }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void Lowercase_continuation_does_not_count_as_a_suffixed_match()
    {
        // "Selectable" — no PascalCase segment boundary after "Select".
        var ok = ComponentTestMatcher.IsCoverage(
            "Select", "tests/Lumeo.Tests/Misc/SelectableWidgetTests.cs",
            "public class SelectableWidgetTests { }",
            KnownNames);

        Assert.False(ok);
    }

    // ----- bare BCL-generic-collection collision (PR #361-round-2 finding: "List") -----

    [Fact]
    public void Bare_bcl_generic_collection_declaration_does_not_count()
    {
        // List<bool> field — "List" the component colliding with
        // System.Collections.Generic.List<T>, used as a plain field type.
        var ok = ComponentTestMatcher.IsCoverage(
            "List", "tests/Lumeo.Tests/Components/ConsentBanner/ConsentBannerSlideEndTests.cs",
            "public class Interop { public List<bool> ElementPresentAtInvocation { get; } = new(); }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void Generic_component_as_nested_type_argument_still_counts()
    {
        // Render<DataGrid<Person>>(...) — "DataGrid" sits INSIDE Render's own
        // argument list; the bare-generic-declaration exclusion must not
        // swallow this legitimate, already-established usage.
        var ok = ComponentTestMatcher.IsCoverage(
            "DataGrid", "tests/Lumeo.Tests/Misc/SomeOtherTests.cs",
            "var cut = ctx.Render<DataGrid<Person>>(p => p.Add(x => x.Items, rows));",
            KnownNames);

        Assert.True(ok);
    }

    // ----- Services-folder suffix collision (PR #361-round-2 finding) -----

    [Fact]
    public void Suffixed_service_test_file_under_Services_folder_does_not_count()
    {
        // DataGridExportServiceTests.cs suffix-matches "DataGrid" by naming
        // convention, but it tests IDataGridExportService, not the component.
        var ok = ComponentTestMatcher.IsCoverage(
            "DataGrid", "tests/Lumeo.Tests/Services/DataGridExportServiceTests.cs",
            "namespace Lumeo.Tests.Services;\npublic class DataGridExportServiceTests { }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void Suffixed_smoke_test_file_outside_Services_folder_still_counts()
    {
        // Same suffix shape, but NOT under Services — the existing E2E smoke
        // convention (DataGridSmokeTests) must keep counting.
        var ok = ComponentTestMatcher.IsCoverage(
            "DataGrid", "tests/Lumeo.Tests.E2E/Smokes/DataGridSmokeTests.cs",
            "namespace Lumeo.Tests.E2E.Smokes;\npublic class DataGridSmokeTests : PlaywrightTestBase { }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void Real_reference_inside_a_Services_folder_file_still_counts()
    {
        // The Services-folder carve-out only disables the SUFFIX fallback —
        // an actual type reference in the same file is still real coverage.
        var ok = ComponentTestMatcher.IsCoverage(
            "Select", "tests/Lumeo.Tests/Services/SomeUnrelatedServiceTests.cs",
            "namespace Lumeo.Tests.Services;\npublic class SomeUnrelatedServiceTests { var s = new Select(); }",
            KnownNames);

        Assert.True(ok);
    }

    // ----- comment/prose mentions (PR #361-round-1 fix, still respected) -----

    [Fact]
    public void Component_named_only_in_a_doc_comment_does_not_count()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            "/// <summary>\n/// Same pattern already fixed for Sheet/Drawer/Dialog/AlertDialog.\n/// </summary>\npublic class DrawerTests { }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void Component_named_only_in_a_line_comment_does_not_count()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            "// mirrors the Sheet exit-animation fix\npublic class DrawerTests { }",
            KnownNames);

        Assert.False(ok);
    }

    // ----- string literals are text, not type references (PR #424 review) -----
    //
    // A Scheduler test asserting a toolbar button reads == "Timeline" was published as
    // coverage for the unrelated Timeline component. The same shape produced ~150 other
    // bogus links: fixture text, bUnit parameter NAMES passed as strings, and component
    // names inside embedded JSON payloads.

    [Fact]
    public void Component_named_only_in_a_string_literal_does_not_count()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { Assert.Equal(""Sheet"", b.TextContent); } }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void Component_named_only_inside_a_verbatim_string_does_not_count()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var m = @""<div>Sheet</div>""; } }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void A_doubled_quote_does_not_end_a_verbatim_string_early()
    {
        // If it did, the scan would resume inside the literal and read its text as code.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var m = @""he said """"Sheet"""" once""; } }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void Component_named_only_inside_a_raw_string_payload_does_not_count()
    {
        // The registry linked "Progress" to a Gantt test purely because of a {"Progress":20}
        // field in an embedded JSON fixture.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            "public class DrawerTests { const string J = \"\"\"\n{\"Component\":\"Sheet\"}\n\"\"\"; }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void A_type_reference_inside_an_interpolation_hole_still_counts()
    {
        // The other direction: a hole is code. Blanking it would trade the false positive
        // for a false negative.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var s = $""name: {typeof(Lumeo.Sheet).Name}""; } }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void An_escaped_brace_in_an_interpolated_string_stays_text()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var s = $""{{Sheet}} literal""; } }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void Code_following_a_string_literal_is_still_scanned()
    {
        // Blanking must consume exactly the literal — an escaped quote inside it used to be
        // read as the closing one, swallowing the real reference that came after.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { Log(""a \"" b""); Render<Lumeo.Sheet>(); } }",
            KnownNames);

        Assert.True(ok);
    }

    // ----- scanning order: comments and literals are not separate passes (review round 2) -----

    [Fact]
    public void A_line_comment_marker_inside_a_string_does_not_hide_the_rest_of_the_line()
    {
        // Stripping comments before literals loses everything after the quoted "//", including
        // the real reference that follows it on the same line.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var sep = ""//""; Render<Lumeo.Sheet>(); } }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_quote_inside_a_commented_out_line_does_not_open_a_string()
    {
        // The mirror image: stripping literals first would treat the comment's lone quote as an
        // opening delimiter and blank the code up to the next quote anywhere in the file.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            "public class DrawerTests { void A() {\n" +
            "    // was: Assert.Equal(\"x\n" +
            "    Render<Lumeo.Sheet>();\n} }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_char_literal_holding_a_quote_does_not_open_a_string()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var q = '""'; Render<Lumeo.Sheet>(); } }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_type_reference_inside_an_interpolated_raw_string_hole_still_counts()
    {
        // A raw literal is blanked as one unit, so its holes have to be recognised before that
        // happens rather than by a later interpolation-aware pass.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            "public class DrawerTests { void A() { var s = $\"\"\"name: {typeof(Lumeo.Sheet).Name}\"\"\"; } }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_component_named_only_in_a_razor_comment_does_not_count()
    {
        // .razor test files are scanned too, and @* *@ is not a C# block comment. A
        // "(state-on-data-change, Gantt-class)" note in a Scrollspy host was published as Gantt
        // coverage (CodeRabbit, PR #424 round 2).
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "@* Regression host for triage #99 (state-on-data-change, Sheet-class). *@\n<Drawer />",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void A_nested_literal_inside_an_interpolation_hole_is_still_text()
    {
        // The hole is code and is rescanned as code — which means a string sitting inside it
        // gets blanked in turn, rather than being kept verbatim because it was in a hole.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var s = $""{Label(""Sheet"")}""; } }",
            KnownNames);

        Assert.False(ok);
    }

    // ----- round 3: the scanner's own blind spots -----

    [Fact]
    public void A_brace_inside_a_comment_does_not_close_an_interpolation_hole()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var s = $""{ /* } */ typeof(Lumeo.Sheet) }""; } }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void An_apostrophe_in_razor_markup_does_not_blank_the_rest_of_the_line()
    {
        // Treating it as an unterminated char literal took the component tag on the same line
        // with it (Codex review, PR #424 round 3).
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "<p>don't</p><Lumeo.Sheet />\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_real_char_literal_is_still_treated_as_one()
    {
        // The shape check must not swing the other way: 'X' here is a literal, not a reference.
        var ok = ComponentTestMatcher.IsCoverage(
            "Text", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var c = 'T'; var d = '\''; } }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void An_even_run_of_braces_is_escaped_text_all_the_way_down()
    {
        // Only a run of exactly two counted as escaped, so $"{{{{Sheet}}}}" — literal text —
        // was read as a hole and published as Sheet coverage (Codex review, PR #424 round 4).
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var s = $""{{{{Sheet}}}}""; } }",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void An_odd_run_of_braces_still_opens_a_hole()
    {
        // The parity rule must not swing the other way: three braces are an escaped pair plus
        // a hole opener, and the expression inside is real code.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var s = $""{{{typeof(Lumeo.Sheet).Name}""; } }",
            KnownNames);

        Assert.True(ok);
    }
}
