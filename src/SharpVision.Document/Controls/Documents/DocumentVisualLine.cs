// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents one laid-out document line as a contiguous window into the shared run
/// buffer.</summary>
/// <remarks>
/// Lines index a single flat run list rather than owning their own, so laying out a document
/// allocates two growable buffers in total instead of one per line.
/// </remarks>
internal readonly struct DocumentVisualLine
{
    /// <summary>Initializes a line spanning one window of the shared run buffer.</summary>
    /// <param name="runStart">The non-negative index of this line's first run.</param>
    /// <param name="runCount">The non-negative number of runs on this line.</param>
    /// <param name="cells">The non-negative total cell width of this line.</param>
    public DocumentVisualLine(int runStart, int runCount, int cells)
    {
        Debug.Assert(runStart >= 0, "A line's first run index is non-negative.");
        Debug.Assert(runCount >= 0, "A line's run count is non-negative.");
        Debug.Assert(cells >= 0, "A line's cell width is non-negative.");

        RunStart = runStart;
        RunCount = runCount;
        Cells = cells;
    }

    /// <summary>Gets the index of this line's first run in the shared run buffer.</summary>
    public int RunStart { get; }

    /// <summary>Gets the number of runs on this line.</summary>
    public int RunCount { get; }

    /// <summary>Gets the total cell width of this line.</summary>
    public int Cells { get; }
}
