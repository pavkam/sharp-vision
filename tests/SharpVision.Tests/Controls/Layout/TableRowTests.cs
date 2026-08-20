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

    /// <summary>Verifies a null cell sequence, or a null cell within an otherwise valid sequence,
    /// is rejected.</summary>
    [Fact]
    public void Constructor_WhenCellsOrACellIsNull_ThrowsArgumentNullException()
    {
        _ = Should.Throw<ArgumentNullException>(() => new TableRow(null!));
        _ = Should.Throw<ArgumentNullException>(() => new TableRow([new ControlText("A"), null!]));
    }

    /// <summary>Verifies an empty cell sequence is rejected.</summary>
    [Fact]
    public void Constructor_WhenCellsIsEmpty_ThrowsArgumentException() =>
        _ = Should.Throw<ArgumentException>(() => new TableRow([]));

    /// <summary>Verifies the same control instance repeated in one row is rejected.</summary>
    [Fact]
    public void Constructor_WhenACellRepeats_ThrowsArgumentException()
    {
        var cell = new ControlText("Repeated");

        _ = Should.Throw<ArgumentException>(() => new TableRow([cell, cell]));
    }

    /// <summary>Verifies a cell already owned by another parent is rejected.</summary>
    [Fact]
    public void Constructor_WhenACellIsAlreadyOwned_ThrowsArgumentException()
    {
        var owned = new ControlText("Owned");
        _ = new Stack { Children = { owned } };

        _ = Should.Throw<ArgumentException>(() => new TableRow([owned]));
    }

    /// <summary>Verifies a disposed cell is rejected.</summary>
    [Fact]
    public void Constructor_WhenACellIsDisposed_ThrowsArgumentException()
    {
        var disposed = new ControlText("Disposed");
        disposed.Dispose();

        _ = Should.Throw<ArgumentException>(() => new TableRow([disposed]));
    }
}
