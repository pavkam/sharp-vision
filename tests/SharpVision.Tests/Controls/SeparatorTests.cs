// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Separator defaults, validation, layout, and safe glyph behavior.</summary>
public sealed class SeparatorTests
{
    /// <summary>Verifies the divider starts horizontal, one-cell, and non-interactive.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var separator = new Separator();

        // Assert
        separator.Orientation.ShouldBe(Orientation.Horizontal);
        separator.HorizontalGlyph.ShouldBe(new Rune('─'));
        separator.VerticalGlyph.ShouldBe(new Rune('│'));
        separator.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
        separator.VerticalAlignment.ShouldBe(VerticalAlignment.Stretch);
        separator.CanFocus.ShouldBeFalse();
        separator.IsHitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies invalid orientation and glyph values fail before observable mutation.</summary>
    [Fact]
    public void Setters_WhenValuesAreInvalid_ThrowBeforeMutation()
    {
        // Arrange
        var separator = new Separator();

        // Act
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            separator.Orientation = (Orientation) 99);
        _ = Should.Throw<ArgumentException>(() => separator.HorizontalGlyph = new Rune('\n'));
        _ = Should.Throw<ArgumentException>(() => separator.VerticalGlyph = new Rune('界'));

        // Assert
        separator.Orientation.ShouldBe(Orientation.Horizontal);
        separator.HorizontalGlyph.ShouldBe(new Rune('─'));
        separator.VerticalGlyph.ShouldBe(new Rune('│'));
    }

    /// <summary>Verifies either orientation retains a one-cell intrinsic desired size.</summary>
    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void Layout_WhenOrientationChanges_KeepsOneCellIntrinsicSize(Orientation orientation)
    {
        // Arrange
        var separator = new Separator { Orientation = orientation };

        // Act
        new Engine().Layout(separator, new Size(5, 4));

        // Assert
        separator.DesiredSize.ShouldBe(new Size(1, 1));
    }

    /// <summary>Verifies an ambiguous custom glyph degrades without changing its configured value.</summary>
    [Fact]
    public void Render_WhenConfiguredGlyphBecomesWide_UsesPortableFallback()
    {
        // Arrange
        var separator = new Separator { HorizontalGlyph = new Rune('·') };
        separator.SetCellPolicy(new Policy(Ambiguous.Wide));
        new Engine().Layout(separator, new Size(3, 1));
        using Frame frame = new(new Size(3, 1), ambiguousWidth: Ambiguous.Wide);

        // Act
        separator.Render(frame.Canvas);

        // Assert
        FrameOracle.Get(frame, default).ShouldBe("-");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("-");
        separator.HorizontalGlyph.ShouldBe(new Rune('·'));
    }
}
