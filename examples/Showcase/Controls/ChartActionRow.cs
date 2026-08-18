// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Aligns one chart mutation action with its trailing live status.</summary>
internal sealed class ChartActionRow: CompositeControlBase
{
    /// <summary>Initializes a two-column action and status row.</summary>
    /// <param name="action">The retained action control.</param>
    /// <param name="status">The retained live status text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="status"/> is null.</exception>
    internal ChartActionRow(ControlBase action, Text status)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(status);

        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnSpacing = 2
        };
        grid.Columns.Add(Track.Auto());
        grid.Columns.Add(Track.Star(1, minimum: 1));
        grid.Rows.Add(Track.Auto());
        Grid.SetColumn(action, 0);
        Grid.SetColumn(status, 1);
        status.HorizontalAlignment = HorizontalAlignment.Right;
        status.Margin = new Thickness(0, 1, 0, 0);
        grid.Children.Add(action);
        grid.Children.Add(status);
        InitializeContent(grid);
    }
}
