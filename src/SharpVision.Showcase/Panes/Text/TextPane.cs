using SharpVision.Layout;
using SharpVision.Text;

using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;

namespace SharpVision.Showcase.Panes.Text;

/// <summary>Documents and demonstrates the Text control.</summary>
internal sealed class TextPane: ShowcasePane
{
    private const string _catalogSummary =
        "Formats Unicode text by grapheme cluster with wrapping, trimming, alignment, and cell-width policy.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Content edit", "Change Unicode text", "The control measures extended grapheme clusters and preserves combining and wide-cell ownership."),
        PaneMetadata.Interaction("Resize", "Change the available width", "Wrapping or trimming recomputes while keeping complete grapheme clusters intact."),
        PaneMetadata.Interaction("Alignment", "Choose Start, Center, or End", "Each formatted line moves within its committed content box."),
        PaneMetadata.Interaction("Pointer probe", "Move the pointer over the uneven grid readout", "Pixel coordinates stay exact; mapped cells appear only when exact grid metrics are available."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Content", "string", "empty", "Provides the non-null UTF-16 content measured as extended grapheme clusters."),
        PaneMetadata.Property("Wrapping", "Wrapping", "None", "Chooses no wrapping, grapheme wrapping, or word-aware wrapping within available cells."),
        PaneMetadata.Property("Trimming", "Trimming", "None", "Clips or appends an ellipsis when a formatted line cannot fit the content box."),
        PaneMetadata.Property("TextAlignment", "Alignment", "Start", "Places each formatted line at the start, center, or end of its available cells."),
        PaneMetadata.Property("AmbiguousWidth", "Ambiguous", "Narrow", "Controls whether East Asian Ambiguous graphemes occupy one or two terminal cells."),
    ];

    /// <summary>Initializes the Text showcase page and composes its specimens.</summary>
    internal TextPane()
        : base("Text", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "Text",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new TextPane());

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        examples.Children.Add(PaneSupport.SampleSection(
            "Cell geometry specimen",
            "Composed and decomposed text share width. Orphan combining marks render as replacement cells without changing editable source text.",
            new ControlText("é vs e\u0301 · orphan \u0301 · ambiguous · · 你好 · 👩‍💻 · 🇺🇸")
            {
                Foreground = Palette.Accent,
            }));
        examples.Children.Add(PaneSupport.SampleSection(
            "Uneven pixel pointer grid",
            "Pixel coordinates stay exact. Mapped cells appear only when exact grid metrics are available; unavailable cells are not shown as (0,0).",
            new PointerProbe()));
        examples.Children.Add(PaneSupport.SampleSection(
            "Unicode-safe wrapping",
            "Word wrapping leaves complete grapheme clusters together, including combining marks and wide emoji.",
            new ControlText("Plain Unicode: café · 你好 · 👩‍💻\nA narrow reading column wraps words without splitting clusters.")
            {
                Width = Length.Cells(28),
                Wrapping = Wrapping.Word,
            }));
        examples.Children.Add(PaneSupport.SampleSection(
            "Centered label",
            "Centering is for compact labels and status messages; it is deliberately shown without trimming.",
            new ControlText("Centered status")
            {
                Width = Length.Cells(28),
                TextAlignment = Alignment.Center,
                Foreground = Palette.Warning,
                Attributes = TerminalAttributes.Bold,
            }));
        examples.Children.Add(PaneSupport.SampleSection(
            "Single-line truncation",
            "Ellipsis is for one-line labels where the remaining space matters more than wrapping.",
            new ControlText("This deliberately long one-line label trims safely")
            {
                Width = Length.Cells(28),
                Trimming = Trimming.GraphemeEllipsis,
                Foreground = Palette.Accent,
            }));
    }
}
