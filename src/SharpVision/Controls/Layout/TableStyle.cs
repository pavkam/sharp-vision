// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable tabular presentation. This style's own "table" theme key
/// falls back to <see cref="ControlStyle"/>'s "control" key for anything it does not author
/// itself.
///
/// <para>Falls back to "control" rather than "container" despite a table being container-shaped:
/// <c>Table</c> takes <c>ControlBase</c>'s borderless default today, and <c>ContainerStyle</c>'s
/// light border reserves a cell on every edge, which would change every table's measured size.</para>
/// </summary>
[PublicAPI]
public sealed record TableStyle: ControlStyle
{
    /// <summary>Gets the primary table-style definition. A theme's own "table" key can restyle the
    /// grid glyphs, the three part colors, and the cell padding directly.</summary>
    internal static StyleDefinition<TableStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        Compare);

    private static TableStyle Complete(ControlStyle control, VisualState state) =>
        new(control.Face, control.Border, control.Shadow, TableGlyphs.Default, default);

    /// <summary>Initializes a complete tabular presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="glyphs">The complete grid-line glyph family.</param>
    /// <param name="cellPadding">The padding applied to every header and data cell.</param>
    [SetsRequiredMembers]
    public TableStyle(Face face, Border border, Shadow shadow, TableGlyphs glyphs, Thickness cellPadding)
        : base(face, border, shadow)
    {
        Glyphs = glyphs;
        CellPadding = cellPadding;
    }

    /// <summary>Gets the standard tabular presentation.</summary>
    public static new TableStyle Default => Complete(ControlStyle.Default, VisualState.Normal);

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

    private static InvalidationImpact Compare(
        TableStyle previous,
        Theme? previousTheme,
        TableStyle current,
        Theme? currentTheme) =>
        previous.CellPadding != current.CellPadding
            ? InvalidationImpact.Measure
            : previous != current ||
              Resolved(previous.HeaderForeground, previousTheme) != Resolved(current.HeaderForeground, currentTheme) ||
              Resolved(previous.HeaderBackground, previousTheme) != Resolved(current.HeaderBackground, currentTheme) ||
              Resolved(previous.GridLineColor, previousTheme) != Resolved(current.GridLineColor, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None;

    // A null part color stays distinct from every resolved one, so "inherit the face" and "paint
    // this exact color" never compare equal just because a theme maps them to the same literal.
    private static Color? Resolved(ControlColor? value, Theme? theme) =>
        value is { } color ? ControlBase.ResolveColor(color, theme) : null;
}
