// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies ProgressBar range, layout, and rendering contracts.</summary>
public sealed class ProgressBarTests
{
    /// <summary>Verifies documented range, presentation, alignment, and interaction defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var bar = new ProgressBar();

        // Assert
        bar.Minimum.ShouldBe(0);
        bar.Maximum.ShouldBe(1);
        bar.Value.ShouldBe(0);
        bar.IsIndeterminate.ShouldBeFalse();
        bar.Orientation.ShouldBe(Orientation.Horizontal);
        bar.UseSubCellResolution.ShouldBeFalse();
        bar.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
        bar.VerticalAlignment.ShouldBe(VerticalAlignment.Stretch);
        bar.CanFocus.ShouldBeFalse();
        bar.IsHitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies finite values clamp while invalid numbers fail before mutation.</summary>
    [Fact]
    public void Value_WhenAssigned_ClampsFiniteValuesAndRejectsNonFiniteValues()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 20, Minimum = 10 };
        bar.Value.ShouldBe(10);

        // Act and assert
        bar.Value = 25;
        bar.Value.ShouldBe(20);
        bar.Value = 5;
        bar.Value.ShouldBe(10);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Value = double.NaN);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Value = double.PositiveInfinity);
        bar.Value.ShouldBe(10);
    }

    /// <summary>Verifies endpoint changes clamp before ordered property notifications.</summary>
    [Fact]
    public void Minimum_WhenRaisedAboveValue_ClampsBeforeNotifications()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 100, Value = 50 };
        List<string?> properties = [];
        bar.PropertyChanged += (_, eventArgs) =>
        {
            bar.Value.ShouldBe(60);
            properties.Add(eventArgs.PropertyName);
        };

        // Act
        bar.Minimum = 60;

        // Assert
        properties.ShouldBe([nameof(ProgressBar.Minimum), nameof(ProgressBar.Value)]);
    }

    /// <summary>Verifies lowering the maximum clamps before ordered property notifications.</summary>
    [Fact]
    public void Maximum_WhenLoweredBelowValue_ClampsBeforeNotifications()
    {
        // Arrange
        var bar = new ProgressBar { Maximum = 100, Value = 80 };
        List<string?> properties = [];
        bar.PropertyChanged += (_, eventArgs) =>
        {
            bar.Value.ShouldBe(60);
            properties.Add(eventArgs.PropertyName);
        };

        // Act
        bar.Maximum = 60;

        // Assert
        properties.ShouldBe([nameof(ProgressBar.Maximum), nameof(ProgressBar.Value)]);
    }

    /// <summary>Verifies invalid endpoints and orientation fail without changing valid state.</summary>
    [Fact]
    public void Setters_WhenValuesAreInvalid_ThrowBeforeMutation()
    {
        // Arrange
        var bar = new ProgressBar();

        // Act
        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Minimum = double.NegativeInfinity);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Maximum = double.NaN);
        _ = Should.Throw<ArgumentException>(() => bar.Minimum = 1);
        _ = Should.Throw<ArgumentException>(() => bar.Maximum = 0);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => bar.Orientation = (Orientation) 99);

        // Assert
        bar.Minimum.ShouldBe(0);
        bar.Maximum.ShouldBe(1);
        bar.Orientation.ShouldBe(Orientation.Horizontal);
        bar.UseSubCellResolution.ShouldBeFalse();
    }

    /// <summary>Verifies sub-cell rendering uses deterministic eighth-cell blocks.</summary>
    [Fact]
    public void Render_WhenSubCellResolutionIsEnabled_UsesFractionalBlock()
    {
        // Arrange
        var bar = new ProgressBar
        {
            Value = 0.5,
            UseSubCellResolution = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        new Engine().Layout(bar, new Size(3, 1));
        using Frame frame = new(new Size(3, 1));

        // Act
        bar.Render(frame.Canvas);

        // Assert
        FrameOracle.Get(frame, default).ShouldBe("█");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("▌");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("░");
    }
}
