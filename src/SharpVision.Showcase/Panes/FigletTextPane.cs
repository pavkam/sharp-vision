// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;



/// <summary>Documents the FigletText control with a live editable FIGfont preview.</summary>
internal sealed class FigletTextPane: CompositeControl
{

    internal FigletTextPane() => InitializeContent(CreateContent());
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "FigletText";

    /// <inheritdoc/>
    private static Dock CreateContent()
    {
        var catalog = FigletCatalog.Default;
        var text = new TextInput()
        {
            Width = Length.Cells(30),
            Text = "SharpVision",
        };
        string[] fontNames = [.. catalog.Names];
        var picker = new ComboBox()
        {
            Width = Length.Cells(30),
            Items = fontNames,
            SelectedIndex = Array.IndexOf(fontNames, "Standard"),
            DropDownHeight = 8,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarChrome = ScrollBarChrome.Thin,
            ScrollBarFill = ScrollBarFill.Line,
        };
        var preview = new FigletText(catalog.Load("Standard"))
        {
            Content = text.Text,
        };
        var status = new Text("Type text, then choose a font from the dropdown.");
        text.TextChanged += (_, eventArgs) => preview.Content = eventArgs.Text;
        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedIndex < 0 || picker.Items[picker.SelectedIndex] is not string name)
            {
                return;
            }

            // Load only the selected audited font; the archive is never expanded wholesale.
            preview.Font = catalog.Load(name);
            status.Content = $"Previewing {name}. Choose another font to compare it.";
        };

        var standard = new FigletText(catalog.Load("Standard")) { Content = "SV" };
        var slant = new FigletText(catalog.Load("Slant")) { Content = "SV" };
        var small = new FigletText(catalog.Load("Small")) { Content = "SV" };

        var fullWidth = new FigletText(catalog.Load("Standard"))
        {
            Content = "AB",
            Options = new FigletOptions(layout: FigletLayout.None),
        };
        var fitted = new FigletText(catalog.Load("Standard"))
        {
            Content = "AB",
            Options = new FigletOptions(layout: FigletLayout.HorizontalFitting),
        };
        var smushed = new FigletText(catalog.Load("Standard"))
        {
            Content = "AB",
            Options = new FigletOptions(
                layout: FigletLayout.HorizontalSmushing | FigletLayout.Equal | FigletLayout.Hierarchy),
        };

        var inherited = new FigletText(catalog.Load("Small")) { Content = "Theme" };
        var explicitStyle = new FigletText(catalog.Load("Small"))
        {
            Content = "Accent",
            Foreground = ThemeColors.Accent,
            Attributes = TerminalAttributes.Bold,
        };

        var large = new FigletText(catalog.Load("Banner")) { Content = "VISION" };
        var viewport = new Stack
        {
            Width = Length.Cells(40),
            Height = Length.Cells(8),
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarChrome = ScrollBarChrome.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            Children = { large },
        };

        var fallback = new FigletText(catalog.Load("Standard")) { Content = "café 你好" };

        return Doc.Page(
            Title,
            "Renders text through a bounded immutable FIGfont while preserving the ordinary control box model.",
            Doc.Section(
                "🔤",
                "Live editor",
                "Start with source text and one audited embedded font, then update either property through ordinary control mutation.",
                Doc.Example(
                    "Editable FIGfont preview",
                    "Type source text and choose a font. Only the selected font is loaded and the preview remeasures from its generated rows.",
                    Doc.Column(text, picker, status, preview),
                    "var title = new FigletText(FigletCatalog.Default.Load(\"Standard\"))\n{\n    Content = \"SharpVision\",\n};")),
            Doc.Section(
                "🔤",
                "Font comparison",
                "Compare a few intentional shapes before browsing the complete audited catalog.",
                Doc.Example(
                    "Standard, Slant, and Small",
                    "The same short source reveals height, weight, and spacing differences without loading all 400 fonts.",
                    Doc.Column(
                        Doc.Column(new Text("Standard"), standard),
                        Doc.Column(new Text("Slant"), slant),
                        Doc.Column(new Text("Small"), small)))),
            Doc.Section(
                "🔤",
                "Layout options",
                "FigletOptions can override full width, fitting, and smushing while preserving the immutable font.",
                Doc.Example(
                    "Full, fitted, and smushed",
                    "Compare identical glyphs with no fitting, horizontal fitting, and selected smushing rules.",
                    Doc.Column(fullWidth, fitted, smushed),
                    "preview.Options = new FigletOptions(layout: FigletLayout.HorizontalFitting);")),
            Doc.Section(
                "🔤",
                "Style",
                "FIGlet output follows the active control style unless a local semantic override is intentional.",
                Doc.Example(
                    "Inherited and explicit appearance",
                    "Theme follows the application; Accent owns a local semantic foreground and bold attribute.",
                    Doc.Column(inherited, explicitStyle))),
            Doc.Section(
                "🔤",
                "Large output",
                "FigletText does not scale or wrap generated art; place it in an AutoScroll container when bounded presentation matters.",
                Doc.Example(
                    "Scrollable Banner output",
                    "Use the thin rails to inspect output larger than the forty-by-eight viewport.",
                    viewport)),
            Doc.Section(
                "🔤",
                "Fallback",
                "Fonts may omit source scalars; rendering applies each audited font's deterministic fallback without claiming universal glyph coverage.",
                Doc.Example(
                    "Latin accents and CJK source",
                    "The source remains Unicode even where the selected FIGfont must substitute missing glyphs.",
                    fallback)));
    }
}
