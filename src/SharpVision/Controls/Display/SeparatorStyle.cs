// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable divider presentation. This style declares no theme
/// section of its own: it falls back to <see cref="ControlStyle"/>'s "control" role section for
/// its passive chrome, resolves its own horizontal and vertical glyphs from code-owned defaults,
/// and is themeable only through that fallback and a locally assigned
/// <see cref="Separator.Style"/>.</summary>
[PublicAPI]
public sealed record SeparatorStyle: ControlStyle
{
    /// <summary>Gets the primary separator-style definition. Falls back to
    /// <see cref="ControlStyle"/>'s "control" role section; the horizontal and vertical glyphs
    /// are code-owned.</summary>
    internal static StyleDefinition<SeparatorStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, _, current, _) =>
            previous != current ? InvalidationImpact.Render : InvalidationImpact.None);

    /// <summary>Gets the non-invalidating definition used by library and external pure forwarding hosts -
    /// <c>MessageBox</c>'s and every <c>FileDialogBase</c>-derived dialog's shared action-bar
    /// divider.</summary>
    public static StyleDefinition<SeparatorStyle> ForwardingDefinition { get; } = StyleDefinitions.Part(
        static theme => Definition.Resolve(null, theme),
        static (_, _, _, _) => InvalidationImpact.None);

    private static SeparatorStyle Complete(ControlStyle control, VisualState state, Theme theme) =>
        new(control.Face, control.Border, control.Shadow, new Rune('─'), new Rune('│'));

    /// <summary>Initializes a complete separator presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="horizontalGlyph">The printable one-cell horizontal divider glyph.</param>
    /// <param name="verticalGlyph">The printable one-cell vertical divider glyph.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public SeparatorStyle(Face face, Border border, Shadow shadow, Rune horizontalGlyph, Rune verticalGlyph) : base(face, border, shadow)
    {
        HorizontalGlyph = horizontalGlyph;
        VerticalGlyph = verticalGlyph;
    }

    /// <summary>Gets the standard separator presentation.</summary>
    public static new SeparatorStyle Default => Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the horizontal divider glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune HorizontalGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the vertical divider glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune VerticalGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }
}
