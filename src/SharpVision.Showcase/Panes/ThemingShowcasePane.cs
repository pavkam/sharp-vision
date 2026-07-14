// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;


/// <summary>Documents and demonstrates the Theming control.</summary>
internal sealed class ThemingShowcasePane: ShowcasePane
{
    internal const string Title = "Theming";
    private const string _catalogSummary =
        "Demonstrates application themes, type-keyed styles, local overrides, and third-party style properties.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Theme switch", "Choose a theme in the sidebar picker", "Application.Theme republishes a frozen theme to every attached control."),
        new InteractionDescription("Local override", "Set Foreground on a specimen control", "The explicit local value survives later theme changes until cleared."),
        new InteractionDescription("Third-party property", "Change ShowcasePanel label placement", "Custom StyleProperty metadata resolves through the same cascade as built-in chrome."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Application.Theme", "Theme", "Themes.Dark", "Owns the active frozen theme snapshot published to the attached tree."),
        new PropertyDescription("Control.Style", "IControlStyle?", "null", "Applies a per-instance overlay only to the owning control."),
        new PropertyDescription("Control.Foreground", "Color?", "themed", "Reads and writes the foreground style property through the typed cascade."),
        new PropertyDescription("ShowcasePanel.LabelPlacement", "LabelPlacement", "Left", "Demonstrates a third-party style property registered outside SharpVision."),
    ];

    /// <summary>Initializes the Theming showcase page and composes its specimens.</summary>
    internal ThemingShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        ShowcasePanel panel = new();
        ControlStack placement = new() { Spacing = 1 };
        placement.Children.Add(new ControlText("Label placement"));
        ControlButton left = new() { Content = new ControlText("Left") };
        ControlButton right = new() { Content = new ControlText("Right") };
        left.Click += (_, _) => panel.LabelPlacement = LabelPlacement.Left;
        right.Click += (_, _) => panel.LabelPlacement = LabelPlacement.Right;
        placement.Children.Add(new ControlStack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { left, right },
        });

        examples.Children.Add(PaneSupport.SampleSection(
            "Application theme",
            "Use the theme picker in the sidebar footer. Application.Theme publishes a frozen snapshot to every attached control without ancestor-style inheritance.",
            panel));
        examples.Children.Add(PaneSupport.SampleSection(
            "Third-party style property",
            "ShowcasePanel registers LabelPlacement through StyleProperty metadata. Themes and local values resolve it with the same cascade as built-in chrome.",
            placement));
    }
}
