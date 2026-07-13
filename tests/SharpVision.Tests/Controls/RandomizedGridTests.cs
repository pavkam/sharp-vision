// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;



/// <summary>Proves seeded Grid geometry invariants across hostile valid inputs.</summary>
public sealed class RandomizedGridTests
{
    private const int _caseCount = 10_000;
    private const int _seed = 0x051A_475A;

    /// <summary>Verifies mixed tracks, spans, visibility, and tiny sizes remain exact.</summary>
    [Fact]
    public void Layout_WhenGridsAreRandomized_RemainsDeterministicAndContained()
    {
        Random seeds = new(_seed);

        for (int sample = 0; sample < _caseCount; sample++)
        {
            int caseSeed = seeds.Next();
            Grid first = CreateGrid(new Random(caseSeed), out List<ProbeControl>? firstColumns, out List<ProbeControl>? firstRows);
            Grid second = CreateGrid(new Random(caseSeed), out List<ProbeControl>? secondColumns, out List<ProbeControl>? secondRows);
            Size size = new(caseSeed % 31, caseSeed / 31 % 17);
            string context = $"seed=0x{_seed:X8}, case={sample}, caseSeed={caseSeed}, size={size}";
            Engine engine = new();

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
        int consumed = 0;
        int previousEnd = 0;

        for (int index = 0; index < controls.Count; index++)
        {
            Rect bounds = controls[index].Bounds;
            int origin = horizontal ? bounds.X : bounds.Y;
            int extent = horizontal ? bounds.Width : bounds.Height;
            origin.ShouldBeGreaterThanOrEqualTo(previousEnd, context);
            extent.ShouldBeGreaterThanOrEqualTo(0, context);

            if (index > 0)
            {
                int gap = origin - previousEnd;
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
        foreach (Control child in grid.Children)
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
        int columnCount = random.Next(1, 6);
        int rowCount = random.Next(1, 6);
        Grid grid = new()
        {
            ColumnSpacing = random.Next(0, 4),
            RowSpacing = random.Next(0, 4),
        };

        AddDefinitions(random, grid.Columns, columnCount);
        AddDefinitions(random, grid.Rows, rowCount);
        columnControls = [];
        rowControls = [];

        for (int column = 0; column < columnCount; column++)
        {
            ProbeControl child = new(new Size(random.Next(0, 9), random.Next(0, 5)));
            Grid.SetColumn(child, column);
            grid.Children.Add(child);
            columnControls.Add(child);
        }

        for (int row = 0; row < rowCount; row++)
        {
            ProbeControl child = new(new Size(random.Next(0, 9), random.Next(0, 5)));
            Grid.SetRow(child, row);
            grid.Children.Add(child);
            rowControls.Add(child);
        }

        int extraCount = random.Next(0, 6);

        for (int index = 0; index < extraCount; index++)
        {
            int row = random.Next(rowCount);
            int column = random.Next(columnCount);
            ProbeControl child = new(new Size(random.Next(0, 17), random.Next(0, 9)))
            {
                Visibility = random.Next(0, 5) == 0 ? Visibility.Collapsed : Visibility.Visible,
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
        for (int index = 0; index < count; index++)
        {
            if (index == count - 1)
            {
                collection.Add(Track.Star(random.NextDouble() + 0.01));
                continue;
            }

            int minimum = random.Next(0, 4);
            int maximum = random.Next(minimum, minimum + 10);
            Track track = random.Next(0, 4) switch
            {
                0 => Track.Auto(minimum, maximum),
                1 => Track.Cells(random.Next(0, 13), minimum, maximum),
                2 => Track.Percent(random.NextDouble() * 100, minimum, maximum),
                _ => Track.Star(random.NextDouble() + 0.01, minimum, maximum),
            };
            collection.Add(track);
        }
    }
}
