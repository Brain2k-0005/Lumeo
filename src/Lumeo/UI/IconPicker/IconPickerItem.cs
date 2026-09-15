namespace Lumeo;

/// <summary>
/// One selectable entry in an <see cref="IconPicker"/>'s grid.
/// </summary>
/// <remarks>
/// <see cref="Svg"/> is a plain <see cref="IconSource"/> — the same type every first-party
/// icon pack (<c>Lumeo.Icons.Lucide</c>, <c>Lumeo.Icons.Tabler</c>, …) exposes as a static
/// property and the same type <see cref="Lumeo.SvgGlyph"/> already renders — so building the
/// list is a straight reflection pass over a pack's static class (see the
/// <c>IconPicker</c> docs page for the helper) with no dependency from the core package on any
/// individual icon pack assembly.
/// </remarks>
/// <param name="Name">
/// The icon's identifier (matches the pack's static property name, e.g. <c>"Home"</c>). This
/// is the value <see cref="IconPicker.Value"/> holds and <see cref="IconPicker.ValueChanged"/>
/// emits, and what <see cref="Keywords"/>-less search matches against.
/// </param>
/// <param name="Svg">The renderable icon.</param>
/// <param name="Keywords">
/// Optional extra search terms (aliases, synonyms) matched case-insensitively alongside
/// <see cref="Name"/> when <see cref="IconPicker.Searchable"/> is on.
/// </param>
public sealed record IconPickerItem(string Name, IconSource Svg, IReadOnlyList<string>? Keywords = null);
