// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Scrolling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable scrollbar presentation. This style's own
/// "scrollBar" theme key falls back to the standard borderless interactive appearance.</summary>
[PublicAPI]
public sealed record ScrollBarStyle: ControlStyle
{
    /// <summary>Gets the primary scrollbar-style definition. A theme's own "scrollBar" key can
    /// restyle Chrome, Fill, Glyphs, or any color directly - the standalone registrable
    /// "scrollBar" style section this used to read separately is retired; the reflective Overlay
    /// mechanism reaches every member of this type uniformly.</summary>
    internal static StyleDefinition<ScrollBarStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetInteractiveControlStyleSet(),
        Complete,
        Compare);

    // The chrome, fill, and glyph set come from the active theme's glyph family (see
    // themes.md#glyph-families) rather than a literal hardcoded here - GlyphFamily.Default carries
    // the exact Full/Block/ScrollBarGlyphs.Default combination this style used to hardcode
    // directly, so an unthemed resolution is unchanged.
    private static ScrollBarStyle Complete(ControlStyle control, VisualState state, Theme theme) =>
        new(
            control.Face,
            control.Border,
            control.Shadow,
            theme.Glyphs.ScrollBar.Chrome,
            theme.Glyphs.ScrollBar.Fill,
            theme.Glyphs.ScrollBar.Glyphs,
            SemanticColor.Muted,
            SemanticColor.Accent,
            SemanticColor.ControlText);

    /// <summary>Gets the secondary definition used by generated-scrollbar hosts.</summary>
    internal static StyleDefinition<ScrollBarStyle> PartDefinition { get; } = StyleDefinitions.Part(
        static theme => Definition.Resolve(null, theme),
        Compare);

    /// <summary>Gets the non-invalidating definition used by pure forwarding hosts.</summary>
    public static StyleDefinition<ScrollBarStyle> ForwardingDefinition { get; } = StyleDefinitions.Part(
        static theme => Definition.Resolve(null, theme),
        static (_, _, _, _) => InvalidationImpact.None);

    /// <summary>Gets the definition used by an editor whose rail chrome changes arrangement.</summary>
    internal static StyleDefinition<ScrollBarStyle> ArrangePartDefinition { get; } = StyleDefinitions.Part(
        static theme => Definition.Resolve(null, theme),
        static (previous, previousTheme, current, currentTheme) =>
            previous.Chrome != current.Chrome
                ? InvalidationImpact.Arrange
                : Compare(previous, previousTheme, current, currentTheme));

    /// <summary>Initializes a complete scrollbar presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="chrome">The compact or full scrollbar chrome.</param>
    /// <param name="fill">The line or block glyph treatment.</param>
    /// <param name="glyphs">The complete button, track, and thumb glyph family.</param>
    /// <param name="trackColor">The non-transparent unoccupied-track foreground.</param>
    /// <param name="thumbColor">The non-transparent thumb foreground.</param>
    /// <param name="buttonColor">The non-transparent directional-button foreground.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chrome"/> or <paramref name="fill"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A part foreground is transparent.</exception>
    [SetsRequiredMembers]
    public ScrollBarStyle(
        Face face,
        Border border,
        Shadow shadow,
        ScrollBarChrome chrome,
        ScrollBarFill fill,
        ScrollBarGlyphs glyphs,
        ControlColor trackColor,
        ControlColor thumbColor,
        ControlColor buttonColor) : base(face, border, shadow)
    {
        Chrome = chrome;
        Fill = fill;
        Glyphs = glyphs;
        TrackColor = trackColor;
        ThumbColor = thumbColor;
        ButtonColor = buttonColor;
    }

    /// <summary>Gets the standard full block presentation.</summary>
    public static new ScrollBarStyle Default => FullBlock;

    /// <summary>Gets the full button-and-track block presentation.</summary>
    public static ScrollBarStyle FullBlock { get; } = Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the full button-and-track line presentation.</summary>
    public static ScrollBarStyle FullLine { get; } = FullBlock with { Fill = ScrollBarFill.Line };

    /// <summary>Gets the compact track-only block presentation.</summary>
    public static ScrollBarStyle ThinBlock { get; } = FullBlock with { Chrome = ScrollBarChrome.Thin };

    /// <summary>Gets the compact track-only line presentation.</summary>
    public static ScrollBarStyle ThinLine { get; } = FullBlock with { Chrome = ScrollBarChrome.Thin, Fill = ScrollBarFill.Line };

    /// <summary>Gets the compact or full scrollbar chrome.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is undefined.</exception>
    public required ScrollBarChrome Chrome
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);
            field = value;
        }
    }

    /// <summary>Gets the line or block glyph treatment.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is undefined.</exception>
    public required ScrollBarFill Fill
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);
            field = value;
        }
    }

    /// <summary>Gets the complete button, track, and thumb glyph family.</summary>
    public required ScrollBarGlyphs Glyphs { get; init; }

    /// <summary>Gets the unoccupied-track foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor TrackColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the thumb foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor ThumbColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the directional-button foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor ButtonColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    [Pure]
    private static InvalidationImpact Compare(
        ScrollBarStyle previous,
        Theme? previousTheme,
        ScrollBarStyle current,
        Theme? currentTheme) =>
        previous.Chrome != current.Chrome
            ? InvalidationImpact.Measure
            : previous != current ||
              ControlBase.ResolveColor(previous.TrackColor, previousTheme) !=
              ControlBase.ResolveColor(current.TrackColor, currentTheme) ||
              ControlBase.ResolveColor(previous.ThumbColor, previousTheme) !=
              ControlBase.ResolveColor(current.ThumbColor, currentTheme) ||
              ControlBase.ResolveColor(previous.ButtonColor, previousTheme) !=
              ControlBase.ResolveColor(current.ButtonColor, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None;
}
