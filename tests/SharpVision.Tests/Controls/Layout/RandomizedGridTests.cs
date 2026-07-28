// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Proves seeded Grid geometry invariants across hostile valid inputs.</summary>
public sealed class RandomizedGridTests
{
    private const int _caseCount = 10_000;
    private const int _seed = 0x051A_475A;

    /// <summary>Verifies mixed tracks, spans, visibility, and tiny sizes remain exact.</summary>
    [Fact]
    public void Layout_WhenGridsAreRandomized_RemainsDeterministicAndContained()
    {
        var seeds = new Random(_seed);

        for (var sample = 0; sample < _caseCount; sample++)
        {
            var caseSeed = seeds.Next();
            var first = CreateGrid(new Random(caseSeed), out var firstColumns, out var firstRows);
            var second = CreateGrid(new Random(caseSeed), out var secondColumns, out var secondRows);
            var size = new Size(caseSeed % 31, caseSeed / 31 % 17);
            var context = $"seed=0x{_seed:X8}, case={sample}, caseSeed={caseSeed}, size={size}";
            var engine = new Engine();

            engine.Layout(first, size);
            engine.Layout(second, size);

            AssertAxis(firstColumns, first.ColumnSpacing, size.Width, horizontal: true, context);
            AssertAxis(firstRows, first.RowSpacing, size.Height, horizontal: false, context);
            AssertContained(first, context);
            first.Bounds.ShouldBe(second.Bounds, context);
            first.DesiredSize.ShouldBe(second.DesiredSize, context);
            firstColumns.Select(static child => child.Bounds)
                .ShouldBe(secondColumns.Select(static child => child.Bounds), context);
            firstRows.Select(static child => child.Bounds)
                .ShouldBe(secondRows.Select(static child => child.Bounds), context);
        }
    }

    private static void AssertAxis(
        List<ProbeControl> controls,
        int spacing,
        int available,
        bool horizontal,
        string context)
    {
        var consumed = 0;
        var previousEnd = 0;

        for (var index = 0; index < controls.Count; index++)
        {
            var bounds = controls[index].Bounds;
            var origin = horizontal ? bounds.X : bounds.Y;
            var extent = horizontal ? bounds.Width : bounds.Height;
            origin.ShouldBeGreaterThanOrEqualTo(previousEnd, context);
            extent.ShouldBeGreaterThanOrEqualTo(0, context);

            if (index > 0)
            {
                var gap = origin - previousEnd;
                gap.ShouldBeLessThanOrEqualTo(spacing, context);
                consumed += gap;
            }

            consumed += extent;
            previousEnd = origin + extent;
        }

        consumed.ShouldBe(available, context);
    }

    private static void AssertContained(Grid grid, string context)
    {
        foreach (var child in grid.Children)
        {
            child.Bounds.Width.ShouldBeGreaterThanOrEqualTo(0, context);
            child.Bounds.Height.ShouldBeGreaterThanOrEqualTo(0, context);
            child.Bounds.X.ShouldBeGreaterThanOrEqualTo(grid.Bounds.X, context);
            child.Bounds.Y.ShouldBeGreaterThanOrEqualTo(grid.Bounds.Y, context);
            child.Bounds.Right.ShouldBeLessThanOrEqualTo(grid.Bounds.Right, context);
            child.Bounds.Bottom.ShouldBeLessThanOrEqualTo(grid.Bounds.Bottom, context);
        }
    }

    private static Grid CreateGrid(
        Random random,
        out List<ProbeControl> columnControls,
        out List<ProbeControl> rowControls)
    {
        var columnCount = random.Next(1, 6);
        var rowCount = random.Next(1, 6);
        var grid = new Grid { ColumnSpacing = random.Next(0, 4), RowSpacing = random.Next(0, 4) };

        AddDefinitions(random, grid.Columns, columnCount);
        AddDefinitions(random, grid.Rows, rowCount);
        columnControls = [];
        rowControls = [];

        for (var column = 0; column < columnCount; column++)
        {
            var child = new ProbeControl(new Size(random.Next(0, 9), random.Next(0, 5)));
            Grid.SetColumn(child, column);
            grid.Children.Add(child);
            columnControls.Add(child);
        }

        for (var row = 0; row < rowCount; row++)
        {
            var child = new ProbeControl(new Size(random.Next(0, 9), random.Next(0, 5)));
            Grid.SetRow(child, row);
            grid.Children.Add(child);
            rowControls.Add(child);
        }

        var extraCount = random.Next(0, 6);

        for (var index = 0; index < extraCount; index++)
        {
            var row = random.Next(rowCount);
            var column = random.Next(columnCount);
            var child = new ProbeControl(new Size(random.Next(0, 17), random.Next(0, 9)))
            {
                Visibility = random.Next(0, 5) == 0 ? Visibility.Collapsed : Visibility.Visible
            };
            Grid.SetRow(child, row);
            Grid.SetColumn(child, column);
            Grid.SetRowSpan(child, random.Next(1, rowCount - row + 1));
            Grid.SetColumnSpan(child, random.Next(1, columnCount - column + 1));
            grid.Children.Add(child);
        }

        return grid;
    }

    private static void AddDefinitions(Random random, TrackCollection collection, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (index == count - 1)
            {
                collection.Add(Track.Star(random.NextDouble() + 0.01));
                continue;
            }

            var minimum = random.Next(0, 4);
            var maximum = random.Next(minimum, minimum + 10);
            var track = random.Next(0, 4) switch
            {
                0 => Track.Auto(minimum, maximum),
                1 => Track.Cells(random.Next(0, 13), minimum, maximum),
                2 => Track.Percent(random.NextDouble() * 100, minimum, maximum),
                _ => Track.Star(random.NextDouble() + 0.01, minimum, maximum)
            };
            collection.Add(track);
        }
    }
}
