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

    private static TextStyle Complete(ControlStyle control, VisualState state) =>
        new(control.Face, control.Border, control.Shadow, ControlGlyphs.Text.Ellipsis.Value);

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
