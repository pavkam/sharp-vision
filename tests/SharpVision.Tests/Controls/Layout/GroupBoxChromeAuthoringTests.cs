// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

using System.Reflection;

/// <summary>Verifies GroupBox exposes the local chrome authoring its own specification instructs a
/// reader to use.
///
/// <para>The spec listed inherited <c>Face</c>, <c>Border</c>, and <c>Shadow</c> as GroupBox API
/// and told the reader to "assign a complete local composite when a particular group needs a
/// different body, border, or shadow". Only <c>Face</c> was public; <c>Border</c> and
/// <c>Shadow</c> are protected on <c>ControlBase</c>, and GroupBox is sealed, so neither
/// assignment compiled and no subclass could reach them. The frame of the one control whose whole
/// purpose is a titled frame could only be changed globally, through the <c>Container</c>
/// style.</para>
///
/// <para>Every other framed control already had this route - <c>Popup</c> and <c>Window</c> through
/// <c>ChromeAuthoringFloatingSurface</c>, the layout panels through
/// <c>ChromeAuthoringContainer</c>. GroupBox derives from <c>HeaderedContentControl</c> and got
/// neither.</para>
/// </summary>
public sealed class GroupBoxChromeAuthoringTests
{
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
}
