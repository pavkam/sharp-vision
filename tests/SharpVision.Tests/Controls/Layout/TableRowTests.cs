// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies a TableRow preserves its validated cell ownership snapshot.</summary>
public sealed class TableRowTests
{
    /// <summary>Verifies a consumer cannot replace a validated cell through the public view.</summary>
    [Fact]
    public void Cells_WhenConsumerAttemptsArrayMutation_PreservesValidatedSnapshot()
    {
        var original = new ControlText("original");
        var replacement = new ControlText("replacement");
        var row = new TableRow([original]);

        if (row.Cells is ControlBase[] mutableCells)
        {
            mutableCells[0] = replacement;
        }

        row.Cells.ShouldHaveSingleItem().ShouldBeSameAs(original);
    }
}
