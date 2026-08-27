// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies detached navigation-separator policy and style ownership.</summary>
public sealed class NavigationViewSeparatorTests
{
    /// <summary>Verifies separators are one-row stretched decoration, never input targets.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedNonInteractiveDefaults()
    {
        var separator = new NavigationViewSeparator();

        separator.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
        separator.IsHitTestVisible.ShouldBeFalse();
        separator.IsFocusable.ShouldBeFalse();
        separator.CanTabStop.ShouldBeFalse();
        new LayoutEngine().Layout(separator, new Size(9, 3));
        separator.Bounds.Width.ShouldBe(9);
        separator.DesiredSize.ShouldBe(new Size(1, 1));
    }

    /// <summary>Verifies local separator presentation round-trips and disposal closes mutation.</summary>
    [Fact]
    public void Style_WhenAssignedThenDisposed_RoundTripsAndRejectsFurtherMutation()
    {
        var separator = new NavigationViewSeparator();
        var style = NavigationViewSeparatorStyle.Default with { Glyph = new Rune('=') };

        separator.Style = style;

        separator.Style.ShouldBe(style);
        separator.ActualStyle.ShouldBe(style);

        separator.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => separator.Style = null);
    }
}
