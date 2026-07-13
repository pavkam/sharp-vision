// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Controls;

/// <summary>Documents and demonstrates the RadioButton control.</summary>
internal sealed class RadioButtonShowcasePane: ShowcasePane
{
    internal const string Title = "RadioButton";
    private const string _catalogSummary =
        "Selects one option from an ordinally named group scoped to the attached control root.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Space", "Press and release Space", "This member becomes checked and its checked peer is cleared."),
        new InteractionDescription("Pointer", "Click the primary pointer inside", "The member receives focus and selects within its group."),
        new InteractionDescription("Arrows", "Navigate among group members", "Focus moves through eligible members without selecting disabled entries."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("IsChecked", "bool", "false", "Selects this member and atomically clears the previously selected peer."),
        new PropertyDescription("GroupName", "string?", "null", "Scopes mutual exclusion by ordinal name within the attached root."),
        new PropertyDescription("Content", "Control?", "null", "Owns the optional label arranged after the single-cell radio indicator."),
        new PropertyDescription("IsEnabled", "bool", "true", "Excludes the member from focus, pointer activation, and group keyboard navigation when false."),
    ];

    /// <summary>Initializes the RadioButton showcase page and composes its specimens.</summary>
    internal RadioButtonShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        ControlStack group = PaneSupport.Vertical();
        group.Children.Add(new ControlRadioButton
        {
            Content = new ControlText("Fast"),
            GroupName = "quality",
            IsChecked = true,
            Style = Palette.Interactive(),
        });
        group.Children.Add(new ControlRadioButton
        {
            Content = new ControlText("Balanced"),
            GroupName = "quality",
            Style = Palette.Interactive(),
        });
        group.Children.Add(new ControlRadioButton
        {
            Content = new ControlText("Unavailable"),
            GroupName = "quality",
            IsEnabled = false,
            Style = Palette.Interactive(),
        });
        examples.Children.Add(PaneSupport.SampleSection(
            "Named quality group",
            "Pick one mode. Arrow keys move selection between available members; the disabled member remains visibly unavailable.",
            PaneSupport.Card(group, Glyphs.Rounded)));

        ControlRadioButton independent = new ControlRadioButton
        {
            Content = new ControlText("Independent selection group"),
            GroupName = "delivery",
            IsChecked = true,
            Style = Palette.Interactive(),
        };
        examples.Children.Add(PaneSupport.SampleSection(
            "Separate group",
            "A different GroupName scopes selection independently, so this choice does not disturb the quality group.",
            PaneSupport.Card(independent, Glyphs.Light)));
    }
}
