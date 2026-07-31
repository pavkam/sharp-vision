// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies GroupBox validation, measurement, content ownership, and intrinsic frame layout.</summary>
public sealed class GroupBoxTests
{
    /// <summary>Verifies defaults and invalid assignments preserve committed public state.</summary>
    [ComponentUnitEvidence(typeof(GroupBox))]
    [Fact]
    public void Properties_WhenCreatedOrAssignedInvalidValue_PreserveValidatedDefaults()
    {
        var group = new GroupBox();

        group.Header.ShouldBeEmpty();
        group.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
        group.ActualBorder.Sides.ShouldBe(BorderSide.All);
        group.Face.Background.ShouldBe(ThemeColor.Surface);
        group.ActualBorder.Foreground.ShouldBe(Color.Default);
        group.Content.ShouldBeNull();

        _ = Should.Throw<ArgumentNullException>(() => group.Header = null!);
        group.Header.ShouldBeEmpty();
        group.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
    }

    /// <summary>Verifies a wide header expands desired width and content occupies the once-inset frame interior.</summary>
    [Fact]
    public void Layout_WhenHeaderIsWideAndContentExists_MeasuresCellsAndInsetsContentOnce()
    {
        var content = new ProbeControl(new Size(3, 2));
        var group = new GroupBox
        {
            Header = "界 Tools",
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        new LayoutEngine().Layout(group, new Size(20, 8));

        group.DesiredSize.ShouldBe(new Size(12, 4));
        group.Bounds.ShouldBe(new Rect(0, 0, 12, 4));
        content.Bounds.ShouldBe(new Rect(1, 1, 10, 2));
    }

    /// <summary>Verifies content replacement releases the previous child and commits the next owned child.</summary>
    [Fact]
    public void Content_WhenReplaced_TransfersTheSingleOwnedSlot()
    {
        var first = new ControlText("First");
        var second = new ControlText("Second");
        var group = new GroupBox { Content = first };

        group.Content = second;

        first.Parent.ShouldBeNull();
        second.Parent.ShouldBeSameAs(group);
        group.Content.ShouldBeSameAs(second);
    }

    /// <summary>Verifies header with control characters is rejected.</summary>
    [Theory]
    [InlineData("bad\nheader")]
    [InlineData("bad\rheader")]
    [InlineData("bad\theader")]
    public void Header_WhenContainsControlCharacters_ThrowsBeforeMutation(string header)
    {
        // Arrange
        var group = new GroupBox();

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => group.Header = header);
        group.Header.ShouldBeEmpty();
    }

    /// <summary>Verifies null content renders only the frame.</summary>
    [Fact]
    public void Layout_WhenContentIsNull_MeasuresHeaderOnly()
    {
        // Arrange
        var group = new GroupBox
        {
            Header = "Empty",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        new LayoutEngine().Layout(group, new Size(20, 8));

        // Assert
        group.Content.ShouldBeNull();
        group.DesiredSize.Width.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies disposing the GroupBox prevents mutation.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsMutation()
    {
        // Arrange
        var group = new GroupBox();

        // Act
        group.Dispose();

        // Assert
        _ = Should.Throw<ObjectDisposedException>(() => group.Header = "Test");
    }

    /// <summary>Verifies retained child shadows cannot replace the owning GroupBox frame.</summary>
    [Fact]
    public void Render_WhenContentShadowTouchesFrame_PaintsFrameAfterContent()
    {
        var content = new Button
        {
            Style = TestButtonStyles.WithShadow(
                AppearanceTestValues.Shadow(mode: ShadowMode.BlockGlyph, offset: new Point(1, 1), glyph: new Rune('▓'))),
        };
        var group = new GroupBox
        {
            Width = Length.Cells(5),
            Height = Length.Cells(4),
            Content = content
        };
        new LayoutEngine().Layout(group, new Size(5, 4));
        using Frame frame = new(new Size(5, 4));

        group.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(4, 2)).ShouldBe("│");
        FrameOracle.Get(frame, new Point(2, 3)).ShouldBe("─");
    }
}
