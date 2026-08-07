// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Proves table sort events publish internally consistent committed state.</summary>
public sealed class TableSortChangedEventArgsTests
{
    /// <summary>Verifies the no-column sentinel appears exactly when the direction is None.</summary>
    [Theory]
    [InlineData(-1, TableSortDirection.Ascending)]
    [InlineData(0, TableSortDirection.None)]
    public void Constructor_WhenColumnAndDirectionDisagree_Throws(
        int columnIndex,
        TableSortDirection direction)
    {
        var action = () => new TableSortChangedEventArgs(columnIndex, direction);

        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("direction");
    }
}
