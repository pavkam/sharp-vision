// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies progressive table placeholder rendering.</summary>
public sealed class TablePlaceholderCellTests
{
    /// <summary>Verifies a huge placeholder surface fills only the visible canvas cells.</summary>
    [Fact]
    public void Render_WhenHugeBoundsAreClipped_CompletesForVisibleCellsOnly()
    {
        var placeholder = new TablePlaceholderCell(new Table(), isError: false)
        {
            Bounds = new Rect(0, 0, int.MaxValue, int.MaxValue)
        };
        using var frame = new Frame(new Size(2, 2));

        Should.NotThrow(() => placeholder.Render(frame.Canvas));

        FrameOracle.Get(frame, new Point(1, 1)).ShouldNotBe(" ");
    }
}
