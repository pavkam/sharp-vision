// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Terminal.Geometry;

using TerminalAttributes = Terminal.Rendering.Attributes;

/// <summary>Documents and demonstrates the Shadow control.</summary>
internal sealed class ShadowShowcasePane: ShowcasePane
{
    internal const string Title = "Shadow";
    private const string _catalogSummary =
        "Decorates one child with Turbo Vision-style composite darkening or explicit block-glyph overflow.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Child input", "Interact with the owned child", "The child receives focus and pointer input; the shadow remains passive."),
        new InteractionDescription("Mode", "Switch Composite or BlockGlyph", "Exposed shadow cells either darken existing cells or draw the configured glyph."),
        new InteractionDescription("Viewport edge", "Move or resize the shadow beyond the canvas", "Only the visible shadow footprint is clipped into the terminal."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Child", "Control?", "null", "Owns the single control whose committed cells provide the shadow silhouette."),
        new PropertyDescription("Mode", "ShadowMode", "Composite", "Chooses style composition over existing cells or block-glyph drawing in exposed shadow cells."),
        new PropertyDescription("Offset", "Point", "(2, 1)", "Moves the visual shadow by signed horizontal and vertical terminal-cell offsets."),
        new PropertyDescription("Glyph", "Rune", "▓", "Selects the printable one-cell Rune used by block-glyph shadow mode."),
        new PropertyDescription("Attributes", "Attributes?", "Dim", "Overrides rendition attributes applied to shadow cells without changing the child."),
    ];

    /// <summary>Initializes the Shadow showcase page and composes its specimens.</summary>
    internal ShadowShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        examples.Children.Add(new ControlText("Composite stage")
        {
            Attributes = TerminalAttributes.Bold,
        });
        examples.Children.Add(PaneSupport.ShadowStage(new ControlShadow
        {
            Child = PaneSupport.DemoCard("Composite", Glyphs.Rounded),
            Offset = new Point(2, 1),
        }));
        examples.Children.Add(new ControlText("Block glyph stage")
        {
            Attributes = TerminalAttributes.Bold,
        });
        examples.Children.Add(PaneSupport.ShadowStage(new ControlShadow
        {
            Child = PaneSupport.DemoCard("Block glyph", Glyphs.Paired),
            Mode = ShadowMode.BlockGlyph,
            Glyph = new Rune('░'),
            Offset = new Point(2, 1),
        }));
    }
}
