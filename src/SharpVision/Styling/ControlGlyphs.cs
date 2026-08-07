// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Provides stable code-owned glyph defaults shared by built-in controls.</summary>
internal static class ControlGlyphs
{
    /// <summary>Gets border, shadow, and window chrome glyphs.</summary>
    public static ChromeGlyphs Chrome { get; } = new(
        Glyph('╭', '+'), Glyph('─', '-'), Glyph('╮', '+'), Glyph('│', '|'), Glyph('╯', '+'),
        Glyph('─', '-'), Glyph('╰', '+'), Glyph('│', '|'), Glyph('▓', '#'),
        Glyph('▀', '#'), Glyph('▄', '#'), Glyph('█', '#'),
        Glyph('[', '['), Glyph('■', 'x'), Glyph(']', ']'));

    /// <summary>Gets progress and fractional block glyphs.</summary>
    public static ProgressGlyphs Progress { get; } = new(
        Glyph('░', '.'),
        Glyph('█', '#'),
        Glyph('▒', '?'),
        [
            Glyph('░', '.'), Glyph('▏', ':'), Glyph('▎', '-'), Glyph('▍', '='), Glyph('▌', '+'),
            Glyph('▋', '*'), Glyph('▊', '%'), Glyph('▉', '@'), Glyph('█', '#')
        ],
        [
            Glyph('░', '.'), Glyph('▁', ':'), Glyph('▂', '-'), Glyph('▃', '='), Glyph('▄', '+'),
            Glyph('▅', '*'), Glyph('▆', '%'), Glyph('▇', '@'), Glyph('█', '#')
        ]);

    /// <summary>Gets disclosure and drop-down glyphs.</summary>
    public static DisclosureGlyphs Disclosure { get; } = new(
        Glyph('▶', '>'), Glyph('▼', 'v'), Glyph('▼', 'v'));

    /// <summary>Gets checkbox, radio, and menu-selection glyphs.</summary>
    public static SelectionGlyphs Selection { get; } = new(
        Glyph(' ', ' '), Glyph('✓', 'x'), Glyph('─', '-'),
        Glyph('○', 'o'), Glyph('✓', 'x'), Glyph('−', '-'),
        Glyph('☐', 'o'), Glyph('☑', 'x'), Glyph('◩', '-'),
        Glyph('○', 'o'), Glyph('◉', 'x'), Glyph(' ', ' '), Glyph('●', 'x'),
        Glyph(' ', ' '), Glyph('✓', 'x'), Glyph('○', 'o'), Glyph('◉', 'x'));

    /// <summary>Gets navigation markers and separators.</summary>
    public static NavigationGlyphs Navigation { get; } = new(
        Glyph('·', '.'), Glyph('›', '>'), Glyph('▶', '>'), Glyph('▼', 'v'), Glyph('─', '-'));

    /// <summary>Gets structural separator and tab glyphs.</summary>
    public static SeparatorGlyphs Separators { get; } = new(
        Glyph('─', '-'), Glyph('│', '|'), Glyph('─', '-'), Glyph('─', '-'),
        Glyph('│', '|'), Glyph('┼', '+'), Glyph('│', '|'), Glyph('─', '-'));

    /// <summary>Gets framework-authored text glyphs.</summary>
    public static TextGlyphs Text { get; } = new(Glyph('…', '.'));

    private static ControlGlyph Glyph(char value, char fallback) =>
        new(new Rune(value), new Rune(fallback));
}
