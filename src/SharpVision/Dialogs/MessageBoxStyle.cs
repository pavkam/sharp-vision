// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using System.Diagnostics.CodeAnalysis;

using Styling;

/// <summary>Defines one complete immutable MessageBox presentation. By design, this style's own
/// "messageBox" theme key falls back to <see cref="WindowStyle"/>'s window appearance
/// (including the ActiveBorder-on-FocusWithin default every Window-derived style shares) for
/// anything it does not author itself. The generated action Buttons and divider Separator resolve
/// their own "button"/"separator" theme keys independently through the established part-style
/// forwarding mechanism rather than nesting inside this aggregate.</summary>
[PublicAPI]
public sealed record MessageBoxStyle: WindowStyle
{
    /// <summary>Gets the primary MessageBox-style definition.</summary>
    internal static StyleDefinition<MessageBoxStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetWindowStyleSet(),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            // Both application sites run only from MeasureOverride, so a member this check omits is
            // never re-applied: nothing schedules the pass that would apply it. The appearance pipeline
            // covers Face/Border/Shadow only, which leaves every other member to this method.
            previous.MessageMargin != current.MessageMargin ||
            previous.ActionBarMargin != current.ActionBarMargin ||
            previous.MessageFace != current.MessageFace ||
            !FaceResolvesEqually(previous.MessageFace, previousTheme, current.MessageFace, currentTheme)
                ? InvalidationImpact.Measure
                : InvalidationImpact.None);

    private static MessageBoxStyle Complete(WindowStyle window, VisualState state, Theme theme) =>
        new(window.Face, window.Border, window.Shadow, window.Face, new Thickness(1, 2, 1, 0), new Thickness(1, 0))
        {
            CloseGlyph = window.CloseGlyph,
            CloseLeftBracket = window.CloseLeftBracket,
            CloseRightBracket = window.CloseRightBracket,
            CloseMarkColor = window.CloseMarkColor,
            CloseMarkActiveColor = window.CloseMarkActiveColor,
            CloseMarkPressedColor = window.CloseMarkPressedColor,
            CloseMarkDisabledColor = window.CloseMarkDisabledColor
        };

    /// <summary>Initializes a complete MessageBox presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="messageFace">The message text face.</param>
    /// <param name="messageMargin">The outer edges around the message text.</param>
    /// <param name="actionBarMargin">The outer edges around the divider and action row.</param>
    [SetsRequiredMembers]
    public MessageBoxStyle(
        Face face,
        Border border,
        Shadow shadow,
        Face messageFace,
        Thickness messageMargin,
        Thickness actionBarMargin) : base(
        face,
        border,
        shadow,
        WindowStyle.Default.CloseGlyph,
        WindowStyle.Default.CloseLeftBracket,
        WindowStyle.Default.CloseRightBracket,
        WindowStyle.Default.CloseMarkColor,
        WindowStyle.Default.CloseMarkActiveColor,
        WindowStyle.Default.CloseMarkPressedColor,
        WindowStyle.Default.CloseMarkDisabledColor)
    {
        MessageFace = messageFace;
        MessageMargin = messageMargin;
        ActionBarMargin = actionBarMargin;
    }

    /// <summary>Gets the standard MessageBox presentation.</summary>
    public static new MessageBoxStyle Default => Complete(WindowStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the message text face.</summary>
    public required Face MessageFace { get; init; }

    /// <summary>Gets the outer edges around the message text.</summary>
    public required Thickness MessageMargin { get; init; }

    /// <summary>Gets the outer edges around the divider and action row.</summary>
    public required Thickness ActionBarMargin { get; init; }

    // Both comparisons are needed, and neither subsumes the other. The raw one catches a local
    // assignment; the resolved one catches a theme swap where two symbolically-identical faces map
    // to different literals. Resolved alone is not enough because with no theme every semantic
    // colour resolves to the same neutral value, so two genuinely different faces compare equal.
    private static bool FaceResolvesEqually(Face previous, Theme? previousTheme, Face current, Theme? currentTheme) =>
        ControlBase.ResolveColor(previous.Foreground, previousTheme) ==
            ControlBase.ResolveColor(current.Foreground, currentTheme) &&
        ControlBase.ResolveColor(previous.Background, previousTheme) ==
            ControlBase.ResolveColor(current.Background, currentTheme) &&
        previous.Underline == current.Underline &&
        previous.Attributes == current.Attributes &&
        ControlBase.ResolveColor(previous.UnderlineColor, previousTheme) ==
            ControlBase.ResolveColor(current.UnderlineColor, currentTheme);
}
