// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies GroupBox validation, measurement, content ownership, and intrinsic frame layout.</summary>
public sealed class GroupBoxTests
{
    /// <summary>Verifies defaults and invalid assignments preserve committed public state.</summary>
    [Fact]
    public void Properties_WhenCreatedOrAssignedInvalidValue_PreserveValidatedDefaults()
    {
        var group = new GroupBox();

        group.Header.ShouldBeEmpty();
        group.Glyphs.ShouldBe(Glyphs.Rounded);
        group.BorderThickness.ShouldBe(new Thickness(1));
        group.Background.ShouldBe(ThemeColor.From(ColorRole.Surface));
        group.BorderColor.ShouldBe(ThemeColor.From(ColorRole.Border));
        group.Content.ShouldBeNull();

        _ = Should.Throw<ArgumentNullException>(() => group.Header = null!);
        _ = Should.Throw<ArgumentException>(() => group.Glyphs = default);

        group.Header.ShouldBeEmpty();
        group.Glyphs.ShouldBe(Glyphs.Rounded);
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
            VerticalAlignment = VerticalAlignment.Top,
        };

        new Engine().Layout(group, new Size(20, 8));

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
}
