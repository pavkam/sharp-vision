// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the TextInput control with single-line, constrained, and multiline editor specimens.</summary>
internal sealed class TextInputPane: CompositeControlBase
{
    internal TextInputPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "TextInput";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var editable = new TextInput { Width = Length.Cells(28), Text = "Edit me: café 👩‍💻" };
        var editStatus = new Text("Editing: waiting");
        editable.TextChanged += (_, eventArgs) => editStatus.Content = $"Editing: {eventArgs.Text}";
        editable.Submitted += (_, eventArgs) => editStatus.Content = $"Submitted: {eventArgs.Text}";

        var selectionEditor = new TextInput { Width = Length.Cells(28), Text = "Select café 👩‍💻" };
        var selectionStatus = new Text("Selection: none");
        var selectAll = new Button { Text = "&Select all" };
        selectAll.Click += (_, _) =>
        {
            selectionEditor.Select(0, selectionEditor.Text.Length);
            selectionStatus.Content = $"Selection: 0..{selectionEditor.Text.Length}";
        };

        var history = new TextInput { Width = Length.Cells(28), Text = "Draft" };
        var historyStatus = new Text("History: ready");
        var revise = new Button { Text = "&Append revision" };
        revise.Click += (_, _) =>
        {
            history.Text += " revised";
            historyStatus.Content = $"History: undo={history.CanUndo}, redo={history.CanRedo}";
        };
        var undo = new Button { Text = "&Undo" };
        undo.Click += (_, _) =>
        {
            _ = history.Undo();
            historyStatus.Content = $"History: undo={history.CanUndo}, redo={history.CanRedo}";
        };
        var redo = new Button { Text = "&Redo" };
        redo.Click += (_, _) =>
        {
            _ = history.Redo();
            historyStatus.Content = $"History: undo={history.CanUndo}, redo={history.CanRedo}";
        };

        var clipboard = new TextInput { Width = Length.Cells(28), Text = "Copy café 👩‍💻" };
        clipboard.Select(0, clipboard.Text.Length);
        var clipboardStatus = new Text("Clipboard: select Copy or Cut");
        var copy = new Button { Text = "&Copy selection" };
        copy.Click += (_, _) => clipboardStatus.Content = $"Clipboard: copied {clipboard.CopySelection()}";
        var cut = new Button { Text = "Cu&t selection" };
        cut.Click += (_, _) => clipboardStatus.Content = $"Clipboard: cut {clipboard.CutSelection()}";

        var eventEditor = new TextInput { Width = Length.Cells(28), Text = "Accepted" };
        var eventStatus = new Text("Events: waiting");
        eventEditor.TextChanging += (_, eventArgs) =>
        {
            if (eventArgs.Proposal.Text.Contains('!'))
            {
                eventArgs.Cancel = true;
                eventStatus.Content = "Events: TextChanging canceled";
            }
            else
            {
                eventStatus.Content = "Events: TextChanging";
            }
        };
        eventEditor.TextChanged += (_, _) => eventStatus.Content += " → TextChanged";
        eventEditor.SelectionChanged += (_, _) => eventStatus.Content += " → SelectionChanged";
        var rejectEdit = new Button { Text = "Try re&jected edit" };
        rejectEdit.Click += (_, _) => eventEditor.Text += "!";
        var acceptEdit = new Button { Text = "Co&mmit accepted edit" };
        acceptEdit.Click += (_, _) => eventEditor.Text += " revision";

        var readOnly = new TextInput { Width = Length.Cells(28), Text = "Read-only value", ReadOnly = true };

        var password = new TextInput { Width = Length.Cells(28), Text = "secret", PasswordCharacter = new Rune('•') };

        var limited = new TextInput { Width = Length.Cells(28), Text = "12 chars max", MaxLength = 12 };

        var multiline = new TextInput
        {
            Width = Length.Cells(28),
            Height = Length.Cells(5),
            AcceptsReturn = true,
            AcceptsTab = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarStyle = ScrollBarStyle.ThinLine,
            Text = "Multiline editor\nWheel here to scroll\nwithout losing focus\nAt the edge, the page scrolls"
        };
        var offsetStatus = new Text("Offsets: horizontal=0, vertical=0");
        var reportOffsets = new Button { Text = "Report editor &offsets" };
        reportOffsets.Click += (_, _) =>
            offsetStatus.Content =
                $"Offsets: horizontal={multiline.HorizontalOffset}, vertical={multiline.VerticalOffset}";
        var unicode = new TextInput { Width = Length.Cells(28), Text = "Delete 👩‍💻" };

        return new DocPage(
            Title,
            "<info>TextInput</info> edits grapheme-safe single-line or multiline text with selection, undo, masking, and scrolling.",
            new DocSection(
                "⌨️",
                "Editing and submission",
                "A single-line editor commits grapheme-safe changes and treats <reverse>Enter</reverse> as submission.",
                new DocExample(
                    "Free-form editing",
                    "Type Unicode text, navigate by complete grapheme, then press <reverse>Enter</reverse>. The status distinguishes mutation from <success>submission</success>.",
                    new DocColumn(editable, editStatus),
                    "var name = new TextInput { Width = Length.Cells(28) };\nname.Submitted += (_, e) => Save(e.Text);")),
            new DocSection(
                "🔤",
                "Selection",
                "Caret and selection endpoints are validated Unicode grapheme boundaries.",
                new DocExample(
                    "Select complete content",
                    "Use Select all and observe a range spanning the complete combining and emoji source without splitting a cluster.",
                    new DocColumn(selectionEditor, selectAll, selectionStatus),
                    "editor.Select(0, editor.Text.Length);")),
            new DocSection(
                "📋",
                "Clipboard and history",
                "Copy/cut shortcuts and bounded undo/redo operate on immutable text-and-selection snapshots.",
                new DocExample(
                    "Owned clipboard text",
                    "Copy returns an owned string without mutation; Cut returns the same selection and deletes it through the ordinary edit transaction.",
                    new DocColumn(clipboard, new DocRow(copy, cut), clipboardStatus),
                    "var copied = editor.CopySelection();\nvar cut = editor.CutSelection();"),
                new DocExample(
                    "Revision history",
                    "Append a revision, then use Undo and Redo. Availability updates after every committed snapshot.",
                    new DocColumn(history, new DocRow(revise, undo, redo), historyStatus))),
            new DocSection(
                "⚡",
                "Edit events",
                "TextChanging can cancel a valid proposal before state mutates; committed edits then raise TextChanged before SelectionChanged.",
                new DocExample(
                    "Cancellation and commit order",
                    "Reject the exclamation edit, then commit the accepted revision. The readout exposes the exact pre-commit and post-commit sequence.",
                    new DocColumn(eventEditor, new DocRow(rejectEdit, acceptEdit), eventStatus))),
            new DocSection(
                "🔒",
                "Policies",
                "Read-only, password, and maximum-length policies reject only the mutations they own.",
                new DocExample(
                    "Read-only and masked values",
                    "Read-only text still navigates and copies. Password text renders one mask per grapheme and never copies its source.",
                    new DocColumn(readOnly, password)),
                new DocExample(
                    "Maximum grapheme count",
                    "MaxLength counts user-visible graphemes rather than UTF-16 units while still allowing deletion.",
                    limited)),
            new DocSection(
                "📄",
                "Multiline",
                "<reverse>Return</reverse>, <reverse>Tab</reverse>, scrolling, selection, and caret geometry share one editor viewport.",
                new DocExample(
                    "Editor with canonical scrollbar",
                    "Wheel over the editor first, then report its cell offsets. At its endpoint, an unchanged wheel event bubbles to the surrounding documentation viewport.",
                    new DocColumn(multiline, reportOffsets, offsetStatus),
                    "editor.AcceptsReturn = true;\neditor.ScrollBars = ScrollBars.Vertical;")),
            new DocSection(
                "🌐",
                "Unicode boundary",
                "Movement, deletion, selection, and pointer placement never expose an interior UTF-16 or wide-cell position.",
                new DocExample(
                    "Delete one complete ZWJ grapheme",
                    "Place the caret after the developer emoji and press Backspace once. The complete emoji disappears without exposing an interior UTF-16 position.",
                    unicode)));
    }
}
