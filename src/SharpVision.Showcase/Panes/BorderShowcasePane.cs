// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Controls;

/// <summary>Documents and demonstrates the Border control.</summary>
internal sealed class BorderShowcasePane: ShowcasePane
{
    internal const string Title = "Border";
    private const string _catalogSummary =
        "Frames one owned child with independently enabled physical edges and terminal-safe glyph sets.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Child ownership", "Assign one detached child", "The child receives the border's measured, arranged, rendered, and input space."),
        new InteractionDescription("Rendering", "Change Glyphs or BorderThickness", "The frame redraws with the selected physical edges and terminal-safe runes."),
        new InteractionDescription("Resize", "Change the available parent cells", "The child is remeasured inside the committed border edges."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Child", "Control?", "null", "Owns the single control measured, arranged, and rendered inside the border edges."),
        new PropertyDescription("BorderThickness", "Thickness", "0", "Enables each physical edge with a validated thickness of zero or one terminal cell."),
        new PropertyDescription("Glyphs", "Glyphs", "Light", "Selects light, heavy, paired, rounded, ASCII, solid, or shaded Unicode border runes."),
        new PropertyDescription("BorderColor", "Color?", "inherited", "Overrides the foreground color used only for border cells while preserving child styling."),
        new PropertyDescription("Background", "Color?", "inherited", "Fills the complete border box behind both its edges and owned child content."),
    ];

    /// <summary>Initializes the Border showcase page and composes its specimens.</summary>
    internal BorderShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        PaneSupport.AddBorder(examples, "Light", Glyphs.Light);
        PaneSupport.AddBorder(examples, "Heavy", Glyphs.Heavy);
        PaneSupport.AddBorder(examples, "Paired", Glyphs.Paired);
        PaneSupport.AddBorder(examples, "Rounded", Glyphs.Rounded);
        PaneSupport.AddBorder(examples, "ASCII fallback", Glyphs.Ascii);
        PaneSupport.AddBorder(examples, "Solid block", Glyphs.Solid);
        PaneSupport.AddBorder(examples, "Light shade", Glyphs.LightShade);
        PaneSupport.AddBorder(examples, "Medium shade", Glyphs.MediumShade);
        PaneSupport.AddBorder(examples, "Dark shade", Glyphs.DarkShade);
    }
}
