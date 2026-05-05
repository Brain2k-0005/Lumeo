namespace Lumeo.Services;

/// <summary>
/// Cascading marker that <see cref="OverlayProvider"/> provides whenever it
/// renders an overlay shell (Dialog / Sheet / Drawer) around a user component
/// opened via <see cref="OverlayService"/>.
///
/// The user's component renders as the children of the already-rendered
/// shell. If the user's component then nests another <c>&lt;DialogContent /&gt;</c>,
/// <c>&lt;SheetContent /&gt;</c>, or <c>&lt;DrawerContent /&gt;</c> inside itself
/// — copying the inline-declarative pattern — that nested element would
/// otherwise render a second backdrop and a second sliding panel on top of
/// the one the provider already painted.
///
/// To prevent that foot-gun, each <c>*Content</c> component accepts a
/// <c>[CascadingParameter] OverlayShellMarker? Shell</c> and, when present,
/// renders only its <c>ChildContent</c> — passing through to the host shell
/// instead of duplicating it. Inline usage (<c>&lt;Sheet&gt;&lt;SheetContent /&gt;&lt;/Sheet&gt;</c>)
/// is unaffected because no marker is in scope.
/// </summary>
public sealed record OverlayShellMarker(string OverlayId);
