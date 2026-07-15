// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;



/// <summary>Documents the FigletText control with a live editable FIGfont preview.</summary>
internal sealed class FigletTextPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "FigletText";

    /// <summary>Initializes the retained FigletText documentation page.</summary>
    internal FigletTextPane() => InitializeContent(CreateContent());

    private static Stack CreateContent()
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

        return Doc.Page(
            Title,
            "Renders text through a bounded immutable FIGfont while preserving the ordinary control box model.",
            Doc.Example(
                "Editable FIGfont preview",
                "Type source text and choose an audited font from the dropdown; the preview regenerates and remeasures from the selected glyph catalog.",
                Doc.Column(text, picker, status, preview)));
    }
}
