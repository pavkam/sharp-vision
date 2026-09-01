// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Demonstrates persistent in-flow notification styles, retained actions, and dismissal veto.</summary>
internal sealed class InfoBarPane: CompositeControlBase
{
    /// <summary>Gets the exact catalog and page name.</summary>
    internal const string Title = "InfoBar";

    /// <summary>Initializes the retained InfoBar documentation page.</summary>
    internal InfoBarPane() => InitializeContent(CreateContent());

    private static DocPage CreateContent()
    {
        var eventLog = new Text("Event log: waiting for a dismissal request.")
        {
            Overflow = Overflow.Wrap
        };
        var allowDismissal = new CheckBox { Text = "&Allow dismissal" };
        var dismiss = new Button { Text = "&Dismiss from content" };
        var reopen = new Button { Text = "&Reopen notification" };
        var interactive = new InfoBar
        {
            Title = "Deployment requires attention",
            Adornment = new Affix("⚠", "!"),
            Style = InfoBarStyle.Warning,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new Stack
            {
                Spacing = 1,
                Children =
                {
                    new Text("Long retained content wraps naturally and preserves Unicode: café · 界 · 👩‍💻")
                    {
                        Overflow = Overflow.Wrap
                    },
                    new Stack
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 1,
                        Children = { allowDismissal, dismiss }
                    }
                }
            }
        };
        dismiss.Click += (_, _) => interactive.Dismiss();
        reopen.Click += (_, _) => interactive.IsOpen = true;
        interactive.DismissRequested += (_, eventArgs) =>
        {
            eventArgs.Cancel = allowDismissal.IsChecked != true;
            eventLog.Content = eventArgs.Cancel
                ? "Event log: DismissRequested vetoed; enable Allow dismissal to close."
                : "Event log: DismissRequested accepted; cleanup is committing.";
        };
        interactive.Dismissed += (_, _) =>
            eventLog.Content = "Event log: Dismissed after layout, focus, and capture cleanup.";

        var narrow = new InfoBar
        {
            Width = Length.Cells(28),
            Title = "A deliberately long responsive title",
            Adornment = new Affix("界", "#"),
            Style = InfoBarStyle.Info,
            Content = new Text("The body owns wrapping while the close glyph keeps its trailing cell.")
            {
                Overflow = Overflow.Wrap
            }
        };

        return new DocPage(
            Title,
            "<info>InfoBar</info> keeps one persistent notification in ordinary layout, retaining arbitrary content and an explicit cancellable dismissal lifecycle without entering modality.",
            new DocSection(
                "🎨",
                "Complete semantic styles",
                "Info, Success, Warning, and Error are complete styles rather than behavior severities. Each paints an opaque surface with a matching border, title, adornment, and close accent.",
                new DocExample(
                    "Four notification states",
                    "These bars use the same retained-content and dismissal contract while selecting different semantic presentation values.",
                    new DocColumn(
                        CreatePreset("Information", "i", "A routine update is available.", InfoBarStyle.Info),
                        CreatePreset("Success", "✓", "The archive uploaded successfully.", InfoBarStyle.Success),
                        CreatePreset("Warning", "!", "The deployment needs review.", InfoBarStyle.Warning),
                        CreatePreset("Error", "×", "The server rejected the archive.", InfoBarStyle.Error))
                    {
                        Spacing = 1
                    },
                    "var bar = new InfoBar\n{\n    Title = \"Deployment needs review\",\n    Style = InfoBarStyle.Warning,\n    Content = details\n};")),
            new DocSection(
                "🔔",
                "Retained action and dismissal",
                "The body remains an ordinary focusable control tree. Its action chooses whether to request dismissal, and DismissRequested may veto before closed layout and input cleanup commit.",
                new DocExample(
                    "Dismissal veto and event order",
                    "Activate Dismiss from content or the trailing close glyph. The first request is vetoed until Allow dismissal is checked; Reopen notification restores the same retained controls.",
                    new DocColumn(interactive, new DocRow(reopen), eventLog)
                    {
                        Spacing = 1
                    },
                    "bar.DismissRequested += (_, args) => args.Cancel = !allowDismissal.IsChecked;\nbodyAction.Click += (_, _) => bar.Dismiss();"),
                new DocExample(
                    "Narrow Unicode layout",
                    "At 28 cells, the title shrinks and a wide adornment drops whole before the keyboard-reachable dismiss glyph loses its trailing cell.",
                    narrow,
                    "var narrow = new InfoBar\n{\n    Width = Length.Cells(28),\n    Adornment = new Affix(\"界\", \"#\"),\n    Content = wrappingText\n};")));
    }

    private static InfoBar CreatePreset(
        string title,
        string adornment,
        string content,
        InfoBarStyle style) => new()
    {
        Title = title,
        Adornment = new Affix(adornment),
        Style = style,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Content = new Text(content) { Overflow = Overflow.Wrap }
    };
}
