using SharpVision.Controls;

namespace SharpVision.Showcase.Panes.Border;

/// <summary>Documents and demonstrates the Border control.</summary>
internal sealed class BorderPane: ShowcasePane
{
    private const string _catalogSummary =
        "Frames one owned child with independently enabled physical edges and terminal-safe glyph sets.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Child ownership", "Assign one detached child", "The child receives the border's measured, arranged, rendered, and input space."),
        PaneMetadata.Interaction("Rendering", "Change Glyphs or BorderThickness", "The frame redraws with the selected physical edges and terminal-safe runes."),
        PaneMetadata.Interaction("Resize", "Change the available parent cells", "The child is remeasured inside the committed border edges."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Child", "Control?", "null", "Owns the single control measured, arranged, and rendered inside the border edges."),
        PaneMetadata.Property("BorderThickness", "Thickness", "0", "Enables each physical edge with a validated thickness of zero or one terminal cell."),
        PaneMetadata.Property("Glyphs", "Glyphs", "Light", "Selects light, heavy, paired, rounded, ASCII, solid, or shaded Unicode border runes."),
        PaneMetadata.Property("BorderColor", "Color?", "inherited", "Overrides the foreground color used only for border cells while preserving child styling."),
        PaneMetadata.Property("Background", "Color?", "inherited", "Fills the complete border box behind both its edges and owned child content."),
    ];

    /// <summary>Initializes the Border showcase page and composes its specimens.</summary>
    internal BorderPane()
        : base("Border", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "Border",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new BorderPane());

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
