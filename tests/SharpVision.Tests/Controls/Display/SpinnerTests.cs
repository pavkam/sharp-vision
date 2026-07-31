// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies Spinner defaults, validation, and layout.</summary>
public sealed class SpinnerTests
{
    /// <summary>Verifies local style ownership overrides Theme fallback and clearing restores it.</summary>
    [Fact]
    public void Style_WhenThemeAndLocalValuesChange_UsesDocumentedPrecedence()
    {
        var theme = ThemeWithControlProfile(SpinnerStyle.Ascii.Appearance);
        var spinner = new Spinner();

        spinner.SetTheme(theme);
        spinner.Style.ShouldBeNull();
        spinner.ActualStyle.ShouldBe(ThemeOwnedStyle(theme.Control));
        spinner.Style = SpinnerStyle.DenseBraille;
        spinner.ActualStyle.ShouldBe(SpinnerStyle.DenseBraille);
        spinner.Style = null;
        spinner.ActualStyle.ShouldBe(ThemeOwnedStyle(theme.Control));
    }

    /// <summary>Verifies documented one-cell non-interactive defaults.</summary>
    [ComponentUnitEvidence(typeof(Spinner))]
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var spinner = new Spinner();

        // Assert
        spinner.Style.ShouldBeNull();
        spinner.ActualStyle.ShouldBe(ThemeOwnedStyle(Themes.Dark.Control));
        spinner.Interval.ShouldBe(TimeSpan.FromMilliseconds(200));
        spinner.IsPlaying.ShouldBeTrue();
        spinner.HorizontalAlignment.ShouldBe(HorizontalAlignment.Left);
        spinner.VerticalAlignment.ShouldBe(VerticalAlignment.Top);
        spinner.CanFocus.ShouldBeFalse();
        spinner.IsHitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies invalid interval values fail before mutation.</summary>
    [Fact]
    public void Setters_WhenValuesAreInvalid_ThrowBeforeMutation()
    {
        // Arrange
        var spinner = new Spinner();

        // Act
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            spinner.Interval = TimeSpan.Zero);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            spinner.Interval = TimeSpan.FromMilliseconds((double) int.MaxValue + 1));

        // Assert
        spinner.Style.ShouldBeNull();
        spinner.ActualStyle.ShouldBe(ThemeOwnedStyle(Themes.Dark.Control));
        spinner.Interval.ShouldBe(TimeSpan.FromMilliseconds(200));
    }

    /// <summary>Verifies intrinsic layout remains exactly one terminal cell.</summary>
    [Fact]
    public void Layout_WhenMeasured_UsesOneCellDesiredSize()
    {
        // Arrange
        var spinner = new Spinner();

        // Act
        new LayoutEngine().Layout(spinner, new Size(8, 3));

        // Assert
        spinner.DesiredSize.ShouldBe(new Size(1, 1));
        new Size(spinner.Bounds.Width, spinner.Bounds.Height).ShouldBe(new Size(1, 1));
    }

    /// <summary>Verifies setting the same style value does not trigger notification or reset the frame.</summary>
    [Fact]
    public void Style_WhenSetToCurrentValue_IsNoOp()
    {
        // Arrange
        var spinner = new Spinner();
        var raised = 0;
        spinner.PropertyChanged += (_, _) => raised++;

        // Act
        spinner.Style = null;

        // Assert
        raised.ShouldBe(0);
    }

    /// <summary>Verifies setting the same interval value does not trigger notification.</summary>
    [Fact]
    public void Interval_WhenSetToCurrentValue_IsNoOp()
    {
        // Arrange
        var spinner = new Spinner();
        var raised = 0;
        spinner.PropertyChanged += (_, _) => raised++;

        // Act
        spinner.Interval = TimeSpan.FromMilliseconds(200);

        // Assert
        raised.ShouldBe(0);
    }

    /// <summary>Verifies setting the same IsPlaying value does not trigger notification.</summary>
    [Fact]
    public void IsPlaying_WhenSetToCurrentValue_IsNoOp()
    {
        // Arrange
        var spinner = new Spinner();
        var raised = 0;
        spinner.PropertyChanged += (_, _) => raised++;

        // Act
        spinner.IsPlaying = true;

        // Assert
        raised.ShouldBe(0);
    }

    /// <summary>Verifies disposing the spinner prevents further mutation.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsMutation()
    {
        // Arrange
        var spinner = new Spinner();

        // Act
        spinner.Dispose();

        // Assert
        _ = Should.Throw<ObjectDisposedException>(() =>
            spinner.Style = SpinnerStyle.Ascii);
        _ = Should.Throw<ObjectDisposedException>(() =>
            spinner.IsPlaying = false);
    }

    private static Theme ThemeWithControlProfile(ThemeProfile control)
    {
        var theme = new Theme();
        theme.SetProfiles(control, theme.Input, theme.Container, theme.Window, theme.Popup);
        theme.Freeze();
        return theme;
    }

    private static SpinnerStyle ThemeOwnedStyle(ThemeProfile control) =>
        new(SpinnerStyle.Default.Frames, control);
}
