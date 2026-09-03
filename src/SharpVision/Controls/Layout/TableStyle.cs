// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable tabular presentation. This style declares no theme
/// section of its own: it falls back to <see cref="ControlStyle"/>'s "control" role section for
/// its passive chrome, resolves its own grid glyphs, part colors, and cell padding from
/// code-owned defaults, and is themeable only through that fallback and a locally assigned
/// <see cref="Table.Style"/>.
///
/// <para>Falls back to "control" rather than "container" despite a table being container-shaped:
/// <c>Table</c> takes <c>ControlBase</c>'s borderless default today, and <c>ContainerStyle</c>'s
/// light border reserves a cell on every edge, which would change every table's measured size.</para>
/// </summary>
[PublicAPI]
public sealed record TableStyle: ControlStyle
{
    /// <summary>Gets the primary table-style definition. Falls back through the theme's focused
    /// tabular control set rather than the bare "control" role section so a directly focused
    /// Table gets the theme's focused text cue instead of none at all. The tabular set deliberately
    /// omits the generic borderless reverse-video fallback because Table paints one large owner
    /// surface behind independently styled cells; row and cell selection own its strong filled
    /// cue. Hover and press remain exactly as the passive "control" role resolves them. The grid
    /// glyphs, the three part colors, and the cell padding are all code-owned.</summary>
    internal static StyleDefinition<TableStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetTabularControlStyleSet(),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            previous.CellPadding != current.CellPadding
                ? InvalidationImpact.Measure
                : previous != current ||
                  Resolved(previous.HeaderForeground, previousTheme) != Resolved(current.HeaderForeground, currentTheme) ||
                  Resolved(previous.HeaderBackground, previousTheme) != Resolved(current.HeaderBackground, currentTheme) ||
                  Resolved(previous.GridLineColor, previousTheme) != Resolved(current.GridLineColor, currentTheme) ||
                  ControlBase.ResolveColor(previous.SelectedTextColor, previousTheme) !=
                  ControlBase.ResolveColor(current.SelectedTextColor, currentTheme) ||
                  ControlBase.ResolveColor(previous.SelectedBackground, previousTheme) !=
                  ControlBase.ResolveColor(current.SelectedBackground, currentTheme) ||
                  Resolved(previous.PlaceholderForeground, previousTheme) !=
                  Resolved(current.PlaceholderForeground, currentTheme) ||
                  Resolved(previous.PlaceholderErrorForeground, previousTheme) !=
                  Resolved(current.PlaceholderErrorForeground, currentTheme)
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None);

    private static TableStyle Complete(ControlStyle control, VisualState state, Theme theme) =>
        new(
            control.Face,
            control.Border,
            control.Shadow,
            TableGlyphs.Default,
            default,
            SemanticColor.SelectedText,
            SemanticColor.SelectedControl,
            SemanticColor.Muted,
            SemanticColor.Error);

    /// <summary>Initializes a complete tabular presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="glyphs">The complete grid-line glyph family.</param>
    /// <param name="cellPadding">The padding applied to every header and data cell.</param>
    /// <param name="selectedTextColor">The non-transparent selected-cell foreground.</param>
    /// <param name="selectedBackground">The non-transparent selected-cell background.</param>
    /// <param name="placeholderForeground">The non-transparent pending-placeholder-row foreground.</param>
    /// <param name="placeholderErrorForeground">The non-transparent failed-placeholder-row foreground.</param>
    /// <exception cref="ArgumentException">A configured placeholder color is transparent.</exception>
    [SetsRequiredMembers]
    public TableStyle(
        Face face,
        Border border,
        Shadow shadow,
        TableGlyphs glyphs,
        Thickness cellPadding,
        ControlColor selectedTextColor,
        ControlColor selectedBackground,
        ControlColor placeholderForeground,
        ControlColor placeholderErrorForeground)
        : base(face, border, shadow)
    {
        Glyphs = glyphs;
        CellPadding = cellPadding;
        SelectedTextColor = selectedTextColor;
        SelectedBackground = selectedBackground;
        PlaceholderForeground = placeholderForeground;
        PlaceholderErrorForeground = placeholderErrorForeground;
    }

    /// <summary>Gets the standard tabular presentation.</summary>
    public static new TableStyle Default => Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the complete grid-line glyph family.</summary>
    public required TableGlyphs Glyphs { get; init; }

    /// <summary>Gets the padding applied to every header and data cell.</summary>
    public required Thickness CellPadding { get; init; }

    /// <summary>Gets the header-text foreground, or <see langword="null"/> to inherit the table's own
    /// resolved face.</summary>
    /// <remarks>
    /// Nullable on purpose, unlike <c>TabControlStyle</c>'s required part colors. Null here is not
    /// "unset, so pick a semantic default" - it means "use whatever the table's own resolved
    /// foreground is", which no fixed <see cref="ControlColor"/> can express because it follows the
    /// face through every state and theme.
    /// </remarks>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public ControlColor? HeaderForeground
    {
        get;
        init
        {
            if (value is { } color)
            {
                ControlColor.ValidatePaint(color, nameof(value));
            }

            field = value;
        }
    }

    /// <summary>Gets the header-row background, or <see langword="null"/> to inherit the table's own
    /// resolved face.</summary>
    /// <remarks>
    /// The null case carries a second meaning the others do not: an unset header background also
    /// means the header row is not filled at all unless the table itself paints an opaque fill, so
    /// collapsing this to a required member would paint a header band on every table.
    /// </remarks>
    public ControlColor? HeaderBackground { get; init; }

    /// <summary>Gets the grid-line color, or <see langword="null"/> to inherit the table's own
    /// resolved face.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public ControlColor? GridLineColor
    {
        get;
        init
        {
            if (value is { } color)
            {
                ControlColor.ValidatePaint(color, nameof(value));
            }

            field = value;
        }
    }

    /// <summary>Gets the selected-cell foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor SelectedTextColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the selected-cell background.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor SelectedBackground
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the pending-placeholder-row foreground.</summary>
    /// <remarks>
    /// Required rather than nullable, unlike <see cref="HeaderForeground"/> and
    /// <see cref="GridLineColor"/>: the placeholder skeleton is a synthetic status indicator with no
    /// "inherit the table's own resolved face" meaning to fall back to, the same reasoning
    /// <c>TreeViewStyle.LoadingColor</c> already applies to its own status row.
    /// </remarks>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor PlaceholderForeground
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the failed-placeholder-row foreground.</summary>
    /// <remarks>
    /// Required rather than nullable, unlike <see cref="HeaderForeground"/> and
    /// <see cref="GridLineColor"/>: the placeholder skeleton is a synthetic status indicator with no
    /// "inherit the table's own resolved face" meaning to fall back to, the same reasoning
    /// <c>TreeViewStyle.FailedColor</c> already applies to its own status row.
    /// </remarks>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor PlaceholderErrorForeground
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    // A null part color stays distinct from every resolved one, so "inherit the face" and "paint
    // this exact color" never compare equal just because a theme maps them to the same literal.
    [Pure]
    private static Color? Resolved(ControlColor? value, Theme? theme) =>
        value is { } color ? ControlBase.ResolveColor(color, theme) : null;
}
