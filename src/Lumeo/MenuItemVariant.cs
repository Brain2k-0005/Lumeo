namespace Lumeo;

/// <summary>
/// Visual variant for a menu item (DropdownMenu / ContextMenu / Menubar).
/// Mirrors shadcn/ui's <c>variant</c> prop on menu items. In addition to the
/// applied Tailwind classes, the item emits a <c>data-variant</c> attribute so
/// consumers can drive their own <c>data-[variant=destructive]:…</c> CSS.
/// </summary>
public enum MenuItemVariant
{
    /// <summary>The standard menu item styling.</summary>
    Default,

    /// <summary>A destructive action (delete, remove) rendered in the destructive color.</summary>
    Destructive,
}
