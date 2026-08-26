// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies GroupBox validation, measurement, content ownership, and intrinsic frame layout.</summary>
public sealed class GroupBoxTests
{
    /// <summary>Verifies defaults and invalid assignments preserve committed public state.</summary>
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

    /// <summary>Verifies the retained header's relative inset saturates within a parent whose
    /// right edge reaches the integer coordinate limit.</summary>
    [Fact]
    public void Arrange_WhenBoundsSaturateAtIntegerMaximum_KeepsHeaderOrdered()
    {
        // Arrange
        var header = new ControlText("Header");
        var group = new GroupBox { Header = header };
        group.Measure(new Constraint(10, 3));

        // Act
        group.Arrange(new Rect(int.MaxValue - 1, 0, 10, 3));

        // Assert
        header.Bounds.X.ShouldBeGreaterThanOrEqualTo(group.Bounds.X);
        header.Bounds.Right.ShouldBeLessThanOrEqualTo(group.Bounds.Right);
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

        groupBox.ActualShadow.IsVisible.ShouldBeFalse("a container defaults to no shadow");
        groupBox.Shadow = AppearanceTestValues.Shadow(visible: true, offset: new Point(1, 1));

        groupBox.ActualShadow.IsVisible.ShouldBeTrue();
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
        groupBox.ActualShadow.IsVisible.ShouldBeTrue();

        groupBox.ResetShadow();
        groupBox.ActualShadow.IsVisible.ShouldBeFalse();
    }

    /// <summary>The counter-case that keeps the capability gate honest. It is deliberately proven
    /// on GroupBox and not on the shared base, because the other controls with a header own their
    /// own presentation: TabItem is a part of TabControl (and, unlike GroupBox and Expander, does
    /// not even derive from <c>HeaderedContentControl</c>), and Expander paints no frame. Calling
    /// <c>EnableChromeAuthoring()</c> from <c>HeaderedContentControl</c> would have leaked raw
    /// chrome authoring onto Expander - the same mistake the per-control opt-in exists to avoid.
    /// <c>Border</c>/<c>Shadow</c> are public on every <see cref="ControlBase"/> now (closing the
    /// member-hiding hazard means there can only be one declaration of each), so the invariant this
    /// proves shifted from "the member does not exist" to "the member throws until the owning
    /// control opts in": a type that never calls <c>EnableChromeAuthoring()</c> exposes a public
    /// property that still refuses to be read or written.</summary>
    [Theory]
    [InlineData(typeof(TabItem))]
    [InlineData(typeof(Expander))]
    [InlineData(typeof(ProbeHeaderedContentControl))]
    public void ChromeAuthoring_WhenTypeIsNotAFramedGroupBox_ThrowsWithoutTheCapability(Type type)
    {
        var control = (ControlBase) Activator.CreateInstance(type)!;

        _ = Should.Throw<InvalidOperationException>(() => _ = control.Border);
        _ = Should.Throw<InvalidOperationException>(() => _ = control.Shadow);
    }

    /// <summary>Verifies chrome authoring, once enabled, really reaches what the renderer reads
    /// rather than only satisfying the property assignment itself.</summary>
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

    /// <summary>Verifies collapsed content contributes nothing to measurement - the frame measures
    /// to exactly the same header-only geometry a GroupBox with no content at all would produce.</summary>
    [Fact]
    public void Layout_WhenContentIsCollapsed_MeasuresHeaderOnlyGeometry()
    {
        var content = new ProbeControl(new Size(20, 10)) { Visibility = Visibility.Collapsed };
        var group = new GroupBox
        {
            HeaderText = "Tools",
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var empty = new GroupBox
        {
            HeaderText = "Tools",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        new LayoutEngine().Layout(group, new Size(20, 8));
        new LayoutEngine().Layout(empty, new Size(20, 8));

        group.DesiredSize.ShouldBe(empty.DesiredSize);
        group.DesiredSize.Height.ShouldBe(2, "only the top and bottom border rows remain");
        content.Bounds.ShouldBe(default);
        content.Parent.ShouldBeSameAs(group);
    }

    /// <summary>Verifies hidden content keeps contributing to measurement and still receives its
    /// filled arranged slot, identical to visible content of the same intrinsic size.</summary>
    [Fact]
    public void Layout_WhenContentIsHidden_RetainsDesiredSizeAndArrangedSlot()
    {
        var content = new ProbeControl(new Size(4, 2)) { Visibility = Visibility.Hidden };
        var group = new GroupBox
        {
            HeaderText = "Tools",
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        new LayoutEngine().Layout(group, new Size(20, 8));

        group.DesiredSize.ShouldBe(new Size(9, 4));
        content.Bounds.ShouldBe(new Rect(1, 1, 7, 2));
    }

    /// <summary>Verifies hidden content renders no cells of its own - the content region carries only
    /// the GroupBox's own opaque fill, not the content's rune - and is excluded from hit-testing,
    /// while DesiredSize and the arranged slot stay exactly what the sibling test above proves for
    /// the same intrinsic size. Completes the layout-only proof above with the render and hit-test
    /// halves Expander's own Hidden proof already covers.</summary>
    [Fact]
    public void Render_WhenContentIsHidden_ExcludesRenderingAndHitTestingButKeepsGeometry()
    {
        var content = new ProbeControl(new Size(4, 2))
        {
            Content = "H".AsMemory(),
            Visibility = Visibility.Hidden
        };
        var group = new GroupBox
        {
            HeaderText = "Tools",
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        group.SetTheme(ThemeCatalog.Dark);

        new LayoutEngine().Layout(group, new Size(20, 8));

        group.DesiredSize.ShouldBe(new Size(9, 4));
        content.Bounds.ShouldBe(new Rect(1, 1, 7, 2));
        using Frame frame = new(new Size(9, 4));

        group.Render(frame.Canvas);

        // The content's own rune never reaches the frame; the cell carries only the GroupBox's
        // own opaque fill - blank, and colored with its own surface rather than anything the
        // hidden content would have painted.
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBeEmpty();
        frame.GetCell(new Point(1, 1)).Style.Background.ShouldBe(ThemeCatalog.Dark.ResolveColor(SemanticColor.Surface));
        content.HitTest(new Point(1, 1)).ShouldBeNull();
        group.HitTest(new Point(1, 1)).ShouldBeSameAs(
            group,
            "an ineligible hidden child falls through to the group's own bounds");
    }

    /// <summary>Verifies toggling content between IsVisible and Collapsed at runtime invalidates and
    /// restores exactly the same geometry each time, rather than drifting.</summary>
    [Fact]
    public void Layout_WhenContentTogglesCollapsedThenVisible_InvalidatesAndRestoresGeometry()
    {
        var content = new ProbeControl(new Size(4, 2));
        var group = new GroupBox
        {
            HeaderText = "Tools",
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var engine = new LayoutEngine();

        engine.Layout(group, new Size(20, 8));
        var expandedSize = group.DesiredSize;
        var expandedContentBounds = content.Bounds;

        content.Visibility = Visibility.Collapsed;
        engine.Layout(group, new Size(20, 8));

        group.DesiredSize.Height.ShouldBe(2);
        content.Bounds.ShouldBe(default);

        content.Visibility = Visibility.Visible;
        engine.Layout(group, new Size(20, 8));

        group.DesiredSize.ShouldBe(expandedSize);
        content.Bounds.ShouldBe(expandedContentBounds);
    }

    /// <summary>Verifies direct and ancestor-inherited IsEnabled changes compute the effective
    /// disabled state GroupBox's mounted disabled contract depends on, including the state its
    /// composed content inherits.</summary>
    [Fact]
    public void IsEnabled_WhenDisabledDirectlyOrByAncestor_ComputesEffectiveState()
    {
        var content = new ControlText("Body");
        var group = new GroupBox { Content = content };
        group.EffectiveIsEnabled.ShouldBeTrue();
        content.EffectiveIsEnabled.ShouldBeTrue();

        group.IsEnabled = false;
        group.EffectiveIsEnabled.ShouldBeFalse();
        content.EffectiveIsEnabled.ShouldBeFalse();

        group.IsEnabled = true;
        var stack = new Stack { Children = { group } };
        group.EffectiveIsEnabled.ShouldBeTrue();

        stack.IsEnabled = false;
        group.EffectiveIsEnabled.ShouldBeFalse();
        content.EffectiveIsEnabled.ShouldBeFalse();
    }
}
