// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using System.Diagnostics.CodeAnalysis;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Defines one complete immutable collapsible-section presentation. This style declares
/// no theme section of its own: it falls back to the standard borderless interactive appearance
/// for its passive chrome, resolves its own disclosure glyphs and content indent from code-owned
/// defaults, and is themeable only through that fallback and a locally assigned
/// <see cref="Expander.Style"/>.</summary>
[PublicAPI]
public sealed record ExpanderStyle: ControlStyle
{
    /// <summary>Gets the primary expander-style definition. Falls back through
    /// <see cref="Theme.GetInteractiveControlStyleSet"/>; both disclosure glyphs and the content
    /// indent are code-owned.</summary>
    internal static StyleDefinition<ExpanderStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetInteractiveControlStyleSet(),
        Complete,
        static (previous, _, current, _) =>
            previous.ContentIndent != current.ContentIndent
                ? InvalidationImpact.Measure
                : previous != current
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None);

    private static ExpanderStyle Complete(ControlStyle control, VisualState state, Theme theme) =>
        new(
            control.Face with { Background = Color.Transparent },
            control.Border,
            control.Shadow,
            ControlGlyphs.Disclosure.Collapsed.Value,
            ControlGlyphs.Disclosure.Expanded.Value,
            contentIndent: 2);

    /// <summary>Initializes a complete collapsible-section presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="collapsedGlyph">The printable one-cell collapsed-state indicator.</param>
    /// <param name="expandedGlyph">The printable one-cell expanded-state indicator.</param>
    /// <param name="contentIndent">The non-negative content indent in cells.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="contentIndent"/> is negative.</exception>
    [SetsRequiredMembers]
    public ExpanderStyle(
        Face face,
        Border border,
        Shadow shadow,
        Rune collapsedGlyph,
        Rune expandedGlyph,
        int contentIndent) : base(face, border, shadow)
    {
        CollapsedGlyph = collapsedGlyph;
        ExpandedGlyph = expandedGlyph;
        ContentIndent = contentIndent;
    }

    /// <summary>Gets the standard expander presentation.</summary>
    public static new ExpanderStyle Default => Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);

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

    /// <summary>Gets the content indent in terminal cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is negative.</exception>
    [NonNegativeValue]
    public required int ContentIndent
    {
        get;
        init => field = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The content indent must not be negative.");
    }
}
