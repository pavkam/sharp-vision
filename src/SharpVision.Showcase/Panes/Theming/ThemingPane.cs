using SharpVision.Layout;

namespace SharpVision.Showcase.Panes.Theming;

/// <summary>Documents and demonstrates the Theming control.</summary>
internal sealed class ThemingPane: ShowcasePane
{
    private const string _catalogSummary =
        "Demonstrates application themes, type-keyed styles, local overrides, and third-party style properties.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Theme switch", "Activate Light or Dark in the sidebar", "Application.Theme republishes a frozen theme to every attached control."),
        PaneMetadata.Interaction("Local override", "Set Foreground on a specimen control", "The explicit local value survives later theme changes until cleared."),
        PaneMetadata.Interaction("Third-party property", "Change ShowcasePanel label placement", "Custom StyleProperty metadata resolves through the same cascade as built-in chrome."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Application.Theme", "Theme", "Themes.Dark", "Owns the active frozen theme snapshot published to the attached tree."),
        PaneMetadata.Property("Control.Style", "IControlStyle?", "null", "Applies a per-instance overlay only to the owning control."),
        PaneMetadata.Property("Control.Foreground", "Color?", "themed", "Reads and writes the foreground style property through the typed cascade."),
        PaneMetadata.Property("ShowcasePanel.LabelPlacement", "LabelPlacement", "Left", "Demonstrates a third-party style property registered outside SharpVision."),
    ];

    /// <summary>Initializes the Theming showcase page and composes its specimens.</summary>
    internal ThemingPane()
        : base("Theming", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "Theming",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new ThemingPane());

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var panel = new ShowcasePanel
        {
            Foreground = Palette.Text,
            Background = Palette.Surface,
            BorderColor = Palette.Border,
        };
        var placement = new ControlStack { Spacing = 1 };
        placement.Children.Add(new ControlText("Label placement") { Foreground = Palette.Muted });
        var left = new ControlButton { Content = new ControlText("Left") };
        var right = new ControlButton { Content = new ControlText("Right") };
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
            "Use the Light and Dark buttons in the sidebar footer. Application.Theme publishes a frozen snapshot to every attached control without ancestor-style inheritance.",
            panel));
        examples.Children.Add(PaneSupport.SampleSection(
            "Third-party style property",
            "ShowcasePanel registers LabelPlacement through StyleProperty metadata. Themes and local values resolve it with the same cascade as built-in chrome.",
            placement));
    }
}
