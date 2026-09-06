// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>
/// Recognizes terminal-name identities shared by every site that must agree on which sessions
/// belong to the xterm-compatible family, so the recognized name set cannot drift apart between
/// feature hints, backend selection, and active-query planning.
/// </summary>
internal static class TerminalNames
{
    // Each prefix names the terminal emulator that ships it as (or as the start of) its own
    // terminfo/TERM identity rather than an "xterm"-branded one:
    // - alacritty:  Alacritty (TERM=alacritty)
    // - foot:       foot and foot-extra, the Wayland terminal (TERM=foot, foot-extra)
    // - rxvt:       rxvt and its Unicode fork (TERM=rxvt, rxvt-unicode-256color, ...)
    // - wezterm:    WezTerm (TERM=wezterm)
    // - st:         suckless st, matched only as the exact name or with a "-" separator
    //   (TERM=st, st-256color) so it cannot absorb an unrelated name that merely starts with
    //   those two letters
    // - konsole:    KDE's Konsole (TERM=konsole-256color)
    // - vte:        the VTE library used by GNOME Terminal and many other GTK terminals
    //   (TERM=vte-256color)
    // - gnome:      GNOME Terminal's own entry on distributions that ship one (TERM=gnome-256color)
    // - contour:    Contour (TERM=contour)
    // - putty:      PuTTY (TERM=putty-256color)
    // - mintty:     mintty, MSYS2/Cygwin's terminal (TERM=mintty)
    // - iterm2:     iTerm2's own terminfo entry, distinct from its TERM_PROGRAM identification
    //   (TERM=iterm2)
    // - ghostty:    Ghostty (TERM=ghostty)
    private static readonly string[] _prefixes =
    [
        "alacritty", "foot", "rxvt", "wezterm", "konsole", "vte", "gnome", "contour", "putty",
        "mintty", "iterm2", "ghostty"
    ];

    /// <summary>
    /// Determines whether a terminal name identifies a member of the xterm-compatible family:
    /// xterm itself (recognized anywhere in the name, so a multiplexer-prefixed value such as
    /// "screen.xterm-256color" still matches), one of the known xterm-compatible emulators listed
    /// above, or suckless st matched only as the exact name or its "-" prefixed variants. A name
    /// containing "kitty" never matches, since Kitty is xterm-compatible at the protocol level but
    /// every call site already special-cases it as its own distinct identity.
    /// </summary>
    /// <param name="name">The optional terminal name to classify.</param>
    /// <returns><see langword="true"/> when <paramref name="name"/> identifies the xterm family.</returns>
    [Pure]
    public static bool IsXtermFamily(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var lower = name.ToLowerInvariant();

        if (lower.Contains("kitty", StringComparison.Ordinal))
        {
            return false;
        }

        if (lower.Contains("xterm", StringComparison.Ordinal))
        {
            return true;
        }

        if (lower == "st" || lower.StartsWith("st-", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var prefix in _prefixes)
        {
            if (lower.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
