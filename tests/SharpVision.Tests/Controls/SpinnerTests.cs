// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Spinner defaults, validation, and layout.</summary>
public sealed class SpinnerTests
{
    /// <summary>Verifies documented one-cell non-interactive defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var spinner = new Spinner();

        // Assert
        spinner.Pattern.ShouldBe(SpinnerPattern.Braille);
        spinner.Interval.ShouldBe(TimeSpan.FromMilliseconds(200));
        spinner.IsPlaying.ShouldBeTrue();
        spinner.HorizontalAlignment.ShouldBe(HorizontalAlignment.Left);
        spinner.VerticalAlignment.ShouldBe(VerticalAlignment.Top);
        spinner.CanFocus.ShouldBeFalse();
        spinner.IsHitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies invalid pattern and interval values fail before mutation.</summary>
    [Fact]
    public void Setters_WhenValuesAreInvalid_ThrowBeforeMutation()
    {
        // Arrange
        var spinner = new Spinner();

        // Act
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            spinner.Pattern = (SpinnerPattern) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            spinner.Interval = TimeSpan.Zero);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            spinner.Interval = TimeSpan.FromMilliseconds((double) int.MaxValue + 1));

        // Assert
        spinner.Pattern.ShouldBe(SpinnerPattern.Braille);
        spinner.Interval.ShouldBe(TimeSpan.FromMilliseconds(200));
    }

    /// <summary>Verifies intrinsic layout remains exactly one terminal cell.</summary>
    [Fact]
    public void Layout_WhenMeasured_UsesOneCellDesiredSize()
    {
        // Arrange
        var spinner = new Spinner();

        // Act
        new Engine().Layout(spinner, new Size(8, 3));

        // Assert
        spinner.DesiredSize.ShouldBe(new Size(1, 1));
        new Size(spinner.Bounds.Width, spinner.Bounds.Height).ShouldBe(new Size(1, 1));
    }
}
