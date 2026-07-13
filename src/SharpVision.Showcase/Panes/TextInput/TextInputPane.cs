using SharpVision.Layout;

namespace SharpVision.Showcase.Panes.TextInput;

/// <summary>Documents and demonstrates the TextInput control.</summary>
internal sealed class TextInputPane: ShowcasePane
{
    private const string _catalogSummary =
        "Edits grapheme-safe single-line or multiline text with selection, undo, masking, and scrolling.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Text entry", "Type printable characters", "Text changes at grapheme boundaries and the caret advances by user-visible clusters."),
        PaneMetadata.Interaction("Selection", "Use arrows with Shift", "SelectionStart and SelectionLength expand or contract without splitting a grapheme."),
        PaneMetadata.Interaction("Clipboard and undo", "Use copy, cut, paste, or undo shortcuts", "The edit transaction updates text, selection, and the undo history together."),
        PaneMetadata.Interaction("Tab and Enter", "Press Tab or Enter", "Focus moves or submission occurs unless AcceptsTab or AcceptsReturn consumes the key."),
        PaneMetadata.Interaction("Mouse wheel", "Wheel over a multiline editor", "The editor scrolls its own cells first; at an endpoint the next wheel event reaches an enclosing viewport."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Text", "string", "empty", "Stores non-null content and keeps the caret and selection on grapheme boundaries."),
        PaneMetadata.Property("IsReadOnly", "bool", "false", "Allows navigation and copying while suppressing edits, cutting, and pasted mutations."),
        PaneMetadata.Property("AcceptsReturn / AcceptsTab", "bool", "false", "Controls whether Enter and Tab insert content instead of submitting or moving focus."),
        PaneMetadata.Property("PasswordCharacter", "Rune?", "null", "Masks each grapheme with one printable cell and suppresses source disclosure through copy."),
        PaneMetadata.Property("MaxLength", "int", "0 (unlimited)", "Limits content by grapheme count while rejecting a value below existing text length."),
        PaneMetadata.Property("SelectionStart / SelectionLength", "int", "0 / 0", "Expose a normalized UTF-16 range whose endpoints must align to grapheme boundaries."),
        PaneMetadata.Property("HorizontalOffset / VerticalOffset", "int", "0 / 0", "Expose the committed cell and logical-line scroll positions used by caret and wheel navigation."),
        PaneMetadata.Property("ScrollBars / ShowScrollBars", "ScrollBars / ShowScrollBars", "Both / WhenNeeded", "Reserve canonical rails for enabled overflowing axes while retaining wheel and caret scrolling when chrome is hidden."),
        PaneMetadata.Property("ScrollBarChrome / ScrollBarFill", "ScrollBarStyle / ScrollBarFill", "Full / Block", "Configure the editor's owned rails with the same thin/full and line/block treatments as every other scrolling host."),
    ];

    /// <summary>Initializes the TextInput showcase page and composes its specimens.</summary>
    internal TextInputPane()
        : base("TextInput", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "TextInput",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new TextInputPane());

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        examples.Children.Add(new ControlTextInput
        {
            Width = Length.Cells(28),
            Text = "Edit me: café 👩‍💻",
            Style = Palette.Editor(),
        });
        examples.Children.Add(new ControlTextInput
        {
            Width = Length.Cells(28),
            Text = "Read-only value",
            IsReadOnly = true,
            Style = Palette.Editor(),
        });
        examples.Children.Add(new ControlTextInput
        {
            Width = Length.Cells(28),
            Text = "secret",
            PasswordCharacter = new Rune('•'),
            Style = Palette.Editor(),
        });
        examples.Children.Add(new ControlTextInput
        {
            Width = Length.Cells(28),
            Text = "12 chars max",
            MaxLength = 12,
            Style = Palette.Editor(),
        });
        examples.Children.Add(new ControlTextInput
        {
            Width = Length.Cells(28),
            Height = Length.Cells(3),
            AcceptsReturn = true,
            AcceptsTab = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarChrome = ScrollBarStyle.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            Text = "Multiline editor\nWheel here to scroll\nwithout losing focus\nAt the edge, the page scrolls",
            Style = Palette.Editor(),
        });
    }
}
