// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;


/// <summary>Documents and demonstrates the TextInput control.</summary>
internal sealed class TextInputShowcasePane: ShowcasePane
{
    internal const string Title = "TextInput";
    private const string _catalogSummary =
        "Edits grapheme-safe single-line or multiline text with selection, undo, masking, and scrolling.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Text entry", "Type printable characters", "Text changes at grapheme boundaries and the caret advances by user-visible clusters."),
        new InteractionDescription("Selection", "Use arrows with Shift", "SelectionStart and SelectionLength expand or contract without splitting a grapheme."),
        new InteractionDescription("Clipboard and undo", "Use copy, cut, paste, or undo shortcuts", "The edit transaction updates text, selection, and the undo history together."),
        new InteractionDescription("Tab and Enter", "Press Tab or Enter", "Focus moves or submission occurs unless AcceptsTab or AcceptsReturn consumes the key."),
        new InteractionDescription("Mouse wheel", "Wheel over a multiline editor", "The editor scrolls its own cells first; at an endpoint the next wheel event reaches an enclosing viewport."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Text", "string", "empty", "Stores non-null content and keeps the caret and selection on grapheme boundaries."),
        new PropertyDescription("IsReadOnly", "bool", "false", "Allows navigation and copying while suppressing edits, cutting, and pasted mutations."),
        new PropertyDescription("AcceptsReturn / AcceptsTab", "bool", "false", "Controls whether Enter and Tab insert content instead of submitting or moving focus."),
        new PropertyDescription("PasswordCharacter", "Rune?", "null", "Masks each grapheme with one printable cell and suppresses source disclosure through copy."),
        new PropertyDescription("MaxLength", "int", "0 (unlimited)", "Limits content by grapheme count while rejecting a value below existing text length."),
        new PropertyDescription("SelectionStart / SelectionLength", "int", "0 / 0", "Expose a normalized UTF-16 range whose endpoints must align to grapheme boundaries."),
        new PropertyDescription("HorizontalOffset / VerticalOffset", "int", "0 / 0", "Expose the committed cell and logical-line scroll positions used by caret and wheel navigation."),
        new PropertyDescription("ScrollBars / ShowScrollBars", "ScrollBars / ShowScrollBars", "Both / WhenNeeded", "Reserve canonical rails for enabled overflowing axes while retaining wheel and caret scrolling when chrome is hidden."),
        new PropertyDescription("ScrollBarChrome / ScrollBarFill", "ScrollBarChrome / ScrollBarFill", "Full / Block", "Configure the editor's owned rails with the same thin/full and line/block treatments as every other scrolling host."),
    ];

    /// <summary>Initializes the TextInput showcase page and composes its specimens.</summary>
    internal TextInputShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        examples.Children.Add(new ControlTextInput
        {
            Width = Length.Cells(28),
            Text = "Edit me: café 👩‍💻",
        });
        examples.Children.Add(new ControlTextInput
        {
            Width = Length.Cells(28),
            Text = "Read-only value",
            IsReadOnly = true,
        });
        examples.Children.Add(new ControlTextInput
        {
            Width = Length.Cells(28),
            Text = "secret",
            PasswordCharacter = new Rune('•'),
        });
        examples.Children.Add(new ControlTextInput
        {
            Width = Length.Cells(28),
            Text = "12 chars max",
            MaxLength = 12,
        });
        examples.Children.Add(new ControlTextInput
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
        });
    }
}
