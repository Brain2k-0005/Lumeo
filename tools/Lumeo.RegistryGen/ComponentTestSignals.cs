using System.Text.RegularExpressions;

namespace Lumeo.RegistryGen;

/// <summary>
/// Single source of truth for "does this test file exercise component X" — shared by
/// Program.cs's <c>ComputeTestCoverage</c> (feeds <c>testCoverage.files</c>/
/// <c>relatedFiles</c> stats) and <see cref="PerComponentEnricher"/> (feeds the
/// per-component JSON's <c>tests[]</c> array). Before this existed, each scanner grew
/// its OWN copy of the "renders" regex; PerComponentEnricher's copy got extended (round
/// 1) to tolerate a namespace/alias-qualified generic argument
/// (<c>Render&lt;Lumeo.X&gt;</c>, <c>Render&lt;L.X&gt;</c>) while Program.cs's copy did
/// not, so a test using the qualified form (e.g. Spinner's
/// <c>A11yPolishTests.cs</c> calling <c>ctx.Render&lt;Lumeo.Spinner&gt;()</c>) showed up
/// in <c>tests[]</c> but was invisible to <c>testCoverage.relatedFiles</c> — calendar.json
/// and spinner.json both shipped with <c>relatedFiles: 0</c> despite <c>tests[]</c>
/// actually containing a related file (CodeRabbit, PR #356 round-2). Both call sites now
/// build their regex from here, so they can never drift apart again.
/// </summary>
internal static class ComponentTestSignals
{
    /// <summary>
    /// Matches a test file that actually renders/opens the named component:
    /// <c>Render&lt;(ns.)*X&gt;</c>, <c>&lt;X ...&gt;</c> markup, <c>OpenComponent&lt;X&gt;</c>,
    /// or a qualified <c>Lumeo.X</c>/<c>L.X</c> type reference.
    /// </summary>
    public static Regex BuildRendersRegex(string componentName) => new(
        $@"Render<(?:\w+\.)*{Regex.Escape(componentName)}[<>(]" +
        $@"|<{Regex.Escape(componentName)}[\s/>]" +
        $@"|OpenComponent<[^>]*\b{Regex.Escape(componentName)}\b" +
        $@"|\b(?:Lumeo|L)\.{Regex.Escape(componentName)}\b",
        RegexOptions.Compiled);

    /// <summary>
    /// True when a repo-relative test path is physically filed under the component's OWN
    /// dedicated test folder (e.g. <c>tests/Lumeo.Tests/Components/Stepper/...</c>) —
    /// folder ownership is a stronger signal than the render regex above, since a
    /// dedicated sub-component/helper test can legitimately belong to a component
    /// without ever rendering it directly.
    /// </summary>
    public static bool IsOwnedByFolder(string relPath, string componentName) =>
        relPath.Replace('\\', '/').Split('/').Any(seg =>
            string.Equals(seg, componentName, StringComparison.OrdinalIgnoreCase));
}
