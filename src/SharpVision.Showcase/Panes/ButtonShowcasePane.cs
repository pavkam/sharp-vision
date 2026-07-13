// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;


/// <summary>Live Button documentation page composed as a real control tree.</summary>
internal sealed class ButtonShowcasePane: ShowcasePane
{
    internal const string Title = "Button";
    private const string _catalogSummary =
        "Activates one semantic action through keyboard, pointer, programmatic, or command paths.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Enter", "Press Enter while the button is available", "Click fires once and the command executes when CanExecute permits it."),
        new InteractionDescription("Space", "Press and release Space", "The button enters pressed state, then activates on release."),
        new InteractionDescription("Pointer", "Press and release the primary pointer inside", "Focus and capture are applied; release inside fires one Click."),
        new InteractionDescription("Programmatic", "Call PerformClick", "The same availability and command rules apply without synthesizing terminal input."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Content", "Control?", "null", "Owns the single visual child used as the button label or richer content."),
        new PropertyDescription("Command", "ICommand?", "null", "Queries executable state and runs after the Click event for a completed activation."),
        new PropertyDescription("CommandParameter", "object?", "null", "Supplies the borrowed value passed to command availability and execution methods."),
        new PropertyDescription("IsDefault", "bool", "false", "Marks the button for an owning Window to use as its Enter fallback action."),
        new PropertyDescription("IsCancel", "bool", "false", "Marks the button for an owning Window to use as its Escape fallback action."),
        new PropertyDescription("Glyphs", "Glyphs", "Rounded", "Selects the one-cell border family rendered around the button."),
        new PropertyDescription("HasShadow / ShadowOffset", "bool / Point", "true / (1, 1)", "Controls the compact shadow footprint outside the button's interactive surface."),
        new PropertyDescription("ShadowMode / ShadowGlyph", "ShadowMode / Rune", "Composite / ▓", "Selects a quiet style-only lift or an explicit Turbo Vision block-glyph shadow."),
        new PropertyDescription("IsEnabled", "bool", "true", "Disables focus, pointer capture, keyboard activation, Click, and command execution when false."),
    ];

    /// <summary>Initializes the Button showcase page and composes its specimens.</summary>
    internal ButtonShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        ControlText status = new("Activation log: waiting");
        ControlButton active = new()
        {
            Content = new ControlText("Click or press Enter"),
        };
        active.Click += (_, eventArgs) =>
            status.Content = $"Activation log: {eventArgs.Cause}";
        ControlStack primary = new();
        primary.Children.Add(PaneSupport.ButtonSpecimen(active));
        primary.Children.Add(status);
        examples.Children.Add(PaneSupport.SampleSection(
            "Primary action",
            "A raised, bordered action surface responds to hover, focus, press, Enter, Space, and a primary pointer click.",
            primary));

        ControlStack roles = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
        roles.Children.Add(PaneSupport.ButtonSpecimen(new ControlButton
        {
            Content = new ControlText("Default action"),
            IsDefault = true,
        }));
        roles.Children.Add(PaneSupport.ButtonSpecimen(new ControlButton
        {
            Content = new ControlText("Cancel action"),
            IsCancel = true,
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
                ShadowMode = ShadowMode.BlockGlyph,
                ShadowGlyph = new Rune('░'),
            })));

        examples.Children.Add(PaneSupport.SampleSection(
            "Flat action",
            "A shadowless button stays in place while pressed. Its pressed theme covers the full face and border instead of simulating depth that is not there.",
            PaneSupport.ButtonSpecimen(new ControlButton
            {
                Content = new ControlText("Flat action"),
                HasShadow = false,
            })));

        examples.Children.Add(PaneSupport.SampleSection(
            "Disabled action",
            "Unavailable actions remain readable but do not accept focus, pointer capture, or activation.",
            PaneSupport.ButtonSpecimen(new ControlButton
            {
                Content = new ControlText("Disabled"),
                IsEnabled = false,
            })));
    }
}
