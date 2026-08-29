// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Scrolling;

/// <summary>Captures one complete host-to-scrollbar-pair synchronization request.</summary>
internal readonly record struct ScrollBarPairConfiguration
{
    /// <summary>Initializes one complete validated-by-caller synchronization request.</summary>
    /// <param name="horizontalMaximum">The horizontal inclusive maximum.</param>
    /// <param name="verticalMaximum">The vertical inclusive maximum.</param>
    /// <param name="horizontalViewport">The horizontal viewport extent.</param>
    /// <param name="verticalViewport">The vertical viewport extent.</param>
    /// <param name="horizontalValue">The horizontal current value.</param>
    /// <param name="verticalValue">The vertical current value.</param>
    /// <param name="horizontalSmallChange">The horizontal small increment.</param>
    /// <param name="verticalSmallChange">The vertical small increment.</param>
    /// <param name="horizontalLargeChange">The horizontal large increment.</param>
    /// <param name="verticalLargeChange">The vertical large increment.</param>
    public ScrollBarPairConfiguration(
        int horizontalMaximum,
        int verticalMaximum,
        int horizontalViewport,
        int verticalViewport,
        int horizontalValue,
        int verticalValue,
        int horizontalSmallChange,
        int verticalSmallChange,
        int horizontalLargeChange,
        int verticalLargeChange)
    {
        HorizontalMaximum = horizontalMaximum;
        VerticalMaximum = verticalMaximum;
        HorizontalViewport = horizontalViewport;
        VerticalViewport = verticalViewport;
        HorizontalValue = horizontalValue;
        VerticalValue = verticalValue;
        HorizontalSmallChange = horizontalSmallChange;
        VerticalSmallChange = verticalSmallChange;
        HorizontalLargeChange = horizontalLargeChange;
        VerticalLargeChange = verticalLargeChange;
    }

    /// <summary>Gets the horizontal inclusive maximum.</summary>
    public int HorizontalMaximum { get; }

    /// <summary>Gets the vertical inclusive maximum.</summary>
    public int VerticalMaximum { get; }

    /// <summary>Gets the horizontal viewport extent.</summary>
    public int HorizontalViewport { get; }

    /// <summary>Gets the vertical viewport extent.</summary>
    public int VerticalViewport { get; }

    /// <summary>Gets the horizontal current value.</summary>
    public int HorizontalValue { get; }

    /// <summary>Gets the vertical current value.</summary>
    public int VerticalValue { get; }

    /// <summary>Gets the horizontal small increment.</summary>
    public int HorizontalSmallChange { get; }

    /// <summary>Gets the vertical small increment.</summary>
    public int VerticalSmallChange { get; }

    /// <summary>Gets the horizontal large increment.</summary>
    public int HorizontalLargeChange { get; }

    /// <summary>Gets the vertical large increment.</summary>
    public int VerticalLargeChange { get; }
}
