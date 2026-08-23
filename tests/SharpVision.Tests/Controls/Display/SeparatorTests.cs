// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

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
        new LayoutEngine().Layout(separator, new Size(5, 4));

        // Assert
        separator.DesiredSize.ShouldBe(new Size(1, 1));
    }

    /// <summary>Verifies the built-in horizontal glyph fills the arranged row.</summary>
    [Fact]
    public void Render_WhenHorizontal_FillsTheArrangedRow()
    {
        // Arrange
        var separator = new Separator();
        new LayoutEngine().Layout(separator, new Size(3, 1));
        using Frame frame = new(new Size(3, 1));

        // Act
        separator.Render(frame.Canvas);

        // Assert
        FrameOracle.Get(frame, default).ShouldBe("─");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("─");
    }

    /// <summary>Verifies the built-in vertical glyph fills the arranged column.</summary>
    [Fact]
    public void Render_WhenVertical_FillsTheArrangedColumn()
    {
        // Arrange
        var separator = new Separator { Orientation = Orientation.Vertical };
        new LayoutEngine().Layout(separator, new Size(1, 3));
        using Frame frame = new(new Size(1, 3));

        // Act
        separator.Render(frame.Canvas);

        // Assert
        FrameOracle.Get(frame, default).ShouldBe("│");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("│");
    }

    /// <summary>Verifies the horizontal separator fills only the first row when bounds are multi-row.</summary>
    [Fact]
    public void Render_WhenHorizontalWithMultipleRows_FillsOnlyFirstRow()
    {
        // Arrange
        var separator = new Separator();
        new LayoutEngine().Layout(separator, new Size(3, 3));
        using Frame frame = new(new Size(3, 3));

        // Act
        separator.Render(frame.Canvas);

        // Assert
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("─");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("─");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldNotBe("─");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldNotBe("─");
    }

    /// <summary>Verifies a local style's custom glyphs override defaults and resetting the style restores them.</summary>
    [Fact]
    public void Style_WhenCustomized_OverridesDefaultsAndResetRestores()
    {
        // Arrange
        var separator = new Separator();
        var defaultStyle = separator.ActualStyle;

        // Act
        separator.Style = defaultStyle with { HorizontalGlyph = new Rune('='), VerticalGlyph = new Rune('|') };

        // Assert custom
        separator.ActualStyle.HorizontalGlyph.ShouldBe(new Rune('='));
        separator.ActualStyle.VerticalGlyph.ShouldBe(new Rune('|'));

        // Act reset
        separator.Style = null;

        // Assert restored
        separator.ActualStyle.HorizontalGlyph.ShouldBe(defaultStyle.HorizontalGlyph);
        separator.ActualStyle.VerticalGlyph.ShouldBe(defaultStyle.VerticalGlyph);
    }

    /// <summary>Verifies the default foreground is the border semantic color.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesBorderForeground()
    {
        // Arrange and act
        var separator = new Separator();

        // Assert
        separator.Face.Foreground.ShouldBe(SemanticColor.ControlText);
    }

    /// <summary>Verifies custom glyph rendering uses the override glyph.</summary>
    [Fact]
    public void Render_WhenCustomGlyphIsSet_UsesOverrideGlyph()
    {
        // Arrange
        var separator = new Separator { Style = SeparatorStyle.Default with { HorizontalGlyph = new Rune('=') } };
        new LayoutEngine().Layout(separator, new Size(3, 1));
        using Frame frame = new(new Size(3, 1));

        // Act
        separator.Render(frame.Canvas);

        // Assert
        FrameOracle.Get(frame, default).ShouldBe("=");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("=");
    }

    /// <summary>Verifies disposing the separator succeeds without error.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsOrientationMutation()
    {
        // Arrange
        var separator = new Separator();

        // Act
        separator.Dispose();

        // Assert
        _ = Should.Throw<ObjectDisposedException>(() =>
            separator.Orientation = Orientation.Vertical);
    }

    /// <summary>Verifies direct and ancestor-inherited IsEnabled changes compute the effective
    /// disabled state Separator's mounted disabled contract depends on.</summary>
    [Fact]
    public void IsEnabled_WhenDisabledDirectlyOrByAncestor_ComputesEffectiveState()
    {
        var separator = new Separator();
        separator.EffectiveIsEnabled.ShouldBeTrue();

        separator.IsEnabled = false;
        separator.EffectiveIsEnabled.ShouldBeFalse();

        separator.IsEnabled = true;
        var stack = new Stack { Children = { separator } };
        separator.EffectiveIsEnabled.ShouldBeTrue();

        stack.IsEnabled = false;
        separator.EffectiveIsEnabled.ShouldBeFalse();
    }
}
