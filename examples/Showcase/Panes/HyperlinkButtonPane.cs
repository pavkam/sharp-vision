// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the HyperlinkButton control with live, themed specimens.</summary>
internal sealed class HyperlinkButtonPane: CompositeControlBase
{
    internal HyperlinkButtonPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "HyperlinkButton";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        // Basic hyperlink with a visible activation log.
        var status = new Text("Activation log: waiting");
        var basic = new HyperlinkButton("&Click here");
        basic.Click += (_, eventArgs) => status.Content = $"Activation log: {eventArgs.Cause}";

        // Command availability and borrowed parameter execution.
        var commandStatus = new Text("Command log: ready with parameter docs");
        var commandEnabled = new CheckBox { Text = "Command &enabled", IsChecked = true };
        var command = new ShowcaseCommand(
            parameter => commandStatus.Content = $"Command log: executed {parameter}",
            _ => commandEnabled.IsChecked == true);
        var commandLink = new HyperlinkButton("&Open documentation")
        {
            Command = command,
            CommandParameter = "docs"
        };
        commandEnabled.StateChanged += (_, eventArgs) =>
        {
            commandLink.IsEnabled = eventArgs.Current == true;
            command.RaiseCanExecuteChanged();
            commandStatus.Content = eventArgs.Current == true
                ? "Command log: enabled"
                : "Command log: unavailable";
        };

        // Disabled state.
        var disabled = new HyperlinkButton("&Unavailable link") { IsEnabled = false };

        // Side-by-side comparison with a regular Button.
        var comparisonStatus = new Text("Comparison log: waiting");
        var linkVersion = new HyperlinkButton("&Navigate")
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        linkVersion.Click += (_, eventArgs) => comparisonStatus.Content = $"Comparison log: HyperlinkButton {eventArgs.Cause}";
        var buttonVersion = new Button("Na&vigate");
        buttonVersion.Click += (_, eventArgs) => comparisonStatus.Content = $"Comparison log: Button {eventArgs.Cause}";

        // EndAffix reserves a fixed cell for an application-owned external-link indicator.
        var externalStatus = new Text("Activation log: waiting");
        var external = new HyperlinkButton("&Release notes")
        {
            EndAffix = new Affix("↗", ">")
        };
        external.Click += (_, eventArgs) => externalStatus.Content = $"Activation log: {eventArgs.Cause}";

        return new DocPage(
            Title,
            "<info>HyperlinkButton</info> activates one <i>semantic action</i> through a minimal accent-colored underlined text control.",
            new DocSection(
                "🔗",
                "Start here",
                "A clickable text styled as a classic hyperlink. Focus, hover, press, and keyboard activation work exactly like Button.",
                new DocExample(
                    "Basic hyperlink",
                    "Click or press <reverse>Enter</reverse> or <reverse>Space</reverse>. The log identifies the <success>committed input path</success>.",
                    new DocColumn(basic, status),
                    "var link = new HyperlinkButton(\"&Click here\");\nlink.Click += (_, e) => status.Content = e.Cause.ToString();")),
            new DocSection(
                "📎",
                "Commands",
                "Use Command and CommandParameter when an action owns reusable availability and execution policy.",
                new DocExample(
                    "Availability and borrowed parameter",
                    "Toggle command availability, then activate the link. The command receives the exact borrowed parameter only while it can execute.",
                    new DocColumn(commandEnabled, commandLink, commandStatus),
                    "link.Command = openDocsCommand;\nlink.CommandParameter = \"docs\";")),
            new DocSection(
                "🚫",
                "States",
                "A disabled HyperlinkButton loses its underline and uses the muted disabled foreground.",
                new DocExample(
                    "Disabled",
                    "The link is not focusable and does not respond to pointer or keyboard input.",
                    disabled,
                    "var link = new HyperlinkButton(\"&Unavailable link\") { IsEnabled = false };")),
            new DocSection(
                "⚖️",
                "Comparison",
                "HyperlinkButton and Button share the same activation and command contract but differ in chrome.",
                new DocExample(
                    "Side by side",
                    "Both controls respond to the same input paths. HyperlinkButton uses accent-underlined text; Button uses bordered chrome.",
                    new DocColumn(new DocRow(linkVersion, buttonVersion), comparisonStatus),
                    "var link = new HyperlinkButton(\"&Navigate\");\nvar button = new Button(\"Na&vigate\");")),
            new DocSection(
                "🌐",
                "Affixes",
                "<info>StartAffix</info> and <info>EndAffix</info> reserve a fixed cell beside the caption for application-owned, data-driven decoration that a theme never authors.",
                new DocExample(
                    "External-link glyph after the caption",
                    "The arrow marks a link that leaves the application; it sits in its own reserved cell after the caption.",
                    new DocColumn(external, externalStatus),
                    "var link = new HyperlinkButton(\"&Release notes\")\n{\n    EndAffix = new Affix(\"↗\", \">\")\n};")));
    }
}
