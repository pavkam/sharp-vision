// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable text presentation. This style's own "text" theme key
/// falls back to <see cref="ControlStyle"/>'s "control" key for anything it does not author
/// itself.</summary>
/// <remarks>
/// The truncation marker has the widest blast radius of any single glyph in the library - every
/// elided string in an application renders it - and was reachable only per <see cref="Text"/>
/// instance, so a theme built for a terminal without dependable ellipsis coverage had to be applied
/// at every construction site.
/// </remarks>
[PublicAPI]
public sealed record TextStyle: ControlStyle
{
    /// <summary>Gets the primary text-style definition. A theme's own "text" key restyles the
    /// truncation marker for every elided string at once.</summary>
    internal static StyleDefinition<TextStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        Compare);

    // Text never paints its own background - lines draw with BackgroundMode.Transparent, and
    // there is no body fill - so adopting the fallback's opaque background here was an
    // incoherent claim with a real cost: every bundled theme authors an opaque
    // "styles.control.normal.face.background", and copying it wholesale closed the
    // ambient-inheritance transparency gate for every themed Text. A caption hosted under a
    // state-ambient parent (a hovered list row, a focused pressable) then kept its own normal
    // foreground instead of inheriting the parent's state face, which erased the hover and
    // focus foreground cues wherever the pointer path did not reach the caption itself. The
    // code-owned transparent background is the per-state completion default; a theme that
    // genuinely wants an opaque Text background can still author "styles.text.normal.face.background".
    private static TextStyle Complete(ControlStyle control, VisualState state) =>
        new(
            control.Face with { Background = Color.Transparent },
            control.Border,
            control.Shadow,
            ControlGlyphs.Text.Ellipsis.Value);

    /// <summary>Initializes a complete text presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="ellipsisGlyph">The printable one-cell truncation marker.</param>
    /// <exception cref="ArgumentException"><paramref name="ellipsisGlyph"/> is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public TextStyle(Face face, Border border, Shadow shadow, Rune ellipsisGlyph) : base(face, border, shadow) =>
        EllipsisGlyph = ellipsisGlyph;

    /// <summary>Gets the standard text presentation.</summary>
    public static new TextStyle Default => Complete(ControlStyle.Default, VisualState.Normal);

    /// <summary>Gets the glyph marking an elided run.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune EllipsisGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    private static InvalidationImpact Compare(
        TextStyle previous,
        Theme? previousTheme,
        TextStyle current,
        Theme? currentTheme) =>
        previous != current
            ? InvalidationImpact.Render
            : InvalidationImpact.None;
}
