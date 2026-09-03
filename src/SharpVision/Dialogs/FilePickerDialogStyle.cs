// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using System.Diagnostics.CodeAnalysis;

using Styling;

/// <summary>Defines one complete immutable FilePickerDialog presentation. This style declares no
/// theme section of its own: it falls back to <see cref="WindowStyle"/>'s "window" role section
/// for its passive chrome, resolves its own root padding, content spacing, and file-list border
/// from code-owned defaults, and is themeable only through that fallback and a locally assigned
/// <see cref="FilePickerDialog.Style"/>.</summary>
[PublicAPI]
public sealed record FilePickerDialogStyle: FileDialogStyle
{
    /// <summary>Gets the primary FilePickerDialog-style definition.</summary>
    internal static StyleDefinition<FilePickerDialogStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetWindowStyleSet(),
        Complete,
        static (previous, _, current, _) => RootPaddingOrContentDiffers(previous, current));

    private static FilePickerDialogStyle Complete(WindowStyle window, VisualState state, Theme theme) =>
        new(window.Face, window.Border, window.Shadow, DefaultRootPadding, contentSpacing: 1, DefaultFileListBorder)
        {
            CloseGlyph = window.CloseGlyph,
            CloseLeftBracket = window.CloseLeftBracket,
            CloseRightBracket = window.CloseRightBracket,
            CloseMarkColor = window.CloseMarkColor,
            CloseMarkActiveColor = window.CloseMarkActiveColor,
            CloseMarkPressedColor = window.CloseMarkPressedColor,
            CloseMarkDisabledColor = window.CloseMarkDisabledColor,
            ResizeGripGlyph = window.ResizeGripGlyph,
            ResizeGripColor = window.ResizeGripColor,
            ResizeGripActiveColor = window.ResizeGripActiveColor,
            ResizeGripPressedColor = window.ResizeGripPressedColor,
            ResizeGripDisabledColor = window.ResizeGripDisabledColor
        };

    /// <summary>Initializes a complete FilePickerDialog presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="rootPadding">The outer edges around the dialog's retained content.</param>
    /// <param name="contentSpacing">The non-negative spacing between the root content's rows.</param>
    /// <param name="fileListBorder">The complete border around the file-list surface.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="contentSpacing"/> is negative.</exception>
    [SetsRequiredMembers]
    public FilePickerDialogStyle(
        Face face,
        Border border,
        Shadow shadow,
        Thickness rootPadding,
        int contentSpacing,
        Border fileListBorder) : base(face, border, shadow, rootPadding, contentSpacing, fileListBorder)
    {
    }

    /// <summary>Gets the standard FilePickerDialog presentation.</summary>
    public static new FilePickerDialogStyle Default => Complete(WindowStyle.Default, VisualState.Normal, Theme.Unthemed);
}
