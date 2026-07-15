// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the Button control with live, themed activation specimens.</summary>
internal sealed class ButtonPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Button";

    /// <summary>Initializes the retained Button documentation page.</summary>
    internal ButtonPane() => InitializeContent(CreateContent());

    private static Stack CreateContent()
    {
        var status = new Text("Activation log: waiting");
        var primary = new Button() { Content = new Text("Click or press Enter") };
        primary.Click += (_, eventArgs) => status.Content = $"Activation log: {eventArgs.Cause}";

        var dialogDefault = new Button() { Content = new Text("OK"), IsDefault = true };
        var dialogCancel = new Button() { Content = new Text("Cancel"), IsCancel = true };

        var composite = new Button() { Content = new Text("Composite shadow") };
        var blockShadow = new Button()
        {
            Content = new Text("Block glyph shadow"),
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowGlyph = new Rune('░'),
        };
        var flat = new Button() { Content = new Text("Flat, no shadow"), HasShadow = false };
        var disabled = new Button() { Content = new Text("Disabled"), IsEnabled = false };

        return Doc.Page(
            Title,
            "Activates one semantic action through keyboard, pointer, programmatic, or command paths.",
            Doc.Example(
                "Primary action",
                "A raised, bordered surface responds to hover, focus, press, Enter, Space, and a primary pointer click. Activation reports its cause below.",
                Doc.Column(primary, status)),
            Doc.Example(
                "Dialog command roles",
                "IsDefault marks a button as its owning window's Enter fallback and IsCancel as its Escape fallback. Both flags are just markers here since these two buttons have no Window ancestor; see the Window page for the live fallback.",
                Doc.Row(dialogDefault, dialogCancel)),
            Doc.Example(
                "Shadow styles",
                "Buttons carry a composite shadow by default, a Turbo Vision block-glyph shadow, or none.",
                Doc.Row(composite, blockShadow, flat)),
            Doc.Example(
                "Disabled",
                "A disabled button is skipped by focus and ignores activation.",
                disabled));
    }
}
