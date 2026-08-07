// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Charts;

/// <summary>Verifies observable chart data point validation and notification behavior.</summary>
public sealed class ChartDataPointTests
{
    /// <summary>Verifies a point rejects a non-finite value before changing observable state.</summary>
    [Fact]
    public void Value_WhenAssignedNonFiniteValue_RejectsBeforeMutation()
    {
        // Arrange
        var point = new ChartDataPoint("CPU", 12);
        var notifications = 0;
        point.PropertyChanged += (_, _) => notifications++;

        // Act
        _ = Should.Throw<ArgumentOutOfRangeException>(() => point.Value = double.NaN);

        // Assert
        point.Value.ShouldBe(12);
        notifications.ShouldBe(0);
    }

    /// <summary>Verifies changed labels, values, and colors publish their exact property names.</summary>
    [Fact]
    public void Properties_WhenChanged_PublishObservableNotifications()
    {
        // Arrange
        var point = new ChartDataPoint("CPU", 12);
        var names = new List<string?>();
        point.PropertyChanged += (_, eventArgs) => names.Add(eventArgs.PropertyName);

        // Act
        point.Label = "Memory";
        point.Value = 18;
        point.Color = Color.Rgb(10, 20, 30);

        // Assert
        names.ShouldBe([nameof(ChartDataPoint.Label), nameof(ChartDataPoint.Value), nameof(ChartDataPoint.Color)]);
    }
}
