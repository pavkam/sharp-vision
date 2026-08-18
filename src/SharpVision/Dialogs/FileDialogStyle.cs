// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using System.Diagnostics.CodeAnalysis;

using Styling;

/// <summary>Defines the presentation shared by every file-browsing dialog: the frame (falling back
/// to <see cref="WindowStyle"/>), the root content padding, the spacing between rows, and the
/// bordered file-list surface. By design, this type owns nothing beyond structural presentation - the
/// generated action Buttons, the hidden-entry CheckBox, and the generated scrollbars each keep their
/// own independent part-style property (<c>CancelButtonStyle</c>, <c>ShowHiddenCheckBoxStyle</c>,
/// <c>FileListScrollBarStyle</c>, <c>FilterScrollBarStyle</c>, and each dialog's own accept-action
/// style), exactly as already established for style aggregates - the precedence rule is: this aggregate owns the
/// frame and structural geometry, and every named part-style property remains the sole authority for
/// its own control.</summary>
[PublicAPI]
public abstract record FileDialogStyle: WindowStyle
{
    /// <summary>Initializes the shared file-dialog presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="rootPadding">The outer edges around the dialog's retained content.</param>
    /// <param name="contentSpacing">The non-negative spacing between the root content's rows.</param>
    /// <param name="fileListBorder">The complete border around the file-list surface.</param>
    [SetsRequiredMembers]
    protected FileDialogStyle(
        Face face,
        Border border,
        Shadow shadow,
        Thickness rootPadding,
        int contentSpacing,
        Border fileListBorder) : base(
        face,
        border,
        shadow,
        Default.CloseGlyph,
        Default.CloseLeftBracket,
        Default.CloseRightBracket,
        Default.CloseMarkColor,
        Default.CloseMarkActiveColor,
        Default.CloseMarkPressedColor,
        Default.CloseMarkDisabledColor)
    {
        RootPadding = rootPadding;
        ContentSpacing = contentSpacing;
        FileListBorder = fileListBorder;
    }

    /// <summary>Gets the outer edges around the dialog's retained content.</summary>
    public required Thickness RootPadding { get; init; }

    /// <summary>Gets the non-negative spacing between the root content's rows.</summary>
    public required int ContentSpacing { get; init; }

    /// <summary>Gets the complete border around the file-list surface.</summary>
    public required Border FileListBorder { get; init; }

    /// <summary>Gets the established root padding every file dialog used before this style existed.</summary>
    protected static Thickness DefaultRootPadding { get; } = new(left: 1, top: 1, right: 1, bottom: 0);

    /// <summary>Gets the established file-list border every file dialog used before this style existed.</summary>
    protected static Border DefaultFileListBorder { get; } = new(
        BorderSide.All,
        BorderGlyphStyle.Light,
        SemanticColor.ControlBorder,
        Color.Transparent,
        SemanticDecoration.Border);

    /// <summary>Gets the invalidation impact of a change to the shared root padding, content
    /// spacing, or file-list border, for use by every file-browsing dialog's own Definition
    /// comparer.</summary>
    // Both application sites run only from MeasureOverride, so a member this check omits is
    // never re-applied: nothing schedules the pass that would apply it. The appearance pipeline
    // covers Face/Border/Shadow only, which leaves every other member to this method.
    private protected static InvalidationImpact RootPaddingOrContentDiffers(FileDialogStyle previous, FileDialogStyle current) =>
        previous.RootPadding != current.RootPadding ||
        previous.ContentSpacing != current.ContentSpacing ||
        previous.FileListBorder != current.FileListBorder
            ? InvalidationImpact.Measure
            : InvalidationImpact.None;
}
