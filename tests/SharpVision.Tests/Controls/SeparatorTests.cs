// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Separator defaults, validation, layout, and rendering.</summary>
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
        separator.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
        separator.VerticalAlignment.ShouldBe(VerticalAlignment.Stretch);
        separator.CanFocus.ShouldBeFalse();
        separator.IsHitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies invalid orientation fails before observable mutation.</summary>
    [Fact]
    public void Setters_WhenValuesAreInvalid_ThrowBeforeMutation()
    {
        // Arrange
        var separator = new Separator();

        // Act
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            separator.Orientation = (Orientation) 99);

        // Assert
        separator.Orientation.ShouldBe(Orientation.Horizontal);
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

    /// <summary>Verifies the built-in horizontal glyph fills the arranged row.</summary>
    [Fact]
    public void Render_WhenHorizontal_FillsTheArrangedRow()
    {
        // Arrange
        var separator = new Separator();
        new Engine().Layout(separator, new Size(3, 1));
        using Frame frame = new(new Size(3, 1));

        // Act
        separator.Render(frame.Canvas);

        // Assert
        FrameOracle.Get(frame, default).ShouldBe("─");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("─");
    }
}
