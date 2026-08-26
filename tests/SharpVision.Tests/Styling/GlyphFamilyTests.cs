// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies complete public glyph-family values remain usable when default-constructed.</summary>
public sealed class GlyphFamilyTests
{
    /// <summary>Verifies every complete glyph-family struct resolves zeroed storage to its code-owned family.</summary>
    [Fact]
    public void Default_WhenRead_ResolvesEveryCompleteGlyphFamily()
    {
        var checkBox = default(CheckBoxGlyphs);
        var radioButton = default(RadioButtonGlyphs);
        var slider = default(SliderGlyphs);
        var progressBar = default(ProgressBarGlyphs);
        var chaseIndicator = default(ChaseIndicatorGlyphs);
        var scrollBar = default(ScrollBarGlyphs);
        var popupAnchor = default(PopupAnchorGlyphs);
        var table = default(TableGlyphs);
        var chart = default(ChartGlyphs);

        checkBox.Unchecked.ShouldBe(CheckBoxGlyphs.Default.Unchecked);
        checkBox.Checked.ShouldBe(CheckBoxGlyphs.Default.Checked);
        checkBox.Indeterminate.ShouldBe(CheckBoxGlyphs.Default.Indeterminate);
        radioButton.Unchecked.ShouldBe(RadioButtonGlyphs.Default.Unchecked);
        radioButton.Checked.ShouldBe(RadioButtonGlyphs.Default.Checked);
        slider.HorizontalTrack.ShouldBe(SliderGlyphs.Default.HorizontalTrack);
        slider.HorizontalFill.ShouldBe(SliderGlyphs.Default.HorizontalFill);
        slider.VerticalTrack.ShouldBe(SliderGlyphs.Default.VerticalTrack);
        slider.VerticalFill.ShouldBe(SliderGlyphs.Default.VerticalFill);
        slider.Thumb.ShouldBe(SliderGlyphs.Default.Thumb);
        progressBar.Fill.ShouldBe(ProgressBarGlyphs.Default.Fill);
        progressBar.Track.ShouldBe(ProgressBarGlyphs.Default.Track);
        progressBar.Indeterminate.ShouldBe(ProgressBarGlyphs.Default.Indeterminate);
        chaseIndicator.Active.ShouldBe(new Rune('●'));
        chaseIndicator.Inactive.ShouldBe(new Rune('◯'));
        scrollBar.VerticalDecrement.ShouldBe(ScrollBarGlyphs.Default.VerticalDecrement);
        scrollBar.VerticalIncrement.ShouldBe(ScrollBarGlyphs.Default.VerticalIncrement);
        scrollBar.HorizontalDecrement.ShouldBe(ScrollBarGlyphs.Default.HorizontalDecrement);
        scrollBar.HorizontalIncrement.ShouldBe(ScrollBarGlyphs.Default.HorizontalIncrement);
        scrollBar.BlockTrack.ShouldBe(ScrollBarGlyphs.Default.BlockTrack);
        scrollBar.BlockThumb.ShouldBe(ScrollBarGlyphs.Default.BlockThumb);
        scrollBar.HorizontalLineTrack.ShouldBe(ScrollBarGlyphs.Default.HorizontalLineTrack);
        scrollBar.HorizontalLineThumb.ShouldBe(ScrollBarGlyphs.Default.HorizontalLineThumb);
        scrollBar.VerticalLineTrack.ShouldBe(ScrollBarGlyphs.Default.VerticalLineTrack);
        scrollBar.VerticalLineThumb.ShouldBe(ScrollBarGlyphs.Default.VerticalLineThumb);
        popupAnchor.PointingUp.ShouldBe(PopupAnchorGlyphs.Default.PointingUp);
        popupAnchor.PointingDown.ShouldBe(PopupAnchorGlyphs.Default.PointingDown);
        popupAnchor.PointingLeft.ShouldBe(PopupAnchorGlyphs.Default.PointingLeft);
        popupAnchor.PointingRight.ShouldBe(PopupAnchorGlyphs.Default.PointingRight);
        table.Horizontal.ShouldBe(TableGlyphs.Default.Horizontal);
        table.Vertical.ShouldBe(TableGlyphs.Default.Vertical);
        table.Cross.ShouldBe(TableGlyphs.Default.Cross);
        table.SortAscending.ShouldBe(TableGlyphs.Default.SortAscending);
        table.SortDescending.ShouldBe(TableGlyphs.Default.SortDescending);
        table.Placeholder.ShouldBe(TableGlyphs.Default.Placeholder);
        table.PlaceholderError.ShouldBe(TableGlyphs.Default.PlaceholderError);
        chart.Bar.ShouldBe(ChartGlyphs.Default.Bar);
        chart.Point.ShouldBe(ChartGlyphs.Default.Point);
        chart.Line.ShouldBe(ChartGlyphs.Default.Line);
        chart.Area.ShouldBe(ChartGlyphs.Default.Area);
        chart.LegendMarker.ShouldBe(ChartGlyphs.Default.LegendMarker);
        chart.VerticalAxis.ShouldBe(ChartGlyphs.Default.VerticalAxis);
        chart.HorizontalAxis.ShouldBe(ChartGlyphs.Default.HorizontalAxis);
    }
}
