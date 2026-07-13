using SharpVision.Layout;

namespace SharpVision.Showcase.Panes.Button;

/// <summary>Live Button documentation page composed as a real control tree.</summary>
internal sealed class ButtonPane: ShowcasePane
{
    private const string _catalogSummary =
        "Activates one semantic action through keyboard, pointer, programmatic, or command paths.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Enter", "Press Enter while the button is available", "Click fires once and the command executes when CanExecute permits it."),
        PaneMetadata.Interaction("Space", "Press and release Space", "The button enters pressed state, then activates on release."),
        PaneMetadata.Interaction("Pointer", "Press and release the primary pointer inside", "Focus and capture are applied; release inside fires one Click."),
        PaneMetadata.Interaction("Programmatic", "Call PerformClick", "The same availability and command rules apply without synthesizing terminal input."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Content", "Control?", "null", "Owns the single visual child used as the button label or richer content."),
        PaneMetadata.Property("Command", "ICommand?", "null", "Queries executable state and runs after the Click event for a completed activation."),
        PaneMetadata.Property("CommandParameter", "object?", "null", "Supplies the borrowed value passed to command availability and execution methods."),
        PaneMetadata.Property("IsDefault", "bool", "false", "Marks the button for an owning Window to use as its Enter fallback action."),
        PaneMetadata.Property("IsCancel", "bool", "false", "Marks the button for an owning Window to use as its Escape fallback action."),
        PaneMetadata.Property("Glyphs", "Glyphs", "Rounded", "Selects the one-cell border family rendered around the button."),
        PaneMetadata.Property("HasShadow / ShadowOffset", "bool / Point", "true / (1, 1)", "Controls the compact shadow footprint outside the button's interactive surface."),
        PaneMetadata.Property("ShadowMode / ShadowGlyph", "ShadowMode / Rune", "Composite / ▓", "Selects a quiet style-only lift or an explicit Turbo Vision block-glyph shadow."),
        PaneMetadata.Property("IsEnabled", "bool", "true", "Disables focus, pointer capture, keyboard activation, Click, and command execution when false."),
    ];

    /// <summary>Initializes the Button showcase page and composes its specimens.</summary>
    internal ButtonPane()
        : base("Button", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "Button",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new ButtonPane());

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var status = new ControlText("Activation log: waiting");
        var active = new ControlButton
        {
            Content = new ControlText("Click or press Enter"),
            Style = Palette.Interactive(),
        };
        active.Click += (_, eventArgs) =>
            status.Content = $"Activation log: {eventArgs.Cause}";
        var primary = new ControlStack();
        primary.Children.Add(PaneSupport.ButtonSpecimen(active));
        primary.Children.Add(status);
        examples.Children.Add(PaneSupport.SampleSection(
            "Primary action",
            "A raised, bordered action surface responds to hover, focus, press, Enter, Space, and a primary pointer click.",
            primary));

        var roles = new ControlStack { Orientation = Orientation.Horizontal, Spacing = 2 };
        roles.Children.Add(PaneSupport.ButtonSpecimen(new ControlButton
        {
            Content = new ControlText("Default action"),
            IsDefault = true,
            Style = Palette.Interactive(),
        }));
        roles.Children.Add(PaneSupport.ButtonSpecimen(new ControlButton
        {
            Content = new ControlText("Cancel action"),
            IsCancel = true,
            Style = Palette.Interactive(),
        }));
        examples.Children.Add(PaneSupport.SampleSection(
            "Dialog command roles",
            "Default and cancel roles let an owning dialog map Enter and Escape to conventional actions.",
            roles));

        examples.Children.Add(PaneSupport.SampleSection(
            "Turbo Vision block shadow",
            "Composite is a quiet surface lift. Block glyph mode deliberately draws a visible shade footprint when the control needs old-school terminal depth.",
            PaneSupport.ButtonSpecimen(new ControlButton
            {
                Content = new ControlText("Block glyph shadow"),
                ShadowMode = Controls.ShadowMode.BlockGlyph,
                ShadowGlyph = new Rune('░'),
                Style = Palette.Interactive(),
            })));

        examples.Children.Add(PaneSupport.SampleSection(
            "Flat action",
            "A shadowless button stays in place while pressed. Its pressed palette covers the full face and border instead of simulating depth that is not there.",
            PaneSupport.ButtonSpecimen(new ControlButton
            {
                Content = new ControlText("Flat action"),
                HasShadow = false,
                Style = Palette.Interactive(),
            })));

        examples.Children.Add(PaneSupport.SampleSection(
            "Disabled action",
            "Unavailable actions remain readable but do not accept focus, pointer capture, or activation.",
            PaneSupport.ButtonSpecimen(new ControlButton
            {
                Content = new ControlText("Disabled"),
                IsEnabled = false,
                Style = Palette.Interactive(),
            })));
    }
}
