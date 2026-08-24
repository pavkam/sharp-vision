// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable tab-strip presentation. This style's own "tabControl"
/// theme key falls back to <see cref="ControlStyle"/>'s "control" key for anything it does not
/// author itself.</summary>
[PublicAPI]
public sealed record TabControlStyle: ControlStyle
{
    /// <summary>Gets the primary tab-control style definition. A theme's own "tabControl" key can
    /// restyle the divider and underline glyphs and both strip colors directly.</summary>
    internal static StyleDefinition<TabControlStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            previous != current ||
            ControlBase.ResolveColor(previous.DividerColor, previousTheme) != ControlBase.ResolveColor(current.DividerColor, currentTheme) ||
            ControlBase.ResolveColor(previous.SelectionIndicatorColor, previousTheme) != ControlBase.ResolveColor(current.SelectionIndicatorColor, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None);

    private static TabControlStyle Complete(ControlStyle control, VisualState state, Theme theme) =>
        new(
            control.Face,
            control.Border,
            control.Shadow,
            ControlGlyphs.Separators.TabDivider.Value,
            ControlGlyphs.Separators.TabUnderline.Value,
            SemanticColor.ControlBorder,
            SemanticColor.Accent);

    /// <summary>Initializes a complete tab-strip presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="dividerGlyph">The printable one-cell glyph between headers.</param>
    /// <param name="underlineGlyph">The printable one-cell selected-header underline.</param>
    /// <param name="dividerColor">The paintable divider foreground.</param>
    /// <param name="selectionIndicatorColor">The paintable selection-indicator foreground.</param>
    /// <exception cref="ArgumentException">
    /// A glyph is a control or is not one cell wide, or a color is not paintable.
    /// </exception>
    [SetsRequiredMembers]
    public TabControlStyle(
        Face face,
        Border border,
        Shadow shadow,
        Rune dividerGlyph,
        Rune underlineGlyph,
        ControlColor dividerColor,
        ControlColor selectionIndicatorColor) : base(face, border, shadow)
    {
        DividerGlyph = dividerGlyph;
        UnderlineGlyph = underlineGlyph;
        DividerColor = dividerColor;
        SelectionIndicatorColor = selectionIndicatorColor;
    }

    /// <summary>Gets the standard tab-strip presentation.</summary>
    public static new TabControlStyle Default => Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the glyph drawn between adjacent headers.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune DividerGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the glyph underlining the selected header.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune UnderlineGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the divider foreground.</summary>
    /// <remarks>
    /// Required rather than nullable. The control's old property was nullable and picked
    /// <c>SemanticColor.ControlBorder</c> at render time when unset, so the default lived at the
    /// draw site rather than in the value - which is why a theme had nothing to overlay.
    /// </remarks>
    /// <exception cref="ArgumentException">The replacement value is not paintable.</exception>
    public required ControlColor DividerColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the selection-indicator foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is not paintable.</exception>
    public required ControlColor SelectionIndicatorColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }
}
