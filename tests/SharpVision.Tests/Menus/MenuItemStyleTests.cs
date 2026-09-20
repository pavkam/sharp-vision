// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Verifies the immutable MenuItem presentation, including its horizontal-bar inset.</summary>
public sealed class MenuItemStyleTests
{
    /// <summary>Verifies the default reserves one cell on each side and none vertically, matching
    /// the one-cell inset every existing horizontal-bar heading already rendered before this
    /// member existed.</summary>
    [Fact]
    public void Default_WhenRead_UsesOneCellHorizontalPadding()
    {
        var style = MenuItemStyle.Default;

        style.Padding.ShouldBe(new Thickness(horizontal: 1, vertical: 0));
    }

    /// <summary>Verifies a Padding change requires remeasurement, since it moves the caption's
    /// leading and trailing bar-inset columns the same way AffixGap does.</summary>
    [Fact]
    public void Definition_Compare_WhenPaddingChanges_IsMeasure()
    {
        var style = MenuItemStyle.Default;

        MenuItemStyle.Definition.Compare(
                style,
                null,
                style with { Padding = new Thickness(horizontal: 2, vertical: 0) },
                null)
            .ShouldBe(InvalidationImpact.Measure);
    }
}
