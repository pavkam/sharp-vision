// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Records one list item's marker at the line its content begins on.</summary>
/// <remarks>
/// The marker is stored beside the run buffer rather than inside it because an item's first line is
/// produced by recursing into the item's own blocks, which must be free to emit runs without first
/// reserving room for a marker it knows nothing about.
/// </remarks>
internal readonly struct DocumentMarkerPlacement
{
    /// <summary>Initializes a marker placement.</summary>
    /// <param name="line">The non-negative line the marker is drawn on.</param>
    /// <param name="column">The non-negative column the marker starts at.</param>
    /// <param name="text">The non-null resolved marker text.</param>
    /// <param name="foregroundOverride">The enclosing semantic foreground, if any.</param>
    public DocumentMarkerPlacement(
        int line,
        int column,
        string text,
        DocumentFaceKind? foregroundOverride = null)
    {
        Debug.Assert(line >= 0, "A marker sits on a real line.");
        Debug.Assert(column >= 0, "A marker starts at a non-negative column.");
        Debug.Assert(text is not null, "A marker always has resolved text.");

        Line = line;
        Column = column;
        Text = text;
        ForegroundOverride = foregroundOverride;
    }

    /// <summary>Gets the line the marker is drawn on.</summary>
    public int Line { get; }

    /// <summary>Gets the column the marker starts at.</summary>
    public int Column { get; }

    /// <summary>Gets the resolved marker text.</summary>
    public string Text { get; }

    /// <summary>Gets the enclosing semantic face whose foreground paints the marker.</summary>
    public DocumentFaceKind? ForegroundOverride { get; }
}
