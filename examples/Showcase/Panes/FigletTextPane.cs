// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the FigletText control with a live editable FIGfont preview.</summary>
internal sealed class FigletTextPane: CompositeControlBase
{
    internal FigletTextPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "FigletText";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var catalog = FigletCatalog.Default;
        var text = new TextInput { Width = Length.Cells(30), Text = "SharpVision" };
        string[] fontNames = [.. catalog.Names];
        var picker = new ComboBox
        {
            Width = Length.Cells(30),
            Items = fontNames,
            SelectedIndex = Array.IndexOf(fontNames, "Standard"),
            DropDownHeight = 8,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarStyle = ScrollBarStyle.ThinLine
        };
        var preview = new FigletText(catalog.Load("Standard")) { Content = text.Text };
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
            Options = new FigletOptions(layout: FigletLayout.None)
        };
        var fitted = new FigletText(catalog.Load("Standard"))
        {
            Content = "AB",
            Options = new FigletOptions(layout: FigletLayout.HorizontalFitting)
        };
        var smushed = new FigletText(catalog.Load("Standard"))
        {
            Content = "AB",
            Options = new FigletOptions(
                layout: FigletLayout.HorizontalSmushing | FigletLayout.Equal | FigletLayout.Hierarchy)
        };

        var themed = new FigletText(catalog.Load("Small")) { Content = "Theme" };

        var large = new FigletText(catalog.Load("Banner")) { Content = "VISION" };
        var viewport = new Stack
        {
            Width = Length.Cells(40),
            Height = Length.Cells(8),
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarStyle = ScrollBarStyle.ThinLine,
            Children = { large }
        };

        var fallback = new FigletText(catalog.Load("Standard")) { Content = "café 你好" };

        var multiLine = new FigletText(catalog.Load("Standard")) { Content = "Multi\nLine" };

        return new DocPage(
            Title,
            "<info>FigletText</info> renders text through a bounded immutable <info>FigletFont</info> while preserving the ordinary control box model.",
            new DocSection(
                "✏️",
                "Live editor",
                "Start with source text and one audited embedded font, then update either property through ordinary control mutation.",
                new DocExample(
                    "Editable FIGfont preview",
                    "Type source text and choose a font. Only the selected font is loaded and the preview remeasures from its generated rows.",
                    new DocColumn(text, picker, status, preview),
                    "var title = new FigletText(FigletCatalog.Default.Load(\"Standard\"))\n{\n    Content = \"SharpVision\",\n};")),
            new DocSection(
                "🔤",
                "Font comparison",
                "Compare a few intentional shapes before browsing the complete audited catalog.",
                new DocExample(
                    "Standard, Slant, and Small",
                    "The same short source reveals height, weight, and spacing differences without loading all 400 fonts.",
                    new DocColumn(
                        new DocColumn(new Text("Standard"), standard),
                        new DocColumn(new Text("Slant"), slant),
                        new DocColumn(new Text("Small"), small)))),
            new DocSection(
                "📐",
                "Layout options",
                "FigletOptions can override full width, fitting, and smushing while preserving the immutable font.",
                new DocExample(
                    "Full, fitted, and smushed",
                    "Compare identical glyphs with no fitting, horizontal fitting, and selected smushing rules.",
                    new DocColumn(fullWidth, fitted, smushed),
                    "preview.Options = new FigletOptions(layout: FigletLayout.HorizontalFitting);")),
            new DocSection(
                "🎨",
                "Theme inheritance",
                "FIGlet output preserves its parent background while following the active theme without local appearance configuration.",
                new DocExample(
                    "Theme-owned appearance",
                    "Change the application theme and the same retained FIGletText instance resolves its foreground automatically.",
                    themed)),
            new DocSection(
                "📏",
                "Large output",
                "<info>FigletText</info> does not scale or wrap generated art; place it in an <info>AutoScroll</info> container when bounded presentation matters.",
                new DocExample(
                    "Scrollable Banner output",
                    "Use the thin rails to inspect output larger than the forty-by-eight viewport.",
                    viewport)),
            new DocSection(
                "📄",
                "Multi-line output",
                "Newline characters in the source produce vertically stacked FIGlet rows, each rendered independently.",
                new DocExample(
                    "Two-line banner",
                    "Each source line generates a full FIGlet row. Vertical smushing applies within each row but lines stack vertically.",
                    multiLine,
                    "var banner = new FigletText(font) { Content = \"Multi\\nLine\" };")),
            new DocSection(
                "🌐",
                "Fallback",
                "Fonts may omit source scalars; rendering applies each audited font's deterministic fallback without claiming universal glyph coverage.",
                new DocExample(
                    "Latin accents and CJK source",
                    "The source remains Unicode even where the selected FIGfont must substitute missing glyphs.",
                    fallback)));
    }
}
