using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

namespace SharpVision.Showcase.Panes.Window;

/// <summary>Documents and demonstrates the Window control.</summary>
internal sealed class WindowPane: ShowcasePane
{
    private const string _catalogSummary =
        "Frames one owned child as a titled terminal application surface with optional Turbo Vision-style shadowing.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Title", "Choose a title placement and Glyphs family", "The title stays inside the frame corners while the selected border set redraws the chrome."),
        PaneMetadata.Interaction("Enter", "Press Enter when no child handles it", "The first available IsDefault Button activates."),
        PaneMetadata.Interaction("Escape", "Press Escape when no child handles it", "The first available IsCancel Button activates."),
        PaneMetadata.Interaction("Resize", "Change the viewport or window bounds", "The child remains inside the frame and the shadow clips safely at the terminal edge."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Child", "Control?", "null", "Owns the one control arranged in the framed interior after the one-cell frame edges."),
        PaneMetadata.Property("Title", "string", "empty", "Writes a non-null title into the top edge and safely clips it before either frame corner."),
        PaneMetadata.Property("TitlePlacement", "WindowTitlePlacement", "Left", "Places the safe title at the left, center, or right of the usable top edge."),
        PaneMetadata.Property("Glyphs", "Glyphs", "Rounded", "Selects the Unicode or ASCII-safe physical glyph family used by the complete frame."),
        PaneMetadata.Property("HasShadow", "bool", "true", "Enables the translated visual shadow without changing the content layout rectangle."),
        PaneMetadata.Property("ShadowMode", "ShadowMode", "Composite", "Chooses composite darkening or visible block glyphs for shadow cells outside the window body."),
        PaneMetadata.Property("ShadowOffset", "Point", "(2, 1)", "Moves the optional shadow in signed terminal cells and clips it safely to the terminal viewport."),
    ];

    /// <summary>Initializes the Window showcase page and composes its specimens.</summary>
    internal WindowPane()
        : base("Window", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "Window",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new WindowPane());

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var chromeOptions = PaneSupport.Horizontal();
        chromeOptions.Children.Add(WindowVariant("Left", Glyphs.Rounded, WindowTitlePlacement.Left));
        chromeOptions.Children.Add(WindowVariant("Center", Glyphs.Paired, WindowTitlePlacement.Center));
        chromeOptions.Children.Add(WindowVariant("Right", Glyphs.Ascii, WindowTitlePlacement.Right));

        var form = PaneSupport.Vertical();
        form.Children.Add(new ControlText("Choose how this project opens.")
        {
            Foreground = Palette.Text,
        });
        form.Children.Add(new ControlCheckBox
        {
            Content = new ControlText("Restore last session"),
            IsChecked = true,
            MarkStyle = CheckBoxStyle.Tick,
            Style = Palette.Interactive(),
        });
        form.Children.Add(new ControlCheckBox
        {
            Content = new ControlText("Start in safe mode"),
            MarkStyle = CheckBoxStyle.Brackets,
            Style = Palette.Interactive(),
        });
        var actions = PaneSupport.Horizontal();
        actions.HorizontalAlignment = HorizontalAlignment.Center;
        actions.Children.Add(PaneSupport.ButtonSpecimen(new ControlButton
        {
            Content = new ControlText("Apply"),
            IsDefault = true,
            Style = Palette.Interactive(),
        }));
        actions.Children.Add(PaneSupport.ButtonSpecimen(new ControlButton
        {
            Content = new ControlText("Cancel"),
            IsCancel = true,
            Style = Palette.Interactive(),
        }));
        form.Children.Add(actions);
        var window = new ControlWindow
        {
            Width = Length.Cells(42),
            Height = Length.Auto,
            Title = "Project settings",
            BorderColor = Palette.Accent,
            Background = Palette.Surface,
            HasShadow = true,
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowOffset = new Point(2, 1),
            Child = form,
        };
        var stage = new ControlCanvas
        {
            Width = Length.Cells(48),
            Height = Length.Cells(13),
            ClipToBounds = true,
        };
        ControlCanvas.SetLeft(window, Length.Cells(1));
        ControlCanvas.SetTop(window, Length.Cells(1));
        stage.Children.Add(window);
        examples.Children.Add(PaneSupport.SampleSection(
            "Border and title options",
            "Each Window uses the same child contract with a different Glyphs family and title placement: rounded left, paired center, and portable ASCII right.",
            chromeOptions));
        examples.Children.Add(PaneSupport.SampleSection(
            "Titled application surface",
            "A Window owns its interior while title chrome, frame, and shadow render as one terminal-safe surface. Try Enter for Apply or Escape for Cancel.",
            new ControlBorder
            {
                BorderThickness = new Thickness(1),
                Glyphs = Glyphs.Light,
                BorderColor = Palette.Border,
                Background = Palette.Panel,
                Child = stage,
            }));
    }

    private static ControlWindow WindowVariant(string title, Glyphs glyphs, WindowTitlePlacement placement) => new()
    {
        Width = Length.Cells(14),
        Height = Length.Cells(5),
        Title = title,
        TitlePlacement = placement,
        Glyphs = glyphs,
        BorderColor = Palette.Accent,
        Background = Palette.Surface,
        ShadowOffset = new Point(1, 1),
        Child = new ControlText("Preview")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Palette.Text,
        },
    };
}
