using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Lumeo;

/// <summary>
/// The behaviour + accessibility bundle a trigger hands to its child when rendered
/// with <c>AsChild</c> (no wrapper element). A cooperating child folds this onto its
/// own single root element: merge <see cref="Class"/> via <c>Cx.Merge</c> (slot first,
/// so the child still wins Tailwind conflicts), splat <see cref="Attributes"/> (static
/// role / aria / data-state) before its own author attributes, compose the relevant
/// <c>On*</c> callbacks before its own handlers, and apply <see cref="Id"/> as its
/// element <c>id</c> when non-null.
///
/// This is Lumeo's Blazor-idiomatic stand-in for Radix's <c>asChild</c> (Slot) and
/// Base UI's <c>render</c> prop. Blazor cannot clone-and-merge an opaque
/// <see cref="RenderFragment"/>, so — unlike React — the child must COOPERATE: a Lumeo
/// primitive (e.g. <c>Button</c>) auto-applies the slot via <c>[CascadingParameter]</c>;
/// arbitrary markup splats it manually through a <c>RenderFragment&lt;TriggerSlot&gt;</c>.
/// Handlers are typed fields (not stuffed into <see cref="Attributes"/>) because Blazor's
/// <c>@attributes</c> cannot chain a dictionary <c>onclick</c> with the child's own
/// single-cast handler — the child composes them in C#.
/// </summary>
/// <param name="Id">Element id the child must apply when the owning component anchors
///   positioning / aria on the trigger ELEMENT (Tooltip, sub-menus). Null when the
///   component anchors on its own root wrapper instead (Popover / Dialog / Sheet),
///   in which case no id transfer is required.</param>
/// <param name="Class">Base utility classes the trigger wrapper used to carry.</param>
/// <param name="Attributes">Static a11y / state attributes (role, aria-haspopup,
///   aria-expanded, aria-controls, data-state, ...).</param>
/// <param name="OnClick">The trigger's activation logic (open / toggle).</param>
/// <param name="OnKeyDown">The trigger's key logic. Left <c>default</c> when the child
///   is expected to activate natively (a real <c>&lt;button&gt;</c> fires click on
///   Enter/Space, so forwarding keydown there would double-activate).</param>
/// <param name="OnFocusIn">Focus-open logic (HoverCard / Tooltip); else <c>default</c>.</param>
/// <param name="OnFocusOut">Focus-close logic; else <c>default</c>.</param>
/// <param name="OnMouseEnter">Hover-open logic (HoverCard / Tooltip); else <c>default</c>.</param>
/// <param name="OnMouseLeave">Hover-close logic; else <c>default</c>.</param>
public sealed record TriggerSlot(
    string? Id,
    string Class,
    IReadOnlyDictionary<string, object> Attributes,
    EventCallback<MouseEventArgs> OnClick,
    EventCallback<KeyboardEventArgs> OnKeyDown = default,
    EventCallback<FocusEventArgs> OnFocusIn = default,
    EventCallback<FocusEventArgs> OnFocusOut = default,
    EventCallback<MouseEventArgs> OnMouseEnter = default,
    EventCallback<MouseEventArgs> OnMouseLeave = default);
