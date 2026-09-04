// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Records one block quote's vertical bar as a line range rather than a run on every line
/// it crosses.</summary>
/// <remarks>
/// A quote's height is only known after its content is laid out, and the bar spans all of it. Storing
/// the range keeps the run buffer strictly append-only during layout - no back-patching of lines that
/// were already emitted - and costs one entry per quote instead of one run per line.
/// </remarks>
internal readonly struct DocumentQuoteBar
{
    /// <summary>Initializes a bar spanning an inclusive line range.</summary>
    /// <param name="firstLine">The non-negative first line the bar occupies.</param>
    /// <param name="lastLine">The inclusive last line the bar occupies.</param>
    /// <param name="column">The non-negative column the bar is drawn in.</param>
    /// <param name="face">The face inherited from the block that owns the bar.</param>
    /// <param name="foregroundOverride">The enclosing semantic foreground, if any.</param>
    public DocumentQuoteBar(
        int firstLine,
        int lastLine,
        int column,
        DocumentFaceKind face,
        DocumentFaceKind? foregroundOverride = null)
    {
        Debug.Assert(firstLine >= 0, "A quote bar starts on a real line.");
        Debug.Assert(lastLine >= firstLine, "A quote bar spans at least one line.");
        Debug.Assert(column >= 0, "A quote bar is drawn at a non-negative column.");

        FirstLine = firstLine;
        LastLine = lastLine;
        Column = column;
        Face = face;
        ForegroundOverride = foregroundOverride;
    }

    /// <summary>Gets the first line the bar occupies.</summary>
    public int FirstLine { get; }

    /// <summary>Gets the inclusive last line the bar occupies.</summary>
    public int LastLine { get; }

    /// <summary>Gets the column the bar is drawn in.</summary>
    public int Column { get; }

    /// <summary>Gets the face inherited from the block that owns the bar.</summary>
    public DocumentFaceKind Face { get; }

    /// <summary>Gets the enclosing semantic face whose foreground paints the bar.</summary>
    public DocumentFaceKind? ForegroundOverride { get; }
}
