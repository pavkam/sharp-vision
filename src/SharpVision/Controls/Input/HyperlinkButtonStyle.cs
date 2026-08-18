// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable hyperlink-button presentation. This style's own
/// "hyperlinkButton" theme key falls back to the standard borderless interactive appearance,
/// defaulting every state to accent-colored underlined text - a hyperlink's identity persists
/// through hover, focus, and press, unlike a resting-only accent.</summary>
[PublicAPI]
public sealed record HyperlinkButtonStyle: ControlStyle
{
    /// <summary>Gets the primary hyperlink-button-style definition. A theme's own
    /// "hyperlinkButton" key can restyle the face directly - the reflective Overlay
    /// mechanism reaches every member of this type uniformly.</summary>
    internal static StyleDefinition<HyperlinkButtonStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetInteractiveControlStyleSet(),
        Complete,
        static (previous, _, current, _) =>
            previous != current ? InvalidationImpact.Render : InvalidationImpact.None);

    private static HyperlinkButtonStyle Complete(ControlStyle control, VisualState state) =>
        new(
            control.Face with
            {
                // Foreground/Attributes are forced only at rest, so hover/focus/disabled keep
                // reacting with the fallback's own state colors, exactly like every other
                // interactive control. Underline/UnderlineColor are forced unconditionally: a
                // hyperlink's underline is its identity and must survive every state, not just
                // Normal - forcing it identically for every state's own completion makes the
                // fallback-delta diff see no underline change to revert, so it carries through
                // from the baked Normal value untouched.
                Foreground = state == VisualState.Normal ? SemanticColor.Accent : control.Face.Foreground,
                Attributes = state == VisualState.Normal ? SemanticDecoration.NormalText : control.Face.Attributes,
                Underline = Underline.Straight,
                UnderlineColor = SemanticColor.Accent
            },
            control.Border,
            control.Shadow);

    /// <summary>Initializes a complete hyperlink-button presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    [SetsRequiredMembers]
    public HyperlinkButtonStyle(Face face, Border border, Shadow shadow) : base(face, border, shadow) { }

    /// <summary>Gets the standard hyperlink-button presentation.</summary>
    public static new HyperlinkButtonStyle Default => Complete(ControlStyle.Default, VisualState.Normal);
}
