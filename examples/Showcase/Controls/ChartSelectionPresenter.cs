// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Connects one interactive chart to a compact live selection description.</summary>
internal static class ChartSelectionPresenter
{
    /// <summary>Updates the status whenever the chart selection changes and writes its initial state.</summary>
    /// <param name="chart">The retained interactive chart.</param>
    /// <param name="status">The retained status text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="chart"/> or <paramref name="status"/> is null.</exception>
    internal static void Connect(ChartControlBase chart, Text status)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(status);
        chart.SelectionChanged += (_, _) => Refresh(chart, status);
        Refresh(chart, status);
    }

    /// <summary>Refreshes the status after caller-driven point mutation.</summary>
    /// <param name="chart">The retained interactive chart.</param>
    /// <param name="status">The retained status text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="chart"/> or <paramref name="status"/> is null.</exception>
    internal static void Refresh(ChartControlBase chart, Text status)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(status);

        if (chart.Selection is not { } selection)
        {
            status.Content = "No selection · arrows move · Esc clears";
            return;
        }

        var series = chart.Series[selection.SeriesIndex];
        var point = series.Points[selection.PointIndex];
        var label = point.Label.Length == 0
            ? FormattableString.Invariant($"point {selection.PointIndex + 1}")
            : point.Label;
        status.Content = FormattableString.Invariant(
            $"{series.Name} · {label}: {point.Value:0.##}");
    }
}
