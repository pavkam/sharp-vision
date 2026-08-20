// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Charts;

/// <summary>Verifies observable chart data point validation and notification behavior.</summary>
public sealed class ChartDataPointTests
{
    /// <summary>Verifies a valid construction round-trips its label and value and defaults to no
    /// color override.</summary>
    [Fact]
    public void Constructor_WhenValid_UsesDocumentedDefaults()
    {
        // Arrange and act
        var point = new ChartDataPoint("CPU", 12);

        // Assert
        point.Label.ShouldBe("CPU");
        point.Value.ShouldBe(12);
        point.Color.ShouldBeNull();
    }

    /// <summary>Verifies a null label is rejected.</summary>
    [Fact]
    public void Constructor_WhenLabelIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => new ChartDataPoint(null!, 1));

    /// <summary>Verifies a non-finite value is rejected.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenValueIsNotFinite_Throws(double value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ChartDataPoint("CPU", value));

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

    /// <summary>Verifies a point rejects a null label before changing observable state.</summary>
    [Fact]
    public void Label_WhenAssignedNull_RejectsBeforeMutation()
    {
        // Arrange
        var point = new ChartDataPoint("CPU", 12);
        var notifications = 0;
        point.PropertyChanged += (_, _) => notifications++;

        // Act
        _ = Should.Throw<ArgumentNullException>(() => point.Label = null!);

        // Assert
        point.Label.ShouldBe("CPU");
        notifications.ShouldBe(0);
    }

    /// <summary>Verifies a point rejects a transparent literal color override before changing
    /// observable state, since a transparent point could never paint.</summary>
    [Fact]
    public void Color_WhenAssignedTransparent_RejectsBeforeMutation()
    {
        // Arrange
        var color = Color.Rgb(10, 20, 30);
        var point = new ChartDataPoint("CPU", 12) { Color = color };
        var notifications = 0;
        point.PropertyChanged += (_, _) => notifications++;

        // Act
        _ = Should.Throw<ArgumentException>(() => point.Color = Color.Transparent);

        // Assert
        point.Color.ShouldBe(color);
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

    /// <summary>Verifies re-assigning a label, value, or color to its current value skips notification.</summary>
    [Fact]
    public void Properties_WhenAssignedCurrentValue_SkipsNotification()
    {
        // Arrange
        var color = Color.Rgb(10, 20, 30);
        var point = new ChartDataPoint("CPU", 12) { Color = color };
        var notifications = 0;
        point.PropertyChanged += (_, _) => notifications++;

        // Act
        point.Label = "CPU";
        point.Value = 12;
        point.Color = color;

        // Assert
        notifications.ShouldBe(0);
        point.Label.ShouldBe("CPU");
        point.Value.ShouldBe(12);
        point.Color.ShouldBe(color);
    }
}
