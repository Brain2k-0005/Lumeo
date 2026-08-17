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
    public void A_real_char_literal_is_consumed_without_eating_what_follows()
    {
        // This used to assert False against a fixture that never contained the token at all, so
        // it passed for any scanner behaviour whatsoever (CodeRabbit, PR #424 round 7). The real
        // property is that an escaped-quote char literal is consumed EXACTLY, leaving the
        // reference after it visible.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var d = '\''; Render<Lumeo.Sheet>(); } }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_char_literal_holding_a_component_name_is_not_a_reference()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Text", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var c = 'T'; var s = ""Text""; } }",
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

    [Fact]
    public void A_reference_inside_a_razor_attribute_expression_still_counts()
    {
        // A markup attribute uses the same quote a C# string does, so blanking the whole value
        // discarded the Razor expression inside it (Codex review, PR #424 round 5).
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "<Host Value=\"@(typeof(Lumeo.Sheet))\" />\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void An_implicit_razor_expression_counts_too()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "<Host Title=\"@Lumeo.Sheet.Name\" />\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void Plain_attribute_text_in_razor_is_still_text()
    {
        // The Razor allowance must not turn every attribute value back into code.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "<Host Title=\"Sheet\" />\n",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void A_string_literal_in_a_cs_file_is_unaffected_by_the_razor_allowance()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerTests.cs",
            @"public class DrawerTests { void A() { var m = ""@(typeof(Lumeo.Sheet))""; } }",
            KnownNames);

        Assert.False(ok);
    }

    // ----- round 6: a .razor file is markup AND code, and they differ -----

    [Fact]
    public void A_literal_inside_a_razor_code_block_is_text_even_if_it_looks_like_markup()
    {
        // The Razor allowance was file-wide, so a const string that merely LOOKS like an
        // attribute expression counted as a reference (Codex review, PR #424 round 6).
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "<div></div>\n@code { const string Example = \"@(typeof(Lumeo.Sheet))\"; }\n",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void A_real_reference_inside_a_razor_code_block_still_counts()
    {
        // The region model must not blank code: inside @code this is ordinary C#.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "<div></div>\n@code { void A() { Render<Lumeo.Sheet>(); } }\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_single_quoted_markup_attribute_is_text()
    {
        // IsCharLiteral rejects the apostrophe because the value is longer than one character,
        // and the scanner then left the whole attribute as code — while the double-quoted
        // equivalent was correctly blanked (Codex review, PR #424 round 6).
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "<Host Title='Sheet' />\n",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void A_single_quoted_attribute_keeps_its_razor_expression()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "<Host Value='@(typeof(Lumeo.Sheet))' />\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_char_literal_inside_a_razor_code_block_does_not_eat_what_follows()
    {
        // Same vacuous shape as above: the fixture never named the component, so the assertion
        // held regardless of how the char literal was scanned.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "<div></div>\n@code { char c = 'T'; void A() { Render<Lumeo.Sheet>(); } }\n",
            KnownNames);

        Assert.True(ok);
    }

    // ----- round 7 -----

    [Fact]
    public void An_inline_razor_template_inside_a_code_block_is_markup()
    {
        // Render(@<Collapsible>Toggle</Collapsible>) steps back into markup for the length of
        // the fragment. Staying in C# mode left its caption reading as code — which is why
        // toggle.json claimed CollapsibleTests.razor purely for the word on a trigger button
        // (Codex review, PR #424 round 7).
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "@code { void A() { Render(@<Drawer>My Sheet Button</Drawer>); } }\n",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void A_component_tag_inside_an_inline_template_still_counts()
    {
        // Markup mode must not blank the fragment wholesale: the TAGS in it are real usage.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "@code { void A() { Render(@<Drawer><Sheet /></Drawer>); } }\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void Code_after_an_inline_template_is_still_scanned()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "@code { void A() { Render(@<Drawer>Text</Drawer>); Render<Lumeo.Sheet>(); } }\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_string_inside_a_razor_attribute_expression_is_still_text()
    {
        // Aborting on the inner quote made AppendMarkupAttribute treat it as the attribute's
        // terminator, so the argument leaked out as code.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "<Host Value=\"@(Get(\'Sheet\'))\" />\n".Replace('\'', '\"'),
            KnownNames);

        Assert.False(ok);
    }

    // ----- round 8 -----

    [Fact]
    public void An_attribute_name_is_not_a_component_reference()
    {
        // A parameter is not a type. Keeping attribute names published false coverage:
        // label.json claimed MegaMenuDisabledHost.razor for `<MegaMenuItem Label="Products">`
        // and text.json claimed SplitButtonRotationTests.razor for `Text="Save"`
        // (Codex review, PR #424 round 8).
        var ok = ComponentTestMatcher.IsCoverage(
            "Text", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "<Drawer Text=\"Save\" />\n",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void An_element_name_is_still_a_component_reference()
    {
        // The other direction: blanking the tag's interior must not take the element name.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "<Sheet Title=\"Anything\" />\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_razor_control_directive_condition_is_code()
    {
        // Stopping at the keyword left the condition to be blanked as prose, so a reference the
        // same expression would keep inside @code was lost out here.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "@if (typeof(Lumeo.Sheet) != null) { <Drawer /> }\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_control_directive_body_is_still_markup()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "@if (true) { <Drawer>Sheet</Drawer> }\n",
            KnownNames);

        Assert.False(ok);
    }

    // ----- round 9 -----

    [Fact]
    public void A_parenthesis_inside_a_literal_does_not_truncate_a_directive_condition()
    {
        // Counting it closed the condition at Check's paren, leaving the reference after it to
        // be blanked as prose (Codex review, PR #424 round 9).
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "@if (Check(\")\") && typeof(Lumeo.Sheet) != null) { <Drawer /> }\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void An_html_void_element_does_not_swallow_an_inline_template()
    {
        // <br> has no closing tag, so counting it as an opener meant the root </div> never
        // balanced and the scanner ate the rest of the C# block as markup.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "@code { void A() { Render(@<div><br><Drawer /></div>); var t = typeof(Lumeo.Sheet); } }\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_type_in_an_inherits_directive_counts()
    {
        // The implicit-expression scan stopped at the space, so only the keyword survived and
        // the type was blanked as markup prose.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "@inherits TestHost<Lumeo.Sheet>\n<div></div>\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_type_in_an_inject_directive_counts()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "@inject Lumeo.Sheet Subject\n<div></div>\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_directive_allowance_does_not_leak_past_its_own_line()
    {
        // The rest of the LINE is C#, not the rest of the file: markup on the next line stays
        // markup.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "@inject IServiceProvider Sp\n<Drawer>Sheet</Drawer>\n",
            KnownNames);

        Assert.False(ok);
    }

    // ----- round 10 -----

    [Fact]
    public void An_implicit_expression_keeps_its_invocation_arguments()
    {
        // The contract on EndOfRazorExpression already promised @Foo.Bar(...), but it stopped at
        // the identifier, so the argument was blanked as attribute text (Codex review round 10).
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "<Host Value=\"@Get(typeof(Lumeo.Sheet))\" />\n",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_greater_than_inside_a_quoted_attribute_is_not_the_tag_end()
    {
        // Taking the first '>' left the inline template unbalanced, so the scanner consumed the
        // C# after it as markup and lost the reference.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "@code { void A() { Render(@<Host Title=\"a > b\" />); var t = typeof(Lumeo.Sheet); } }\n",
            KnownNames);

        Assert.True(ok);
    }

    // ----- round 11 -----

    [Fact]
    public void A_razor_expression_inside_an_attribute_does_not_end_the_tag_early()
    {
        // The expression carries its own literals, and one of them holds both the delimiter and
        // a '>'. Stopping at the next matching quote ended the attribute early, so the '>' in
        // that string was read as the tag's end and the template stayed open through the C#
        // after it (Codex review, PR #424 round 11).
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"@code { void A() { Render(@<Host Title=""@($""a > b"")"" />); var t = typeof(Lumeo.Sheet); } }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void An_implicit_expression_follows_its_whole_suffix_chain()
    {
        // `@Get().Render(...)` continues past the first pair of parentheses; stopping there
        // blanked the reference in the second.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<Host Value=""@Get().Render(typeof(Lumeo.Sheet))"" />",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void An_implicit_expression_stops_where_it_actually_ends()
    {
        // The chain must not run on into the attribute's own text.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<Host Value=""@Get() Sheet"" />",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void An_indexer_suffix_is_part_of_the_expression_too()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<Host Value=""@Items[0].Render(typeof(Lumeo.Sheet))"" />",
            KnownNames);

        Assert.True(ok);
    }

    // ----- round 12 -----

    [Fact]
    public void Whitespace_ends_an_implicit_expression()
    {
        // Razor stops at the space, so the parenthesised text after it is display text — the
        // suffix chain must not reach across and keep it as code (Codex review round 12).
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<div title=""@Get() (Sheet)""></div>",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void Conditional_access_continues_the_expression()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<Host Value=""@Provider?.Render(typeof(Lumeo.Sheet))"" />",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_bracket_inside_a_comment_does_not_close_an_indexer()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<Host Value=""@Items[/* ] */0].Render(typeof(Lumeo.Sheet))"" />",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_quoted_attribute_value_may_span_lines()
    {
        // Ending the value at the line break made the '>' on the continuation look like the
        // tag's end, leaving the template open through the C# after it.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            "@code { void A() { Render(@<Host Title=\"a\n> b\" />); var t = typeof(Lumeo.Sheet); } }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_tag_inside_an_html_comment_is_a_sample_not_a_reference()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<div><!-- use <Sheet /> here --></div>",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void A_less_than_operator_in_a_template_body_is_not_a_tag()
    {
        // The comparison opened a phantom element that swallowed the real closing tag, leaving
        // the template unbalanced through the C# that followed.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"@code { void A() { Render(@<div>@(1 < 2 ? ""x"" : ""y"")</div>); var t = typeof(Lumeo.Sheet); } }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_generic_type_parameter_attribute_carries_a_type()
    {
        // TItem takes type syntax rather than display text, so this is a real reference.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<Grid TItem=""Lumeo.Sheet"" />",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void An_ordinary_attribute_is_still_display_text()
    {
        // The type-parameter allowance keys on the @typeparam naming convention, so a plain
        // caption must not be dragged in with it.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<Grid Title=""Lumeo.Sheet"" />",
            KnownNames);

        Assert.False(ok);
    }

    // ----- round 13 -----

    [Fact]
    public void A_brace_in_a_plain_string_is_not_an_interpolation_hole()
    {
        // Round 12 added hole tracking to the string skipper unconditionally, so the brace here
        // opened a phantom hole, the closing quote looked like a nested literal, and the scan ran
        // off the end — blanking the reference. This is the regression the round-12 change caused,
        // and it is the discriminating case I could not construct back then (Codex review round 13).
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<Host Value=""@(Check(""{"") && typeof(Lumeo.Sheet) != null)"" />",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_razor_comment_inside_an_inline_template_is_skipped()
    {
        // '@*' makes no progress through the expression scanner, so the sample tag inside the
        // comment was counted as a live element and the root never balanced.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"@code { void A() { Render(@<div>@* <span> *@</div>); var t = typeof(Lumeo.Sheet); } }",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void Markup_inside_a_script_element_is_data()
    {
        // Razor treats a raw-text element's body as character data, so the tag in it is a sample
        // rather than an instantiated component.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<div><script>const sample = ""<Sheet />"";</script></div>",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void A_component_after_a_script_element_is_still_a_reference()
    {
        // Blanking the body must stop at the closing tag.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<div><script>var x = 1;</script><Sheet /></div>",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_directive_attribute_value_is_code()
    {
        // Razor parses a directive attribute's value as C# even with no leading '@' on the value.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<div @key=""typeof(Lumeo.Sheet)""></div>",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_plain_attribute_next_to_a_directive_one_is_still_text()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<div @key=""1"" title=""Sheet""></div>",
            KnownNames);

        Assert.False(ok);
    }

    // ----- round 14: the consequences of round 13's raw-text and directive handling -----

    [Fact]
    public void A_self_closing_raw_text_element_has_no_body()
    {
        // Razor accepts <textarea />, and this repo writes it that way. Treating it as opening a
        // body made the absent end tag swallow the rest of the file (Codex review round 14).
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<div><textarea /> <Sheet /></div>",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_razor_transition_inside_a_raw_text_body_is_still_code()
    {
        // Razor evaluates the expression even though the surrounding script text is data.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<script>const n = '@(typeof(Lumeo.Sheet).Name)';</script>",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_similar_looking_end_tag_does_not_close_a_raw_text_body()
    {
        // </scripture> is not </script>, and stopping there let the sample tag after it read as
        // live markup.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<script>const s = ""</scripture><Sheet />"";</script>",
            KnownNames);

        Assert.False(ok);
    }

    [Fact]
    public void A_directive_attribute_value_may_contain_its_own_strings()
    {
        // The value is C#, so the terminator has to be found the C# way — stopping at the first
        // nested quote processed the rest as markup and blanked the reference.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<div @onclick=""@(() => { Log(""x""); Use(typeof(Lumeo.Sheet)); })""></div>",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_brace_in_a_char_literal_does_not_close_an_interpolation_hole()
    {
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"<div @key=""@(Check($""{Test('}') && Get(""x"")}"") && typeof(Lumeo.Sheet) != null)""></div>",
            KnownNames);

        Assert.True(ok);
    }

    [Fact]
    public void A_raw_text_body_inside_an_inline_template_is_data()
    {
        // Counting <span> inside the script as an opener meant the real closing tags could never
        // balance the template, so the C# after it was consumed as markup.
        var ok = ComponentTestMatcher.IsCoverage(
            "Sheet", "tests/Lumeo.Tests/Misc/DrawerHost.razor",
            @"@code { void A() { Render(@<div><script>const sample = ""<span>"";</script></div>); var t = typeof(Lumeo.Sheet); } }",
            KnownNames);

        Assert.True(ok);
    }
}
