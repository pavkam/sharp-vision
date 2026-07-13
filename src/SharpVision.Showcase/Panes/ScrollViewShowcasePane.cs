// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Layout;

/// <summary>Documents and demonstrates the ScrollView control.</summary>
internal sealed class ScrollViewShowcasePane: ShowcasePane
{
    internal const string Title = "ScrollView";
    private const string _catalogSummary =
        "Hosts one child in a cell viewport with automatic bars, nested wheel propagation, and bring-into-view.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Arrows and Page keys", "Move the focused viewport", "Offsets change by LineSize or page distance while remaining clamped."),
        new InteractionDescription("Home or End", "Jump to an extent endpoint", "The selected axis offset moves to its minimum or maximum."),
        new InteractionDescription("Wheel", "Scroll over nested content", "The nearest view consumes applicable delta and propagates only unused movement."),
        new InteractionDescription("Bring into view", "Focus or request a descendant rectangle", "Offsets adjust until the target is visible inside the committed viewport."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Content", "Control?", "null", "Owns the single scrollable child measured against the enabled unbounded axes."),
        new PropertyDescription("HorizontalBarVisibility", "ScrollBarVisibility", "Auto", "Shows, hides, disables, or automatically reserves the horizontal bar."),
        new PropertyDescription("VerticalBarVisibility", "ScrollBarVisibility", "Auto", "Shows, hides, disables, or automatically reserves the vertical bar."),
        new PropertyDescription("ConstrainContentToViewport", "bool", "false", "Supplies the finite viewport width during measure so word-wrapping reading content reflows instead of expanding horizontally."),
        new PropertyDescription("HorizontalOffset / VerticalOffset", "int", "0", "Store validated cell offsets clamped whenever extent or viewport changes."),
        new PropertyDescription("LineSize / PageOverlap", "int", "1 / 1", "Control keyboard line movement and retained overlap between page movements."),
    ];

    /// <summary>Initializes the ScrollView showcase page and composes its specimens.</summary>
    internal ScrollViewShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        ControlStack content = PaneSupport.Vertical();

        for (var index = 1; index <= 14; index++)
        {
            content.Children.Add(new ControlText(
                $"Scrollable row {index:00} · wide content beyond the viewport"));
        }

        examples.Children.Add(new ControlScrollView
        {
            Width = Length.Cells(34),
            Height = Length.Cells(8),
            Content = content,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
        });
    }
}
