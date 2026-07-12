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
}
