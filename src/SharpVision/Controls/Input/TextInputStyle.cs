// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable TextInput presentation. This style declares no theme
/// section of its own: it falls back to <see cref="InputStyle"/>'s "input" role section for its
/// complete chrome, has no structural members of its own beyond what <see cref="InputStyle"/>
/// already provides, and is themeable only through that fallback and a locally assigned
/// <see cref="TextInput.Style"/>.</summary>
[PublicAPI]
public sealed record TextInputStyle: InputStyle
{
    /// <summary>Gets the primary TextInput-style definition. Falls back to <see cref="InputStyle"/>'s
    /// "input" role section for its complete chrome and its affix gap.</summary>
    internal static StyleDefinition<TextInputStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(InputStyle.Default),
        Complete,
        static (previous, _, current, _) =>
            previous.AffixGap != current.AffixGap
                ? InvalidationImpact.Measure
                : InvalidationImpact.None);

    private static TextInputStyle Complete(InputStyle input, VisualState state, Theme theme) =>
        new(input.Face, input.Border, input.Shadow)
        {
            // Forwarded from the fallback rather than left at the code-owned value the base
            // constructor supplies. Without this, DropDownGlyph would stay stuck at
            // InputStyle.Default's literal regardless of how a theme customizes "input"'s own
            // dropDownGlyph - a divergence TextInput would never surface visually, since it never
            // draws a dropdown glyph itself.
            DropDownGlyph = input.DropDownGlyph,
            AffixGap = input.AffixGap
        };

    /// <summary>Initializes a complete TextInput presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    [SetsRequiredMembers]
    public TextInputStyle(Face face, Border border, Shadow shadow) : base(face, border, shadow, InputStyle.Default.DropDownGlyph) { }

    /// <summary>Gets the standard bordered TextInput presentation.</summary>
    public static new TextInputStyle Default { get; } = Complete(InputStyle.Default, VisualState.Normal, Theme.Unthemed);
}
