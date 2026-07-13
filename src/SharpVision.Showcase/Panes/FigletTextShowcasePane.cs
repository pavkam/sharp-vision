// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Fonts;
using SharpVision.Layout;

/// <summary>Documents and demonstrates the FigletText control.</summary>
internal sealed class FigletTextShowcasePane: ShowcasePane
{
    internal const string Title = "FigletText";
    private const string _catalogSummary =
        "Renders text through a bounded immutable FIGfont while preserving the ordinary control box model.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Content edit", "Change the source text", "FIGfont output is regenerated and measured from the new grapheme content."),
        new InteractionDescription("Font selection", "Choose an audited FIGfont", "The preview rebuilds with the selected glyph catalog and its documented metrics."),
        new InteractionDescription("Resize", "Change the available cells", "The generated glyphs clip or reflow through the ordinary control box."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Content", "string", "empty", "Provides the non-null Unicode source text expanded through the selected FIGfont glyphs."),
        new PropertyDescription("Font", "FigletFont", "required", "Selects the immutable parsed font and invalidates measurement whenever it changes."),
        new PropertyDescription("Options", "FigletOptions", "font defaults", "Overrides horizontal or vertical layout and left-to-right or right-to-left rendering."),
        new PropertyDescription("Foreground", "Color?", "inherited", "Overrides the foreground color of every generated FIGlet output cell."),
    ];

    /// <summary>Initializes the FigletText showcase page and composes its specimens.</summary>
    internal FigletTextShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        FigletCatalog catalog = FigletCatalog.Default;
        ControlTextInput text = new ControlTextInput
        {
            Width = Length.Cells(30),
            Text = "SharpVision",
            Style = Palette.Editor(),
        };
        var fontNames = catalog.Names.ToArray();
        ControlComboBox picker = new ControlComboBox
        {
            Width = Length.Cells(30),
            Items = fontNames,
            SelectedIndex = Array.IndexOf(fontNames, "Standard"),
            DropDownHeight = 8,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarChrome = ScrollBarStyle.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            Style = Palette.List(),
        };
        ControlFigletText preview = new ControlFigletText(catalog.Load("Standard"))
        {
            Content = text.Text,
            Foreground = Palette.Accent,
        };
        ControlText status = new ControlText("Type text, then choose a font from the dropdown.")
        {
            Foreground = Palette.Muted,
        };
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
        examples.Children.Add(text);
        examples.Children.Add(picker);
        examples.Children.Add(status);
        examples.Children.Add(preview);
    }
}
