// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies a TableCellReference validates its identity and resolves its live cell.</summary>
public sealed class TableCellReferenceTests
{
    /// <summary>Verifies the row and column index round-trip through the constructor.</summary>
    [Fact]
    public void Constructor_WhenGivenAValidRowAndColumn_RoundTripsBothValues()
    {
        var row = new TableRow([new ControlText("A"), new ControlText("B")]);

        var reference = new TableCellReference(row, 1);

        reference.Row.ShouldBeSameAs(row);
        reference.ColumnIndex.ShouldBe(1);
    }

    /// <summary>Verifies a null row is rejected.</summary>
    [Fact]
    public void Constructor_WhenRowIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(() => new TableCellReference(null!, 0));

    /// <summary>Verifies a negative column index is rejected.</summary>
    [Fact]
    public void Constructor_WhenColumnIndexIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var row = new TableRow([new ControlText("A")]);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => new TableCellReference(row, -1));
    }

    /// <summary>Verifies Cell resolves the live control at the referenced row and column.</summary>
    [Fact]
    public void Cell_WhenReferenceIsValid_ResolvesTheRetainedControl()
    {
        var second = new ControlText("Second");
        var row = new TableRow([new ControlText("First"), second]);

        var reference = new TableCellReference(row, 1);

        reference.Cell.ShouldBeSameAs(second);
    }

    /// <summary>Verifies Cell throws once the row no longer contains the referenced column.</summary>
    [Fact]
    public void Cell_WhenColumnIndexExceedsRowCells_ThrowsArgumentOutOfRangeException()
    {
        var row = new TableRow([new ControlText("Only")]);
        var reference = new TableCellReference(row, 1);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => reference.Cell);
    }
}
