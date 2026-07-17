// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies ChaseIndicator defaults, validation, layout, and width policy.</summary>
public sealed class ChaseIndicatorTests
{
    /// <summary>Verifies documented five-cell non-interactive defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var indicator = new ChaseIndicator();

        // Assert
        indicator.Pattern.ShouldBe(ChasePattern.Circle);
        indicator.Length.ShouldBe(5);
        indicator.Interval.ShouldBe(TimeSpan.FromMilliseconds(200));
        indicator.IsPlaying.ShouldBeTrue();
        indicator.HorizontalAlignment.ShouldBe(HorizontalAlignment.Left);
        indicator.VerticalAlignment.ShouldBe(VerticalAlignment.Top);
        indicator.CanFocus.ShouldBeFalse();
        indicator.IsHitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies invalid values fail before observable mutation.</summary>
    [Fact]
    public void Setters_WhenValuesAreInvalid_ThrowBeforeMutation()
    {
        // Arrange
        var indicator = new ChaseIndicator();

        // Act
        _ = Should.Throw<ArgumentOutOfRangeException>(() => indicator.Length = 1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            indicator.Pattern = (ChasePattern) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            indicator.Interval = TimeSpan.Zero);

        // Assert
        indicator.Length.ShouldBe(5);
        indicator.Pattern.ShouldBe(ChasePattern.Circle);
        indicator.Interval.ShouldBe(TimeSpan.FromMilliseconds(200));
    }

    /// <summary>Verifies desired width follows the configured track length.</summary>
    [Fact]
    public void Layout_WhenLengthChanges_UsesTrackLengthByOneCell()
    {
        // Arrange
        var indicator = new ChaseIndicator { Length = 7 };

        // Act
        new Engine().Layout(indicator, new Size(10, 3));

        // Assert
        indicator.DesiredSize.ShouldBe(new Size(7, 1));
        indicator.Bounds.Width.ShouldBe(7);
        indicator.Bounds.Height.ShouldBe(1);
    }

    /// <summary>Verifies ambiguous shape glyphs use deterministic one-cell ASCII fallbacks.</summary>
    [Theory]
    [InlineData(ChasePattern.Circle, "@oooo")]
    [InlineData(ChasePattern.Diamond, "*....")]
    [InlineData(ChasePattern.Square, "#....")]
    [InlineData(ChasePattern.Up, "^....")]
    [InlineData(ChasePattern.Down, "v....")]
    [InlineData(ChasePattern.Left, "<....")]
    [InlineData(ChasePattern.Right, ">....")]
    public void Render_WhenAmbiguousWidthIsWide_UsesPortableFallback(
        ChasePattern pattern,
        string expected)
    {
        // Arrange
        var indicator = new ChaseIndicator { Pattern = pattern };
        indicator.SetCellPolicy(new Policy(Ambiguous.Wide));
        new Engine().Layout(indicator, new Size(5, 1));
        using Frame frame = new(new Size(5, 1), ambiguousWidth: Ambiguous.Wide);

        // Act
        indicator.Render(frame.Canvas);

        // Assert
        var actual = string.Concat(
            Enumerable.Range(0, expected.Length)
                .Select(x => FrameOracle.Get(frame, new Point(x, 0))));
        actual.ShouldBe(expected);
    }
}
