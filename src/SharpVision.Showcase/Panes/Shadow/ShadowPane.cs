using SharpVision.Controls;
using SharpVision.Terminal.Geometry;

using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;

namespace SharpVision.Showcase.Panes.Shadow;

/// <summary>Documents and demonstrates the Shadow control.</summary>
internal sealed class ShadowPane: ShowcasePane
{
    private const string _catalogSummary =
        "Decorates one child with Turbo Vision-style composite darkening or explicit block-glyph overflow.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Child input", "Interact with the owned child", "The child receives focus and pointer input; the shadow remains passive."),
        PaneMetadata.Interaction("Mode", "Switch Composite or BlockGlyph", "Exposed shadow cells either darken existing cells or draw the configured glyph."),
        PaneMetadata.Interaction("Viewport edge", "Move or resize the shadow beyond the canvas", "Only the visible shadow footprint is clipped into the terminal."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Child", "Control?", "null", "Owns the single control whose committed cells provide the shadow silhouette."),
        PaneMetadata.Property("Mode", "ShadowMode", "Composite", "Chooses style composition over existing cells or block-glyph drawing in exposed shadow cells."),
        PaneMetadata.Property("Offset", "Point", "(2, 1)", "Moves the visual shadow by signed horizontal and vertical terminal-cell offsets."),
        PaneMetadata.Property("Glyph", "Rune", "▓", "Selects the printable one-cell Rune used by block-glyph shadow mode."),
        PaneMetadata.Property("Attributes", "Attributes?", "Dim", "Overrides rendition attributes applied to shadow cells without changing the child."),
    ];

    /// <summary>Initializes the Shadow showcase page and composes its specimens.</summary>
    internal ShadowPane()
        : base("Shadow", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "Shadow",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new ShadowPane());

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        examples.Children.Add(new ControlText("Composite stage")
        {
            Foreground = Palette.Success,
            Attributes = TerminalAttributes.Bold,
        });
        examples.Children.Add(PaneSupport.ShadowStage(new ControlShadow
        {
            Child = PaneSupport.DemoCard("Composite", Glyphs.Rounded),
            Foreground = Palette.Muted,
            Background = Palette.Canvas,
            Offset = new Point(2, 1),
        }));
        examples.Children.Add(new ControlText("Block glyph stage")
        {
            Foreground = Palette.Accent,
            Attributes = TerminalAttributes.Bold,
        });
        examples.Children.Add(PaneSupport.ShadowStage(new ControlShadow
        {
            Child = PaneSupport.DemoCard("Block glyph", Glyphs.Paired),
            Mode = ShadowMode.BlockGlyph,
            Glyph = new Rune('░'),
            Foreground = Palette.Muted,
            Background = Palette.Canvas,
            Offset = new Point(2, 1),
        }));
    }
}
