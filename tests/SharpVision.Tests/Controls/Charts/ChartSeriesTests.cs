// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Charts;

using System.Collections.Specialized;

/// <summary>Verifies observable chart series validation and notification behavior.</summary>
public sealed class ChartSeriesTests
{
    /// <summary>Verifies a series rejects an unknown pattern before changing observable state.</summary>
    [Fact]
    public void LinePattern_WhenAssignedUnknownValue_RejectsBeforeMutation()
    {
        // Arrange
        var series = new ChartSeries("CPU") { LinePattern = LinePattern.DoubleDash };
        var notifications = 0;
        series.PropertyChanged += (_, _) => notifications++;

        // Act
        _ = Should.Throw<ArgumentOutOfRangeException>(() => series.LinePattern = (LinePattern) 99);

        // Assert
        series.LinePattern.ShouldBe(LinePattern.DoubleDash);
        notifications.ShouldBe(0);
    }

    /// <summary>Verifies a series rejects a null name before changing observable state.</summary>
    [Fact]
    public void Name_WhenAssignedNull_RejectsBeforeMutation()
    {
        // Arrange
        var series = new ChartSeries("CPU");
        var notifications = 0;
        series.PropertyChanged += (_, _) => notifications++;

        // Act
        _ = Should.Throw<ArgumentNullException>(() => series.Name = null!);

        // Assert
        series.Name.ShouldBe("CPU");
        notifications.ShouldBe(0);
    }

    /// <summary>Verifies a series rejects a transparent literal color override before changing
    /// observable state, since a transparent series could never paint.</summary>
    [Fact]
    public void Color_WhenAssignedTransparent_RejectsBeforeMutation()
    {
        // Arrange
        var color = Color.Rgb(10, 20, 30);
        var series = new ChartSeries("CPU") { Color = color };
        var notifications = 0;
        series.PropertyChanged += (_, _) => notifications++;

        // Act
        _ = Should.Throw<ArgumentException>(() => series.Color = Color.Transparent);

        // Assert
        series.Color.ShouldBe(color);
        notifications.ShouldBe(0);
    }

    /// <summary>Verifies a changed series pattern publishes its exact property name, exactly like
    /// the existing color property.</summary>
    [Fact]
    public void LinePattern_WhenChanged_PublishesObservableNotification()
    {
        // Arrange
        var series = new ChartSeries("CPU");
        var names = new List<string?>();
        series.PropertyChanged += (_, eventArgs) => names.Add(eventArgs.PropertyName);

        // Act
        series.LinePattern = LinePattern.TripleDash;

        // Assert
        names.ShouldBe([nameof(ChartSeries.LinePattern)]);
        series.LinePattern.ShouldBe(LinePattern.TripleDash);
    }

    /// <summary>Verifies re-assigning a name, color, or line pattern to its current value skips notification.</summary>
    [Fact]
    public void Properties_WhenAssignedCurrentValue_SkipsNotification()
    {
        // Arrange
        var color = Color.Rgb(10, 20, 30);
        var series = new ChartSeries("CPU") { Color = color, LinePattern = LinePattern.TripleDash };
        var notifications = 0;
        series.PropertyChanged += (_, _) => notifications++;

        // Act
        series.Name = "CPU";
        series.Color = color;
        series.LinePattern = LinePattern.TripleDash;

        // Assert
        notifications.ShouldBe(0);
        series.Name.ShouldBe("CPU");
        series.Color.ShouldBe(color);
        series.LinePattern.ShouldBe(LinePattern.TripleDash);
    }

    /// <summary>Verifies point membership rejects duplicate references without partial mutation.</summary>
    [Fact]
    public void Add_WhenPointAlreadyExists_RejectsBeforeMutation()
    {
        // Arrange
        var point = new ChartDataPoint("CPU", 12);
        var series = new ChartSeries("Host A");
        series.Points.Add(point);

        // Act
        _ = Should.Throw<ArgumentException>(() => series.Points.Add(point));

        // Assert
        series.Points.ShouldBe([point]);
    }

    /// <summary>Verifies point membership reports deterministic collection changes.</summary>
    [Fact]
    public void Move_WhenPointExists_PublishesMoveNotification()
    {
        // Arrange
        var first = new ChartDataPoint("A", 1);
        var second = new ChartDataPoint("B", 2);
        var series = new ChartSeries("Host A", [first, second]);
        NotifyCollectionChangedEventArgs? observed = null;
        series.Points.CollectionChanged += (_, eventArgs) => observed = eventArgs;

        // Act
        series.Points.Move(0, 1);

        // Assert
        _ = observed.ShouldNotBeNull();
        observed.Action.ShouldBe(NotifyCollectionChangedAction.Move);
        series.Points.ShouldBe([second, first]);
    }
}
