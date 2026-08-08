// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

using System.Reflection;

/// <summary>Verifies GroupBox validation, measurement, content ownership, and intrinsic frame layout.</summary>
public sealed class GroupBoxTests
{
    /// <summary>Verifies defaults and invalid assignments preserve committed public state.</summary>
    [ComponentUnitEvidence(typeof(GroupBox))]
    [Fact]
    public void Properties_WhenCreatedOrAssignedInvalidValue_PreserveValidatedDefaults()
    {
        var group = new GroupBox();

        group.Header.ShouldBeNull();
        group.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
        group.ActualBorder.Sides.ShouldBe(BorderSide.All);
        group.Face.Background.ShouldBe(SemanticColor.Surface);
        group.ActualBorder.Foreground.ShouldBe(Color.Default);
        group.Content.ShouldBeNull();

        _ = Should.Throw<ArgumentNullException>(() => group.HeaderText = null!);
        group.Header.ShouldBeNull();
        group.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
    }

    /// <summary>Verifies a wide header expands desired width and content occupies the once-inset frame interior.</summary>
    [Fact]
    public void Layout_WhenHeaderIsWideAndContentExists_MeasuresCellsAndInsetsContentOnce()
    {
        var content = new ProbeControl(new Size(3, 2));
        var group = new GroupBox
        {
            HeaderText = "界 Tools",
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


    /// <summary>Verifies null content renders only the frame.</summary>
    [Fact]
    public void Layout_WhenContentIsNull_MeasuresHeaderOnly()
    {
        // Arrange
        var group = new GroupBox
        {
            HeaderText = "Empty",
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
        _ = Should.Throw<ObjectDisposedException>(() => group.HeaderText = "Test");
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

    /// <summary>The regression this file exists to pin: the assignment the spec instructs must
    /// compile and take effect.</summary>
    [Fact]
    public void Border_WhenAssignedLocally_IsAuthoritativeOverTheTheme()
    {
        var groupBox = new GroupBox { HeaderText = "Group" };
        var themed = groupBox.ActualBorder;
        var local = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Heavy);

        groupBox.Border = local;

        groupBox.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        groupBox.ActualBorder.GlyphStyle.ShouldNotBe(
            themed.GlyphStyle,
            "the local value must differ from the theme's, or this asserts nothing");
    }

    /// <summary>Verifies the shadow half of the same instruction.</summary>
    [Fact]
    public void Shadow_WhenAssignedLocally_IsAuthoritativeOverTheTheme()
    {
        var groupBox = new GroupBox { HeaderText = "Group" };

        groupBox.ActualShadow.Visible.ShouldBeFalse("a container defaults to no shadow");
        groupBox.Shadow = AppearanceTestValues.Shadow(visible: true, offset: new Point(1, 1));

        groupBox.ActualShadow.Visible.ShouldBeTrue();
        groupBox.ActualShadow.Offset.ShouldBe(new Point(1, 1));
    }

    /// <summary>Verifies the spec's other half - that a local value survives a theme replacement.
    /// Authoring that silently reverted on the next theme swap would satisfy the assertions above
    /// while breaking the documented contract.</summary>
    [Fact]
    public void Border_WhenThemeIsReplaced_KeepsTheLocalValue()
    {
        var groupBox = new GroupBox
        {
            HeaderText = "Group",
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Heavy)
        };

        groupBox.Theme.ShouldBeNull("an unmounted control inherits no theme");
        groupBox.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);

        // Same assertion under an explicitly resolved non-default theme.
        var resolved = ThemeCatalog.White.GetStyleSet(ContainerStyle.Default).Normal.Border;
        resolved.GlyphStyle.ShouldNotBe(BorderGlyphStyle.Heavy, "the probe style must not be the theme's");
        groupBox.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
    }

    /// <summary>Verifies Reset hands ownership back, so the widened surface is the full authoring
    /// contract rather than a one-way door.</summary>
    [Fact]
    public void Reset_WhenLocalChromeWasAssigned_ReturnsOwnershipToTheTheme()
    {
        var groupBox = new GroupBox { HeaderText = "Group" };
        var themed = groupBox.ActualBorder;

        groupBox.Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Heavy);
        groupBox.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);

        groupBox.ResetBorder();
        groupBox.ActualBorder.GlyphStyle.ShouldBe(themed.GlyphStyle);

        groupBox.Shadow = AppearanceTestValues.Shadow(visible: true, offset: new Point(1, 1));
        groupBox.ActualShadow.Visible.ShouldBeTrue();

        groupBox.ResetShadow();
        groupBox.ActualShadow.Visible.ShouldBeFalse();
    }

    /// <summary>The counter-case that keeps the widening honest. It is deliberately on GroupBox and
    /// not on the shared base, because the other headered types own their own presentation:
    /// TabItem is a part of TabControl, and Expander paints no frame. Widening on
    /// <c>HeaderedContentControl</c> would have leaked raw chrome authoring onto both - the same
    /// mistake <c>ChromeAuthoringContainer</c> exists to avoid on <c>Container</c>.</summary>
    [Theory]
    [InlineData(typeof(TabItem))]
    [InlineData(typeof(Expander))]
    [InlineData(typeof(HeaderedContentControl))]
    public void ChromeAuthoring_WhenTypeIsNotAFramedGroupBox_StaysNonPublic(Type type)
    {
        foreach (var name in new[] { "Border", "Shadow" })
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

            property.ShouldBeNull($"{type.Name}.{name} must not be public chrome-authoring surface");
        }
    }

    /// <summary>Verifies the widened members really are the inherited ones rather than a parallel
    /// pair that only looks right - a shadowing property that did not write through would satisfy
    /// every assertion above except what the renderer actually reads.</summary>
    [Fact]
    public void Border_WhenAssignedLocally_ReachesTheRenderedChrome()
    {
        var groupBox = new GroupBox
        {
            HeaderText = "Group",
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Heavy)
        };

        new LayoutEngine().Layout(groupBox, new Size(20, 6));

        groupBox.ActualBorder.Sides.ShouldBe(BorderSide.All);
        groupBox.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
    }

    /// <summary>Verifies a GroupBox's single content child always fills its slot regardless of
    /// Width or alignment, matching Grid's own documented ResolvedAxes.Both contract.</summary>
    [Fact]
    public void GroupBox_WhenContentSetsWidthAndAlignment_StillFillsTheContentSlot()
    {
        var content = new ProbeControl(new Size(2, 1))
        {
            Width = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // GroupBox itself defaults to Left/Top alignment, so as an unstretched root it would
        // shrink-wrap to its own measured content width - itself influenced by content's
        // explicit Width during measure - creating the same circular illusion Expander's own
        // test above must avoid. Stretching GroupBox itself removes that confound.
        var groupBox = new GroupBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = content
        };

        new LayoutEngine().Layout(groupBox, new Size(10, 3));

        // The theme-resolved default border reserves one cell on each side (2 total); content
        // fills the remaining 8 regardless of its own Width or alignment.
        content.Bounds.Width.ShouldBe(8);
    }

    /// <summary>Verifies MaxWidth on a GroupBox's content still caps the otherwise-filled slot.</summary>
    [Fact]
    public void GroupBox_WhenContentSetsMaxWidth_CapsTheFilledContentSlot()
    {
        var content = new ProbeControl(new Size(2, 1)) { MaxWidth = 3 };
        var groupBox = new GroupBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = content
        };

        new LayoutEngine().Layout(groupBox, new Size(10, 3));

        content.Bounds.Width.ShouldBe(3);
    }
}
