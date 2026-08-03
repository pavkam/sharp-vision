// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies ChaseIndicator defaults, validation, layout, and width policy.</summary>
public sealed class ChaseIndicatorTests
{
    /// <summary>Verifies local style ownership overrides Theme fallback and clearing restores it.</summary>
    [Fact]
    public void Style_WhenThemeAndLocalValuesChange_UsesDocumentedPrecedence()
    {
        var theme = ThemeWithControlProfile(ChaseIndicatorStyle.Diamond.Appearance);
        var indicator = new ChaseIndicator();

        indicator.SetTheme(theme);
        indicator.Style.ShouldBeNull();
        indicator.ActualStyle.ShouldBe(ThemeOwnedStyle(theme.Control));
        indicator.Style = ChaseIndicatorStyle.Square;
        indicator.ActualStyle.ShouldBe(ChaseIndicatorStyle.Square);
        indicator.Style = null;
        indicator.ActualStyle.ShouldBe(ThemeOwnedStyle(theme.Control));
    }

    /// <summary>Verifies documented five-cell non-interactive defaults.</summary>
    [ComponentUnitEvidence(typeof(ChaseIndicator))]
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var indicator = new ChaseIndicator();

        // Assert
        indicator.Movement.ShouldBe(ChaseMovement.Bounce);
        indicator.Style.ShouldBeNull();
        indicator.ActualStyle.ShouldBe(ThemeOwnedStyle(Themes.Dark.Control));
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
        indicator.ActualStyle.ShouldBe(ThemeOwnedStyle(Themes.Dark.Control));
        indicator.Movement.ShouldBe(ChaseMovement.Bounce);
        indicator.Interval.ShouldBe(TimeSpan.FromMilliseconds(200));
        indicator.TrailLength.ShouldBe(2);
        indicator.FadeDuration.ShouldBe(TimeSpan.FromMilliseconds(400));
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
    /// <remarks>See #160.</remarks>
    [Fact]
    public void Style_WhenAssignedStyleHasTransparentPartColor_ThrowsBeforeMutation()
    {
        // Arrange
        var indicator = new ChaseIndicator();

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => indicator.Style = new ChaseIndicatorStyle(
            ChaseIndicatorStyle.Default.Active,
            ChaseIndicatorStyle.Default.Inactive,
            Color.Transparent,
            ChaseIndicatorStyle.Default.TrailColor,
            ChaseIndicatorStyle.Default.TrackColor,
            ChaseIndicatorStyle.Default.Appearance));
        _ = Should.Throw<ArgumentException>(() => indicator.Style = new ChaseIndicatorStyle(
            ChaseIndicatorStyle.Default.Active,
            ChaseIndicatorStyle.Default.Inactive,
            ChaseIndicatorStyle.Default.HeadColor,
            Color.Transparent,
            ChaseIndicatorStyle.Default.TrackColor,
            ChaseIndicatorStyle.Default.Appearance));
        _ = Should.Throw<ArgumentException>(() => indicator.Style = new ChaseIndicatorStyle(
            ChaseIndicatorStyle.Default.Active,
            ChaseIndicatorStyle.Default.Inactive,
            ChaseIndicatorStyle.Default.HeadColor,
            ChaseIndicatorStyle.Default.TrailColor,
            Color.Transparent,
            ChaseIndicatorStyle.Default.Appearance));
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
        var indicator = new ChaseIndicator
        {
            Style = new ChaseIndicatorStyle(
                ChaseIndicatorStyle.Default.Active,
                ChaseIndicatorStyle.Default.Inactive,
                Color.Rgb(240, 120, 60),
                Color.Rgb(60, 30, 0),
                Color.Rgb(15, 15, 15),
                ChaseIndicatorStyle.Default.Appearance)
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

    private static Theme ThemeWithControlProfile(ThemeProfile control)
    {
        var theme = new Theme();
        theme.SetProfiles(control, theme.Input, theme.Container, theme.Window, theme.Popup);
        theme.Freeze();
        return theme;
    }

    private static ChaseIndicatorStyle ThemeOwnedStyle(ThemeProfile control) =>
        new(
            ChaseIndicatorStyle.Default.Active,
            ChaseIndicatorStyle.Default.Inactive,
            ChaseIndicatorStyle.Default.HeadColor,
            ChaseIndicatorStyle.Default.TrailColor,
            ChaseIndicatorStyle.Default.TrackColor,
            control);
}
