// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies ChaseIndicator defaults, validation, layout, and width policy.</summary>
public sealed class ChaseIndicatorTests
{
    /// <summary>Verifies trail storage follows the committed length even when its notification throws.</summary>
    [Fact]
    public void TrailLength_WhenPropertyObserverThrows_StillResizesTrail()
    {
        // Arrange
        var indicator = new ChaseIndicator { TrailLength = 4 };
        indicator.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ChaseIndicator.TrailLength))
            {
                throw new InvalidOperationException("observer failure");
            }
        };

        // Act
        _ = Should.Throw<InvalidOperationException>(() => indicator.TrailLength = 0);

        // Assert
        indicator.TrailLength.ShouldBe(0);
        indicator.TrailCapacity.ShouldBe(0);
    }

    /// <summary>Verifies fade and movement timing are rescheduled from committed values even when
    /// their property notifications throw.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Timing_WhenPropertyObserverThrows_StillReschedules(bool fade)
    {
        // Arrange
        var indicator = new ChaseIndicator { TrailLength = fade ? 2 : 0 };
        var propertyName = fade ? nameof(ChaseIndicator.FadeDuration) : nameof(ChaseIndicator.Interval);
        indicator.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == propertyName)
            {
                throw new InvalidOperationException("observer failure");
            }
        };

        // Act
        _ = Should.Throw<InvalidOperationException>(() =>
        {
            if (fade)
            {
                indicator.FadeDuration = TimeSpan.FromMilliseconds(20);
            }
            else
            {
                indicator.Interval = TimeSpan.FromMilliseconds(300);
            }
        });

        // Assert
        indicator.ScheduledInterval.ShouldBe(TimeSpan.FromMilliseconds(fade ? 20 : 300));
    }

    /// <summary>Verifies local style ownership overrides Theme fallback and clearing restores it.</summary>
    [Fact]
    public void Style_WhenThemeAndLocalValuesChange_UsesDocumentedPrecedence()
    {
        var theme = ThemeCatalog.Dark;
        var indicator = new ChaseIndicator();

        indicator.SetTheme(theme);
        indicator.Style.ShouldBeNull();
        indicator.ActualStyle.ShouldBe(ChaseIndicatorStyle.Definition.Resolve(null, theme));
        indicator.Style = ChaseIndicatorStyle.Square;
        indicator.ActualStyle.ShouldBe(ChaseIndicatorStyle.Square);
        indicator.Style = null;
        indicator.ActualStyle.ShouldBe(ChaseIndicatorStyle.Definition.Resolve(null, theme));
    }

    /// <summary>Verifies documented five-cell non-interactive defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var indicator = new ChaseIndicator();

        // Assert
        indicator.Movement.ShouldBe(ChaseMovement.Bounce);
        indicator.Style.ShouldBeNull();
        indicator.ActualStyle.ShouldBe(ChaseIndicatorStyle.Definition.Resolve(null, ThemeCatalog.Dark));
        indicator.Length.ShouldBe(5);
        indicator.Orientation.ShouldBe(Orientation.Horizontal);
        indicator.Spacing.ShouldBe(0);
        indicator.TrailLength.ShouldBe(2);
        indicator.FadeDuration.ShouldBe(TimeSpan.FromMilliseconds(400));
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
            indicator.Movement = (ChaseMovement) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            indicator.Interval = TimeSpan.Zero);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => indicator.TrailLength = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => indicator.FadeDuration = TimeSpan.Zero);

        // Assert
        indicator.Length.ShouldBe(5);
        indicator.Style.ShouldBeNull();
        indicator.ActualStyle.ShouldBe(ChaseIndicatorStyle.Definition.Resolve(null, ThemeCatalog.Dark));
        indicator.Movement.ShouldBe(ChaseMovement.Bounce);
        indicator.Interval.ShouldBe(TimeSpan.FromMilliseconds(200));
        indicator.TrailLength.ShouldBe(2);
        indicator.FadeDuration.ShouldBe(TimeSpan.FromMilliseconds(400));
    }

    /// <summary>Verifies retained trail storage has a fixed public bound and rejects larger values
    /// before observable state changes.</summary>
    [Fact]
    public void TrailLength_WhenAboveMaximum_ThrowsBeforeMutation()
    {
        var indicator = new ChaseIndicator { Length = int.MaxValue };

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => indicator.TrailLength = ChaseIndicator.MaximumTrailLength + 1);

        indicator.TrailLength.ShouldBe(2);
        indicator.TrailCapacity.ShouldBe(2);
    }

    /// <summary>Verifies the maximum retained trail allocates exactly the bounded capacity.</summary>
    [Fact]
    public void TrailLength_WhenAtMaximum_AllocatesBoundedCapacity()
    {
        var indicator = new ChaseIndicator
        {
            Length = int.MaxValue,
            TrailLength = ChaseIndicator.MaximumTrailLength
        };

        indicator.TrailCapacity.ShouldBe(ChaseIndicator.MaximumTrailLength);
    }

    /// <summary>Verifies desired width follows the configured track length.</summary>
    [Fact]
    public void Layout_WhenLengthChanges_UsesTrackLengthByOneCell()
    {
        // Arrange
        var indicator = new ChaseIndicator { Length = 7 };

        // Act
        new LayoutEngine().Layout(indicator, new Size(10, 3));

        // Assert
        indicator.DesiredSize.ShouldBe(new Size(7, 1));
        indicator.Bounds.Width.ShouldBe(7);
        indicator.Bounds.Height.ShouldBe(1);
    }

    /// <summary>Verifies vertical spacing contributes only to the primary-axis extent.</summary>
    [Fact]
    public void Layout_WhenVerticalAndSpaced_UsesOneCellByComputedExtent()
    {
        // Arrange
        var indicator = new ChaseIndicator { Length = 4, Orientation = Orientation.Vertical, Spacing = 2 };

        // Act
        new LayoutEngine().Layout(indicator, new Size(20, 20));

        // Assert
        indicator.DesiredSize.ShouldBe(new Size(1, 10));
        indicator.Bounds.ShouldBe(new Rect(0, 0, 1, 10));
    }

    /// <summary>Verifies invalid geometry fails before any property changes.</summary>
    [Fact]
    public void GeometrySetters_WhenValuesAreInvalid_ThrowBeforeMutation()
    {
        // Arrange
        var indicator = new ChaseIndicator();

        // Act
        _ = Should.Throw<ArgumentOutOfRangeException>(() => indicator.Spacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            indicator.Orientation = (Orientation) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => indicator.Spacing = int.MaxValue);

        // Assert
        indicator.Orientation.ShouldBe(Orientation.Horizontal);
        indicator.Spacing.ShouldBe(0);
        indicator.Length.ShouldBe(5);
    }

    /// <summary>Verifies ambiguous shape glyphs use deterministic one-cell ASCII fallbacks.</summary>
    [Theory]
    [InlineData("Circle", "*....")]
    [InlineData("Diamond", "*....")]
    [InlineData("Square", "*....")]
    [InlineData("Up", "*....")]
    [InlineData("Down", "*....")]
    [InlineData("Left", "*....")]
    [InlineData("Right", "*....")]
    public void Render_WhenAmbiguousWidthIsWide_UsesPortableFallback(
        string preset,
        string expected)
    {
        // Arrange
        var style = preset switch
        {
            "Circle" => ChaseIndicatorStyle.Circle,
            "Diamond" => ChaseIndicatorStyle.Diamond,
            "Square" => ChaseIndicatorStyle.Square,
            "Up" => ChaseIndicatorStyle.Up,
            "Down" => ChaseIndicatorStyle.Down,
            "Left" => ChaseIndicatorStyle.Left,
            "Right" => ChaseIndicatorStyle.Right,
            _ => throw new UnreachableException()
        };
        var indicator = new ChaseIndicator { Style = style, TrailLength = 0 };
        indicator.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));
        new LayoutEngine().Layout(indicator, new Size(5, 1));
        using Frame frame = new(new Size(5, 1), ambiguousWidth: Ambiguous.Wide);

        // Act
        indicator.Render(frame.Canvas);

        // Assert
        var actual = string.Concat(
            Enumerable.Range(0, expected.Length)
                .Select(x => FrameOracle.Get(frame, new Point(x, 0))));
        actual.ShouldBe(expected);
    }

    /// <summary>Verifies a style with a transparent part color throws before mutation.</summary>
    [Fact]
    public void Style_WhenAssignedStyleHasTransparentPartColor_ThrowsBeforeMutation()
    {
        // Arrange
        var indicator = new ChaseIndicator();

        var baseline = ChaseIndicatorStyle.Default;

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => indicator.Style = new ChaseIndicatorStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            Color.Transparent,
            baseline.TrailColor,
            baseline.TrackColor,
            baseline.Glyphs));
        _ = Should.Throw<ArgumentException>(() => indicator.Style = new ChaseIndicatorStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.HeadColor,
            Color.Transparent,
            baseline.TrackColor,
            baseline.Glyphs));
        _ = Should.Throw<ArgumentException>(() => indicator.Style = new ChaseIndicatorStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.HeadColor,
            baseline.TrailColor,
            Color.Transparent,
            baseline.Glyphs));
        indicator.Style.ShouldBeNull();
    }

    /// <summary>Verifies disposing the chase indicator prevents mutation.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsMutation()
    {
        // Arrange
        var indicator = new ChaseIndicator();

        // Act
        indicator.Dispose();

        // Assert
        _ = Should.Throw<ObjectDisposedException>(() =>
            indicator.Style = ChaseIndicatorStyle.Diamond);
    }

    /// <summary>Verifies setting the same style value does not trigger notification or reset phase.</summary>
    [Fact]
    public void Style_WhenSetToCurrentValue_IsNoOp()
    {
        // Arrange
        var indicator = new ChaseIndicator();
        var raised = 0;
        indicator.PropertyChanged += (_, _) => raised++;

        // Act
        indicator.Style = null;

        // Assert
        raised.ShouldBe(0);
    }

    /// <summary>Verifies concrete color endpoints produce deterministic trail interpolation.</summary>
    [Fact]
    public void Render_WhenTrailUsesConcreteColors_InterpolatesForegroundOnly()
    {
        // Arrange
        var baseline = ChaseIndicatorStyle.Default;
        var indicator = new ChaseIndicator
        {
            Style = new ChaseIndicatorStyle(
                baseline.Face,
                baseline.Border,
                baseline.Shadow,
                Color.Rgb(240, 120, 60),
                Color.Rgb(60, 30, 0),
                Color.Rgb(15, 15, 15),
                baseline.Glyphs)
        };
        new LayoutEngine().Layout(indicator, new Size(5, 1));
        using Frame frame = new(new Size(5, 1));

        // Act
        indicator.Render(frame.Canvas);

        // Assert
        frame.GetCell(new Point(0, 0)).Style.Foreground.ShouldBe(Color.Rgb(240, 120, 60));
        frame.GetCell(new Point(1, 0)).Style.Foreground.ShouldBe(Color.Rgb(240, 120, 60));
        frame.GetCell(new Point(2, 0)).Style.Foreground.ShouldBe(Color.Rgb(150, 75, 30));
        frame.GetCell(new Point(3, 0)).Style.Foreground.ShouldBe(Color.Rgb(15, 15, 15));
    }

    /// <summary>Verifies direct and ancestor-inherited IsEnabled changes compute the effective
    /// disabled state ChaseIndicator's mounted disabled contract depends on.</summary>
    [Fact]
    public void IsEnabled_WhenDisabledDirectlyOrByAncestor_ComputesEffectiveState()
    {
        var indicator = new ChaseIndicator();
        indicator.EffectiveIsEnabled.ShouldBeTrue();

        indicator.IsEnabled = false;
        indicator.EffectiveIsEnabled.ShouldBeFalse();

        indicator.IsEnabled = true;
        var stack = new Stack { Children = { indicator } };
        indicator.EffectiveIsEnabled.ShouldBeTrue();

        stack.IsEnabled = false;
        indicator.EffectiveIsEnabled.ShouldBeFalse();
    }
}
