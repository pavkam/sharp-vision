// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

using SharpVision.Tests.Performance;

/// <summary>Gates eager Table arrow-key navigation at bounded cell-state work per keystroke, using
/// deterministic call-count counters instead of a wall-clock timing budget, which measures the
/// host machine as much as the product and is inherently flaky under CI load or coverage
/// instrumentation.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class TablePerformanceTests
{
    private const int _rowCount = 1_000;
    private const int _columnCount = 4;

    /// <summary>Verifies a single Down-arrow keystroke on a 1,000-row eager Row-selection Table
    /// touches a small, row-count-independent number of cells - not the whole grid, twice
    /// (regression for arrow-key navigation locating the active row with an O(rows) Rows.IndexOf
    /// scan and then repainting every cell in the table via CommitActiveCell and again via
    /// CommitSelection on every keystroke).</summary>
    [Fact]
    public void Navigate_WhenDownArrowMovesActiveRowInLargeTable_TouchesBoundedCellCount()
    {
        var table = BuildTable(_rowCount, _columnCount);
        new LayoutEngine().Layout(table, new Size(40, 20));
        table.SelectRow(table.Rows[0]);

        ControlBase.SetSelectedStateCallCount = 0;
        ControlBase.SetCurrentStateCallCount = 0;

        var eventArgs = Key(table, Code.Down);

        eventArgs.IsHandled.ShouldBeTrue();
        table.ActiveRow.ShouldBeSameAs(table.Rows[1]);

        var totalCalls = ControlBase.SetSelectedStateCallCount + ControlBase.SetCurrentStateCallCount;

        // Bounded by a small multiple of the column count - two cells touched for the active-cell
        // move plus one row leaving and one row joining row selection - never anywhere near the
        // rowCount * columnCount the blanket ApplyCellStates() sweep this change replaces would cost.
        totalCalls.ShouldBeLessThanOrEqualTo((2 * _columnCount) + 2);
    }

    private static Table BuildTable(int rowCount, int columnCount)
    {
        var table = new Table { SelectionMode = TableSelectionMode.Row };

        for (var column = 0; column < columnCount; column++)
        {
            table.Columns.Add(TableColumn.Fixed($"Column{column}", 8));
        }

        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var cells = new ControlBase[columnCount];

            for (var column = 0; column < columnCount; column++)
            {
                cells[column] = new ControlText($"R{rowIndex}C{column}");
            }

            table.Rows.Add(new TableRow(cells));
        }

        return table;
    }

    private static KeyEventArgs Key(Table table, Code code)
    {
        var eventArgs = new KeyEventArgs(new Stroke(code, null, nativeCode: 0, Modifiers.None, KeyAction.Press));
        _ = Router.Route(table, Events.Key, eventArgs);
        return eventArgs;
    }
}
