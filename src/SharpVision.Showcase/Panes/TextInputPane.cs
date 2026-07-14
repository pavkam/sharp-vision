// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Documents the TextInput control with single-line, constrained, and multiline editor specimens.</summary>
internal sealed class TextInputPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "TextInput";

    /// <inheritdoc/>
    protected override Control Build()
    {
        TextInput editable = new()
        {
            Width = Length.Cells(28),
            Text = "Edit me: café 👩‍💻",
        };

        TextInput readOnly = new()
        {
            Width = Length.Cells(28),
            Text = "Read-only value",
            IsReadOnly = true,
        };

        TextInput password = new()
        {
            Width = Length.Cells(28),
            Text = "secret",
            PasswordCharacter = new Rune('•'),
        };

        TextInput limited = new()
        {
            Width = Length.Cells(28),
            Text = "12 chars max",
            MaxLength = 12,
        };

        TextInput multiline = new()
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

        return Doc.Page(
            Title,
            "Edits grapheme-safe single-line or multiline text with selection, undo, masking, and scrolling.",
            Doc.Example(
                "Free-form editing",
                "Type printable characters; text changes at grapheme boundaries and the caret advances by user-visible clusters. Selection, clipboard, and undo shortcuts operate on the same transaction.",
                editable),
            Doc.Example(
                "Read-only",
                "Navigation and copying still work while edits, cuts, and pasted mutations are suppressed.",
                readOnly),
            Doc.Example(
                "Password masking",
                "Each grapheme renders as one printable mask cell and the source text is never disclosed through copy.",
                password),
            Doc.Example(
                "Maximum length",
                "MaxLength rejects growth by grapheme count while still allowing navigation and shrinking edits.",
                limited),
            Doc.Example(
                "Multiline editor with scrollbars",
                "AcceptsReturn and AcceptsTab insert content instead of submitting or moving focus. A mouse wheel over the editor scrolls its own cells first; at an endpoint, the next wheel event reaches the enclosing viewport.",
                multiline));
    }
}
