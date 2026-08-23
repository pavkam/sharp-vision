// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Controls.SyntaxHighlighting;
using SharpVision.SyntaxHighlighting;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents CodeView with a live, selectable, foldable Rust sample.</summary>
internal sealed class CodeViewPane: CompositeControlBase
{
    /// <summary>Initializes the live code-view documentation page.</summary>
    internal CodeViewPane() => InitializeContent(CreateContent());

    /// <summary>Gets the exact catalog and page name.</summary>
    internal const string Title = "CodeView";

    private static DocPage CreateContent()
    {
        const string rustSample = """
            // A tiny, representative Rust sample.
            use std::collections::HashMap;

            /// Counts how many times each word appears.
            fn word_counts(text: &str) -> HashMap<String, u32> {
                let mut counts = HashMap::new();
                for word in text.split_whitespace() {
                    let entry = counts.entry(word.to_lowercase()).or_insert(0);
                    *entry += 1;
                }
                counts
            }

            fn main() {
                let sample = "the quick brown fox jumps over the lazy dog";
                let counts = word_counts(sample);
                println!("{} unique words, 0x{:X} as hex", counts.len(), 42);
            }
            """;

        var status = new Text("Selected: (none)");
        var view = new CodeView
        {
            Code = rustSample,
            Language = "Rust",
            Height = Length.Cells(18),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarStyle = ScrollBarStyle.ThinLine,
        };
        view.SelectionChanged += (_, _) =>
            status.Content = view.Selection.IsEmpty
                ? "Selected: (none)"
                : $"Selected: {view.Selection.Length} character(s)";

        var selectAll = new Button("&Select all");
        selectAll.Click += (_, _) => view.SelectAll();
        var clearSelection = new Button("&Clear selection");
        clearSelection.Click += (_, _) => view.ClearSelection();
        var collapseAll = new Button("Co&llapse folds");
        collapseAll.Click += (_, _) => view.CollapseAll();
        var expandAll = new Button("&Expand folds");
        expandAll.Click += (_, _) => view.ExpandAll();

        const string recipe = """
            var view = new CodeView
            {
                Code = source,
                Language = "Rust",
                ScrollBars = ScrollBars.Both
            };

            view.SelectAll();
            var copied = view.CopySelection(); // pure; the host wires the real clipboard
            """;

        var example = new DocExample(
            "A read-only, selectable, foldable Rust function",
            "Click and drag to select text, or use the keyboard: arrow keys move the caret, Shift extends the selection, and Ctrl+A selects everything. Click a ▼/▶ gutter arrow to fold or unfold one range, or right-click for a context menu with Copy, Select All, and whole-document folding commands.",
            new DocColumn(
                view,
                status,
                new DocRow(selectAll, clearSelection),
                new DocRow(collapseAll, expandAll)),
            recipe)
        {
            Width = Length.Percent(75),
        };

        return new DocPage(
            Title,
            $"<info>CodeView</info> displays read-only source code colored against a Kate/KSyntaxHighlighting-format grammar, with mouse and keyboard selection, copying, and region folding. {SyntaxDefinitionCatalog.Default.Names.Count} languages ship embedded.",
            new DocSection(
                "{;}",
                "Syntax-colored source",
                "Every token is colored purely by its default-style role, so a theme swap restyles the whole sample consistently.",
                example));
    }
}
