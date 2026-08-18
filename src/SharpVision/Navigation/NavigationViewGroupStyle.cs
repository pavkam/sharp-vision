// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable navigation-group presentation. This style's own
/// "navigationViewGroup" theme key falls back to the standard borderless row-interaction
/// appearance for anything it does not author itself.</summary>
[PublicAPI]
public sealed record NavigationViewGroupStyle: ControlStyle
{
    /// <summary>Gets the primary navigation-group style definition.</summary>
    internal static StyleDefinition<NavigationViewGroupStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetInteractiveRowStyleSet(),
        Complete,
        static (previous, _, current, _) =>
            previous.ItemIndent != current.ItemIndent
                ? InvalidationImpact.Measure
                : previous != current
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None);

    private static NavigationViewGroupStyle Complete(ControlStyle control, VisualState state) =>
        new(
            control.Face,
            control.Border,
            control.Shadow,
            ControlGlyphs.Navigation.GroupCollapsed.Value,
            ControlGlyphs.Navigation.GroupExpanded.Value,
            itemIndent: 2);

    /// <summary>Initializes a complete navigation-group presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="collapsedGlyph">The printable one-cell collapsed-state indicator.</param>
    /// <param name="expandedGlyph">The printable one-cell expanded-state indicator.</param>
    /// <param name="itemIndent">The non-negative child-item indent in cells.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="itemIndent"/> is negative.</exception>
    [SetsRequiredMembers]
    public NavigationViewGroupStyle(
        Face face,
        Border border,
        Shadow shadow,
        Rune collapsedGlyph,
        Rune expandedGlyph,
        int itemIndent) : base(face, border, shadow)
    {
        CollapsedGlyph = collapsedGlyph;
        ExpandedGlyph = expandedGlyph;
        ItemIndent = itemIndent;
    }

    /// <summary>Gets the standard navigation-group presentation.</summary>
    public static new NavigationViewGroupStyle Default => Complete(ControlStyle.Default, VisualState.Normal);

    /// <summary>Gets the collapsed-state disclosure indicator.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune CollapsedGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the expanded-state disclosure indicator.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune ExpandedGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the child-item indent in terminal cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is negative.</exception>
    public required int ItemIndent
    {
        get;
        init => field = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The item indent must not be negative.");
    }
}
