// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the TextInput control with single-line, constrained, and multiline editor specimens.</summary>
internal sealed class TextInputPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "TextInput";

    /// <inheritdoc/>
    protected override Control Build()
    {
        var editable = new TextInput()
        {
            Width = Length.Cells(28),
            Text = "Edit me: café 👩‍💻",
        };
        var editStatus = new Text("Editing: waiting");
        editable.TextChanged += (_, eventArgs) => editStatus.Content = $"Editing: {eventArgs.Text}";
        editable.Submitted += (_, eventArgs) => editStatus.Content = $"Submitted: {eventArgs.Text}";

        var selectionEditor = new TextInput
        {
            Width = Length.Cells(28),
            Text = "Select café 👩‍💻",
        };
        var selectionStatus = new Text("Selection: none");
        var selectAll = new Button() { Content = new Text("Select all") };
        selectAll.Click += (_, _) =>
        {
            selectionEditor.Select(0, selectionEditor.Text.Length);
            selectionStatus.Content = $"Selection: 0..{selectionEditor.Text.Length}";
        };

        var history = new TextInput { Width = Length.Cells(28), Text = "Draft" };
        var historyStatus = new Text("History: ready");
        var revise = new Button() { Content = new Text("Append revision") };
        revise.Click += (_, _) =>
        {
            history.Text += " revised";
            historyStatus.Content = $"History: undo={history.CanUndo}, redo={history.CanRedo}";
        };
        var undo = new Button() { Content = new Text("Undo") };
        undo.Click += (_, _) =>
        {
            _ = history.Undo();
            historyStatus.Content = $"History: undo={history.CanUndo}, redo={history.CanRedo}";
        };
        var redo = new Button() { Content = new Text("Redo") };
        redo.Click += (_, _) =>
        {
            _ = history.Redo();
            historyStatus.Content = $"History: undo={history.CanUndo}, redo={history.CanRedo}";
        };

        var readOnly = new TextInput()
        {
            Width = Length.Cells(28),
            Text = "Read-only value",
            IsReadOnly = true,
        };

        var password = new TextInput()
        {
            Width = Length.Cells(28),
            Text = "secret",
            PasswordCharacter = new Rune('•'),
        };

        var limited = new TextInput()
        {
            Width = Length.Cells(28),
            Text = "12 chars max",
            MaxLength = 12,
        };

        var multiline = new TextInput()
        {
            Width = Length.Cells(28),
            Height = Length.Cells(3),
            AcceptsReturn = true,
            AcceptsTab = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarChrome = ScrollBarChrome.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            Text = "Multiline editor\nWheel here to scroll\nwithout losing focus\nAt the edge, the page scrolls",
        };
        var unicode = new TextInput
        {
            Width = Length.Cells(28),
            Text = "Move café 👩‍💻 as clusters",
        };

        return Doc.Page(
            Title,
            "Edits grapheme-safe single-line or multiline text with selection, undo, masking, and scrolling.",
            Doc.Section(
                "Editing and submission",
                "A single-line editor commits grapheme-safe changes and treats Enter as submission.",
                Doc.Example(
                    "Free-form editing",
                    "Type Unicode text, navigate by complete grapheme, then press Enter. The status distinguishes mutation from submission.",
                    Doc.Column(editable, editStatus),
                    "var name = new TextInput { Width = Length.Cells(28) };\nname.Submitted += (_, e) => Save(e.Text);")),
            Doc.Section(
                "Selection",
                "Caret and selection endpoints are validated Unicode grapheme boundaries.",
                Doc.Example(
                    "Select complete content",
                    "Use Select all and observe a range spanning the complete combining and emoji source without splitting a cluster.",
                    Doc.Column(selectionEditor, selectAll, selectionStatus),
                    "editor.Select(0, editor.Text.Length);")),
            Doc.Section(
                "Clipboard and history",
                "Copy/cut shortcuts and bounded undo/redo operate on immutable text-and-selection snapshots.",
                Doc.Example(
                    "Revision history",
                    "Append a revision, then use Undo and Redo. Availability updates after every committed snapshot.",
                    Doc.Column(history, Doc.Row(revise, undo, redo), historyStatus))),
            Doc.Section(
                "Policies",
                "Read-only, password, and maximum-length policies reject only the mutations they own.",
                Doc.Example(
                    "Read-only and masked values",
                    "Read-only text still navigates and copies. Password text renders one mask per grapheme and never copies its source.",
                    Doc.Column(readOnly, password)),
                Doc.Example(
                    "Maximum grapheme count",
                    "MaxLength counts user-visible graphemes rather than UTF-16 units while still allowing deletion.",
                    limited)),
            Doc.Section(
                "Multiline",
                "Return, Tab, scrolling, selection, and caret geometry share one editor viewport.",
                Doc.Example(
                    "Editor with canonical scrollbar",
                    "Wheel over the editor first. At its endpoint, an unchanged wheel event bubbles to the surrounding documentation viewport.",
                    multiline,
                    "editor.AcceptsReturn = true;\neditor.ScrollBars = ScrollBars.Vertical;")),
            Doc.Section(
                "Unicode boundary",
                "Movement, deletion, selection, and pointer placement never expose an interior UTF-16 or wide-cell position.",
                Doc.Example(
                    "Combining and ZWJ content",
                    "Move through café and the developer emoji: each behaves as one user-visible cluster where the Unicode contract requires it.",
                    unicode)));
    }
}
