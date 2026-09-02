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
        var eventLog = new Text { Overflow = Overflow.Wrap };
        var eventEntries = new List<string>();

        void AppendEvent(string entry)
        {
            eventEntries.Add(entry);

            while (eventEntries.Count > 3)
            {
                eventEntries.RemoveAt(0);
            }

            eventLog.Content = "<d>Event log</d>\n" +
                string.Join('\n', eventEntries.Select(static value => Text.Escape($"• {value}")));
        }

        AppendEvent("Waiting for a dismissal request.");
        var allowDismissal = new CheckBox
        {
            Text = "&Allow close",
            VerticalAlignment = VerticalAlignment.Center
        };
        var dismiss = new Button
        {
            Text = "Dismiss &now",
            VerticalAlignment = VerticalAlignment.Center
        };
        var reopen = new Button { Text = "&Reopen compact bar" };
        var interactive = new InfoBar
        {
            Width = Length.Cells(44),
            Title = "Review required",
            Adornment = new Affix("⚠", "!"),
            Style = InfoBarStyle.Warning,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = new Stack
            {
                Spacing = 1,
                Children =
                {
                    new Text("Deployment is paused."),
                    new DocRow(allowDismissal, dismiss) { Spacing = 1 }
                }
            }
        };
        dismiss.Click += (_, _) => interactive.Dismiss();
        reopen.Click += (_, _) => interactive.IsOpen = true;
        interactive.DismissRequested += (_, eventArgs) =>
        {
            eventArgs.Cancel = allowDismissal.IsChecked != true;
            AppendEvent(eventArgs.Cancel
                ? "DismissRequested: vetoed; enable Allow close."
                : "DismissRequested: accepted; close and availability cleanup are pending.");
        };
        interactive.Dismissed += (_, _) =>
            AppendEvent("Dismissed: close and availability cleanup completed.");

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
                "The body remains an ordinary focusable control tree. Its action chooses whether to request dismissal, and DismissRequested may veto before close and availability cleanup commit.",
                new DocExample(
                    "Compact dismissal actions",
                    "The concise title and body stay on one line at 44 cells. Allow close and Dismiss now share one aligned action row; the first dismissal is vetoed until the checkbox is set.",
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
