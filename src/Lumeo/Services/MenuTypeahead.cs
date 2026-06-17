using System;

namespace Lumeo.Services;

/// <summary>
/// Shared type-to-focus (typeahead) buffer for the menu family (DropdownMenu,
/// Menubar, MegaMenu). Radix collects printable keystrokes into a query string,
/// resets it after a short idle gap, and focuses the first item whose label
/// starts with the accumulated query. This helper owns only the buffer + the
/// idle-reset timing in managed code; the DOM text match + focus + scroll lives
/// in JS (<c>focusMenuItemByTypeahead</c>) so it stays SSR-safe and reusable.
/// </summary>
internal sealed class MenuTypeahead
{
    /// <summary>Idle window after which the next keystroke starts a fresh query
    /// rather than appending. Matches Radix's 1 second typeahead reset.</summary>
    private const int ResetMs = 1000;

    private string _buffer = string.Empty;
    private long _lastKeyTicks;

    /// <summary>Current accumulated query (for tests / diagnostics).</summary>
    public string Buffer => _buffer;

    /// <summary>
    /// Returns true when <paramref name="key"/> is a single printable character
    /// that should drive typeahead (a letter, digit, or symbol — not a named key
    /// like "ArrowDown", "Enter", "Escape", " "/Space which has menu semantics,
    /// or a modifier combo). The caller checks Ctrl/Alt/Meta separately.
    /// </summary>
    public static bool IsTypeaheadKey(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length != 1) return false;
        var c = key[0];
        // Space is reserved for activating the focused item; control chars are
        // never printable. Everything else printable (incl. letters/digits/
        // punctuation) is a valid typeahead character.
        return !char.IsControl(c) && !char.IsWhiteSpace(c);
    }

    /// <summary>
    /// Appends <paramref name="key"/> to the query (or starts a new query if the
    /// idle window elapsed) and returns the query string to search for. Should
    /// only be called with a key for which <see cref="IsTypeaheadKey"/> is true.
    /// </summary>
    public string Push(string key)
    {
        var now = DateTime.UtcNow.Ticks;
        var idleMs = (now - _lastKeyTicks) / TimeSpan.TicksPerMillisecond;
        if (_buffer.Length == 0 || idleMs > ResetMs)
        {
            _buffer = key;
        }
        else
        {
            _buffer += key;
        }
        _lastKeyTicks = now;
        return _buffer;
    }

    /// <summary>Clears the buffer (e.g. when the menu closes).</summary>
    public void Reset()
    {
        _buffer = string.Empty;
        _lastKeyTicks = 0;
    }
}
