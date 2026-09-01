// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using DataBinding;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents Pager state, bounded layout, interaction, and two-way binding.</summary>
internal sealed class PagerPane: CompositeControlBase
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Pager";

    /// <summary>Initializes the retained Pager documentation page.</summary>
    internal PagerPane()
    {
        var model = new DataBindingShowcaseModel { NumericValue = 8 };
        LivePager = new Pager
        {
            Width = Length.Cells(40),
            PageCount = 18,
            PageIndex = 8,
            MaximumVisiblePages = 5
        };
        _ = LivePager.Bind(model, source => source.NumericValue);
        LiveStatus = new Text("Page 9 of 18 · model PageIndex 8 · ready") { Overflow = Overflow.Wrap };
        LivePager.PageChanged += (_, eventArgs) =>
            LiveStatus.Content =
                $"Page {eventArgs.CurrentPageIndex + 1} of {LivePager.PageCount} · " +
                $"model PageIndex {model.NumericValue} · {eventArgs.Cause}";

        var previous = new Button { Text = "&Previous" };
        var next = new Button { Text = "&Next" };
        previous.Click += (_, _) =>
            _ = LivePager.ChangePage(Math.Max(0, LivePager.PageIndex - 1));
        next.Click += (_, _) =>
            _ = LivePager.ChangePage(Math.Min(LivePager.PageCount - 1, LivePager.PageIndex + 1));

        var empty = new Pager { Width = Length.Cells(32) };
        var single = new Pager
        {
            Width = Length.Cells(32),
            PageCount = 1
        };
        var first = new Pager
        {
            Width = Length.Cells(32),
            PageCount = 12,
            PageIndex = 0
        };
        var last = new Pager
        {
            Width = Length.Cells(32),
            PageCount = 12,
            PageIndex = 11
        };
        var narrow = new Pager
        {
            Width = Length.Cells(9),
            PageCount = 120,
            PageIndex = 57,
            MaximumVisiblePages = 5
        };
        var custom = new Pager
        {
            Width = Length.Cells(40),
            PageCount = 18,
            PageIndex = 8,
            Style = PagerStyle.Default with
            {
                FirstPageGlyph = new ControlGlyph(new Rune('⇤'), new Rune('[')),
                PreviousPageGlyph = new ControlGlyph(new Rune('←'), new Rune('<')),
                NextPageGlyph = new ControlGlyph(new Rune('→'), new Rune('>')),
                LastPageGlyph = new ControlGlyph(new Rune('⇥'), new Rune(']')),
                OmittedPagesGlyph = new ControlGlyph(new Rune('⋯'), new Rune('.')),
                CurrentPageColor = SemanticColor.Success
            }
        };

        InitializeContent(new DocPage(
            Title,
            "<info>Pager</info> keeps one valid page index visible through bounded whole-cell targets, canonical input, and an optional two-way model binding.",
            new DocSection(
                "↔️",
                "Live page and binding",
                "Select a numbered target, use arrows, Page Up/Page Down, Home/End, or the buttons. The event line observes the committed <info>PageIndex</info> after the bound model updates.",
                new DocExample(
                    "Middle page with omission",
                    "The centered window keeps endpoint numbers and uses omission glyphs for the gaps.",
                    new DocColumn(LivePager, new DocRow(previous, next), LiveStatus),
                    "pager.PageCount = 18;\npager.PageIndex = 8;\npager.Bind(viewModel, model => model.PageIndex);")),
            new DocSection(
                "↔",
                "Finite width",
                "The current page is retained first, then endpoint numbers, nearby numbers, omission, and navigation. No target is clipped into a partial glyph or digit.",
                new DocExample(
                    "Narrow retention",
                    "Nine cells keep only the highest-priority complete numbered targets from a 120-page range.",
                    narrow,
                    "var pager = new Pager { Width = Length.Cells(9), PageCount = 120, PageIndex = 57 };")),
            new DocSection(
                "◫",
                "Range states",
                "An empty range has <info>PageIndex = -1</info>; one page renders only 1; first and last states expose disabled endpoint navigation without inventing child buttons.",
                new DocExample(
                    "Empty, single, first, and last",
                    "Each specimen is the real focusable control under the same theme and layout pipeline.",
                    new DocColumn(
                        ShowcasePaneHelpers.DimCaption("Empty · PageIndex -1"),
                        empty,
                        ShowcasePaneHelpers.DimCaption("Single · page 1"),
                        single,
                        ShowcasePaneHelpers.DimCaption("First · previous targets disabled"),
                        first,
                        ShowcasePaneHelpers.DimCaption("Last · next targets disabled"),
                        last))),
            new DocSection(
                "🎨",
                "Style-resolved glyphs",
                "A complete <info>PagerStyle</info> supplies preferred and portable navigation glyphs plus the current-page color. The live cell policy chooses each one-cell representation.",
                new DocExample(
                    "Local glyph family",
                    "This specimen uses arrow variants with ASCII fallbacks and the theme's Success semantic color.",
                    custom))));
    }

    /// <summary>Gets the live bound middle-page specimen.</summary>
    internal Pager LivePager { get; }

    /// <summary>Gets the live page transition and model status.</summary>
    internal Text LiveStatus { get; }
}
