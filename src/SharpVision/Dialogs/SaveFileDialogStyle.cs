// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using System.Diagnostics.CodeAnalysis;

using Styling;

/// <summary>Defines one complete immutable SaveFileDialog presentation. This style's own
/// "saveFileDialog" theme key falls back to <see cref="WindowStyle"/>'s window appearance for
/// anything it does not author itself.</summary>
[PublicAPI]
public sealed record SaveFileDialogStyle: FileDialogStyle
{
    /// <summary>Gets the primary SaveFileDialog-style definition.</summary>
    internal static StyleDefinition<SaveFileDialogStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetWindowStyleSet(),
        Complete,
        Compare);

    private static SaveFileDialogStyle Complete(WindowStyle window, VisualState state) =>
        new(window.Face, window.Border, window.Shadow, DefaultRootPadding, contentSpacing: 1, DefaultFileListBorder)
        {
            CloseGlyph = window.CloseGlyph,
            CloseLeftBracket = window.CloseLeftBracket,
            CloseRightBracket = window.CloseRightBracket
        };

    /// <summary>Initializes a complete SaveFileDialog presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="rootPadding">The outer edges around the dialog's retained content.</param>
    /// <param name="contentSpacing">The non-negative spacing between the root content's rows.</param>
    /// <param name="fileListBorder">The complete border around the file-list surface.</param>
    [SetsRequiredMembers]
    public SaveFileDialogStyle(
        Face face,
        Border border,
        Shadow shadow,
        Thickness rootPadding,
        int contentSpacing,
        Border fileListBorder) : base(face, border, shadow, rootPadding, contentSpacing, fileListBorder)
    {
    }

    /// <summary>Gets the standard SaveFileDialog presentation.</summary>
    public static new SaveFileDialogStyle Default => Complete(WindowStyle.Default, VisualState.Normal);

    private static InvalidationImpact Compare(
        SaveFileDialogStyle previous,
        Theme? previousTheme,
        SaveFileDialogStyle current,
        Theme? currentTheme) =>
        // Both application sites run only from MeasureOverride, so a member this Compare omits is
        // never re-applied: nothing schedules the pass that would apply it. The appearance pipeline
        // covers Face/Border/Shadow only, which leaves every other member to this method.
        previous.RootPadding != current.RootPadding ||
        previous.ContentSpacing != current.ContentSpacing ||
        previous.FileListBorder != current.FileListBorder
            ? InvalidationImpact.Measure
            : InvalidationImpact.None;
}
