// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies ProgressBar range, glyph, layout, and rendering contracts.</summary>
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
        bar.FillGlyph.ShouldBe(new Rune('█'));
        bar.TrackGlyph.ShouldBe(new Rune('░'));
        bar.IndeterminateGlyph.ShouldBe(new Rune('▒'));
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

    /// <summary>Verifies invalid endpoints and glyphs fail without changing valid state.</summary>
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
        _ = Should.Throw<ArgumentException>(() => bar.FillGlyph = new Rune('\n'));
        _ = Should.Throw<ArgumentException>(() => bar.TrackGlyph = new Rune('界'));
        _ = Should.Throw<ArgumentException>(() => bar.IndeterminateGlyph = new Rune('\0'));

        // Assert
        bar.Minimum.ShouldBe(0);
        bar.Maximum.ShouldBe(1);
        bar.Orientation.ShouldBe(Orientation.Horizontal);
        bar.FillGlyph.ShouldBe(new Rune('█'));
        bar.TrackGlyph.ShouldBe(new Rune('░'));
        bar.IndeterminateGlyph.ShouldBe(new Rune('▒'));
    }

    /// <summary>Verifies ambiguous custom glyphs degrade by semantic role without mutation.</summary>
    [Fact]
    public void Render_WhenConfiguredGlyphsBecomeWide_UsesPortableFallbacks()
    {
        // Arrange
        var bar = new ProgressBar
        {
            FillGlyph = new Rune('·'),
            TrackGlyph = new Rune('·'),
            IndeterminateGlyph = new Rune('·'),
        };
        bar.SetCellPolicy(new Policy(Ambiguous.Wide));
        new Engine().Layout(bar, new Size(1, 1));
        using Frame frame = new(new Size(1, 1), ambiguousWidth: Ambiguous.Wide);

        // Act and assert track
        bar.Render(frame.Canvas);
        FrameOracle.Get(frame, default).ShouldBe(".");

        // Act and assert fill
        bar.Value = 1;
        bar.Render(frame.Canvas);
        FrameOracle.Get(frame, default).ShouldBe("#");

        // Act and assert indeterminate
        bar.IsIndeterminate = true;
        bar.Render(frame.Canvas);
        FrameOracle.Get(frame, default).ShouldBe("?");
        bar.FillGlyph.ShouldBe(new Rune('·'));
        bar.TrackGlyph.ShouldBe(new Rune('·'));
        bar.IndeterminateGlyph.ShouldBe(new Rune('·'));
    }
}
