// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable JSON syntax and selection presentation. This
/// style's own "jsonView" theme key falls back to <see cref="ContainerStyle"/>'s "container"
/// key for anything it does not author itself.</summary>
[PublicAPI]
public sealed record JsonViewStyle: ContainerStyle
{
    /// <summary>Gets the primary JSON-view style definition. A theme's own "jsonView" key can
    /// restyle any syntax color directly.</summary>
    internal static StyleDefinition<JsonViewStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ContainerStyle.Default),
        Complete,
        Compare);

    private static JsonViewStyle Complete(ContainerStyle container, VisualState state) =>
        new(
            container.Face,
            container.Border,
            container.Shadow,
            SemanticColor.Accent,
            SemanticColor.Info,
            SemanticColor.Success,
            SemanticColor.Info,
            SemanticColor.Warning,
            SemanticColor.Muted,
            SemanticColor.ControlText,
            SemanticColor.Accent,
            ControlGlyphs.Disclosure.Collapsed.Value,
            ControlGlyphs.Disclosure.Expanded.Value,
            SemanticColor.SelectedText,
            SemanticColor.SelectedControl);

    /// <summary>Initializes a complete JSON-view presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="keyColor">The non-transparent object-key foreground.</param>
    /// <param name="indexColor">The non-transparent array-index foreground.</param>
    /// <param name="stringColor">The non-transparent string foreground.</param>
    /// <param name="numberColor">The non-transparent number foreground.</param>
    /// <param name="booleanColor">The non-transparent boolean foreground.</param>
    /// <param name="nullColor">The non-transparent null foreground.</param>
    /// <param name="punctuationColor">The non-transparent punctuation foreground.</param>
    /// <param name="collapsedGlyph">The printable one-cell collapsed-node arrow.</param>
    /// <param name="expandedGlyph">The printable one-cell expanded-node arrow.</param>
    /// <param name="disclosureColor">The non-transparent disclosure foreground.</param>
    /// <param name="selectedTextColor">The non-transparent selected-token foreground.</param>
    /// <param name="selectedBackground">The non-transparent selected-token background.</param>
    /// <exception cref="ArgumentException">A configured color is transparent.</exception>
    [SetsRequiredMembers]
    public JsonViewStyle(
        Face face,
        Border border,
        Shadow shadow,
        ControlColor keyColor,
        ControlColor indexColor,
        ControlColor stringColor,
        ControlColor numberColor,
        ControlColor booleanColor,
        ControlColor nullColor,
        ControlColor punctuationColor,
        ControlColor disclosureColor,
        Rune collapsedGlyph,
        Rune expandedGlyph,
        ControlColor selectedTextColor,
        ControlColor selectedBackground) : base(face, border, shadow)
    {
        KeyColor = keyColor;
        IndexColor = indexColor;
        StringColor = stringColor;
        NumberColor = numberColor;
        BooleanColor = booleanColor;
        NullColor = nullColor;
        PunctuationColor = punctuationColor;
        DisclosureColor = disclosureColor;
        CollapsedGlyph = collapsedGlyph;
        ExpandedGlyph = expandedGlyph;
        SelectedTextColor = selectedTextColor;
        SelectedBackground = selectedBackground;
    }

    /// <summary>Gets the standard JSON-view presentation.</summary>
    public static new JsonViewStyle Default => Complete(ContainerStyle.Default, VisualState.Normal);

    /// <summary>Gets the object-key foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor KeyColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the array-index foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor IndexColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the string foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor StringColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the number foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor NumberColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the boolean foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor BooleanColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the null foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor NullColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the braces, brackets, separators, and comma foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor PunctuationColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the expanded and collapsed disclosure foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor DisclosureColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the arrow drawn for a collapsed container node.</summary>
    /// <remarks>
    /// The glyph participates in layout: it is part of the measured line text, and hit-testing
    /// measures it to compute the clickable span. Replacing it with a different width therefore
    /// moves the clickable region with the drawn glyph rather than letting the two drift.
    /// </remarks>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune CollapsedGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the arrow drawn for an expanded container node.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune ExpandedGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the selected key or index foreground.</summary>
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

    /// <summary>Gets the selected key or index background.</summary>
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

    private static InvalidationImpact Compare(
        JsonViewStyle previous,
        Theme? previousTheme,
        JsonViewStyle current,
        Theme? currentTheme) =>
        previous != current ||
        ControlBase.ResolveColor(previous.KeyColor, previousTheme) != ControlBase.ResolveColor(current.KeyColor, currentTheme) ||
        ControlBase.ResolveColor(previous.IndexColor, previousTheme) != ControlBase.ResolveColor(current.IndexColor, currentTheme) ||
        ControlBase.ResolveColor(previous.StringColor, previousTheme) != ControlBase.ResolveColor(current.StringColor, currentTheme) ||
        ControlBase.ResolveColor(previous.NumberColor, previousTheme) != ControlBase.ResolveColor(current.NumberColor, currentTheme) ||
        ControlBase.ResolveColor(previous.BooleanColor, previousTheme) != ControlBase.ResolveColor(current.BooleanColor, currentTheme) ||
        ControlBase.ResolveColor(previous.NullColor, previousTheme) != ControlBase.ResolveColor(current.NullColor, currentTheme) ||
        ControlBase.ResolveColor(previous.PunctuationColor, previousTheme) != ControlBase.ResolveColor(current.PunctuationColor, currentTheme) ||
        ControlBase.ResolveColor(previous.DisclosureColor, previousTheme) != ControlBase.ResolveColor(current.DisclosureColor, currentTheme) ||
        ControlBase.ResolveColor(previous.SelectedTextColor, previousTheme) != ControlBase.ResolveColor(current.SelectedTextColor, currentTheme) ||
        ControlBase.ResolveColor(previous.SelectedBackground, previousTheme) != ControlBase.ResolveColor(current.SelectedBackground, currentTheme)
            ? InvalidationImpact.Render
            : InvalidationImpact.None;
}
