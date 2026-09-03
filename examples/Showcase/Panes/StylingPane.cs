// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Demonstrates concrete colors, semantic colors, appearance channels, and live visual states.</summary>
internal sealed class StylingPane: CompositeControlBase
{
    /// <summary>Initializes the retained Styling concept page.</summary>
    internal StylingPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Styling";

    private static DocPage CreateContent()
    {
        var concreteColors = new Wrap { Spacing = 1, LineSpacing = 1 };
        concreteColors.Children.Add(CreateColorSample("Terminal default", Color.Default));
        concreteColors.Children.Add(CreateColorSample("24-bit RGB", Color.Rgb(120, 70, 180)));
        concreteColors.Children.Add(CreateColorSample("Transparent", Color.Transparent));

        var semanticColors = new Wrap
        {
            Width = Length.Percent(100),
            Spacing = 1,
            LineSpacing = 1
        };

        foreach (var semanticColor in Enum.GetValues<SemanticColor>())
        {
            semanticColors.Children.Add(CreateSemanticColorSample(semanticColor));
        }

        var faceChannels = new Wrap { Spacing = 1, LineSpacing = 1 };
        faceChannels.Children.Add(CreateFaceSample(
            "Foreground: Accent",
            new Face(
                SemanticColor.Accent,
                SemanticColor.Control,
                SemanticDecoration.NormalText,
                Underline.None,
                Color.Default)));
        faceChannels.Children.Add(CreateFaceSample(
            "Background: Blue",
            new Face(
                SemanticColor.WindowText,
                SemanticColor.Blue,
                SemanticDecoration.NormalText,
                Underline.None,
                Color.Default)));
        faceChannels.Children.Add(CreateFaceSample(
            "Bold + curly underline",
            new Face(
                SemanticColor.ControlText,
                SemanticColor.Control,
                TerminalAttributes.Bold,
                Underline.Curly,
                SemanticColor.Accent)));

        var borders = new Wrap { Spacing = 2, LineSpacing = 1 };
        borders.Children.Add(CreateBorderSample("Flat", BorderRelief.Flat));
        borders.Children.Add(CreateBorderSample("Raised", BorderRelief.Raised));
        borders.Children.Add(CreateBorderSample("Sunken", BorderRelief.Sunken));

        var shadows = new Wrap { Spacing = 1, LineSpacing = 1 };
        shadows.Children.Add(CreateShadowSample("Composite", ShadowMode.Composite));
        shadows.Children.Add(CreateShadowSample("Block glyph", ShadowMode.BlockGlyph));
        shadows.Children.Add(CreateShadowSample("Fractional block", ShadowMode.FractionalBlock));

        var elementStates = CreateElementStates();

        return new DocPage(
            Title,
            "<info>Styling</info> combines concrete terminal colors with theme-resolved semantic colors and complete Face, Border, and Shadow values.",
            new DocSection(
                "🎨",
                "Color values",
                "Concrete Color values stay literal; semantic values follow the active application Theme without per-control resolution code.",
                new DocExample(
                    "Concrete Color representations",
                    "Terminal default, 24-bit RGB, and transparent composition remain distinct across theme changes.",
                    concreteColors),
                new DocExample(
                    "Complete semantic palette",
                    "Every SemanticColor value is enumerated directly. Change the Showcase theme to resolve the same retained swatches through another palette.",
                    semanticColors,
                    "foreach (var color in Enum.GetValues<SemanticColor>())\n    swatches.Children.Add(CreateSwatch(color));")),
            new DocSection(
                "🎭",
                "Element states",
                "Normal, focus, press, selection, and disabled appearance comes from each real control's public state and routed input.",
                new DocExample(
                    "Live element states",
                    "Click Focus target, hold Press target, or choose a list row. The specimens use their ordinary focus, pointer, selection, and availability state machines.",
                    elementStates,
                    "selectedList.SelectionChanged += OnSelectionChanged;\ndisabled.IsEnabled = false;")),
            new DocSection(
                "🧩",
                "Appearance channels",
                "Face owns foreground, background, attributes, underline, and underline color. Border and Shadow add intrinsic chrome around the same retained control.",
                new DocExample(
                    "Bounded Face channels",
                    "Each specimen changes one visible responsibility while the active Theme resolves its semantic channels.",
                    faceChannels),
                new DocExample(
                    "Flat, raised, and sunken borders",
                    "Each sample explicitly selects a relief mode while the active Theme supplies its highlight and shade colors. Turbo Vision applies relief automatically only to containers; glyph geometry stays fixed.",
                    borders),
                new DocExample(
                    "Composite, block-glyph, and fractional shadows",
                    "The three modes preserve content, replace footprint cells, or use half-row block geometry respectively.",
                    shadows)));
    }

    private static DocRow CreateColorSample(string caption, Color background)
    {
        var sample = new DocRow(
            CreateSwatch(background),
            new Text(caption))
        {
            Width = Length.Cells(22),
            Spacing = 1
        };

        return sample;
    }

    private static DocRow CreateSemanticColorSample(SemanticColor color)
    {
        var sample = new DocRow(
            CreateSwatch(color),
            new Text(color.ToString()))
        {
            Width = Length.Cells(24),
            Spacing = 1
        };

        return sample;
    }

    private static Dock CreateSwatch(ControlColor background) => new()
    {
        Width = Length.Cells(3),
        Height = Length.Cells(1),
        Face = new Face(
            SemanticColor.ControlText,
            background,
            SemanticDecoration.NormalText,
            Underline.None,
            Color.Default)
    };

    private static Dock CreateFaceSample(string caption, Face face) => new()
    {
        Width = Length.Cells(24),
        Height = Length.Cells(3),
        Face = face,
        Padding = new Thickness(1, 0),
        Children = { new Text(caption) }
    };

    private static Dock CreateBorderSample(string caption, BorderRelief relief) => new()
    {
        Width = Length.Cells(18),
        Height = Length.Cells(3),
        Border = new Border(
            BorderSide.All,
            BorderGlyphStyle.Light,
            SemanticColor.ControlBorder,
            relief,
            Color.Transparent,
            SemanticDecoration.Border),
        Padding = new Thickness(1, 0),
        Children = { new Text(caption) }
    };

    private static Dock CreateShadowSample(string caption, ShadowMode mode) => new()
    {
        Width = Length.Cells(20),
        Height = Length.Cells(3),
        Margin = new Thickness(1),
        Border = new Border(
            BorderSide.All,
            BorderGlyphStyle.Rounded,
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border),
        Shadow = new Shadow(
            true,
            mode,
            new Point(1, 1),
            new Rune('▓'),
            SemanticColor.ControlShadow,
            Color.Transparent,
            SemanticDecoration.Shadow),
        Padding = new Thickness(1, 0),
        Children = { new Text(caption) }
    };

    private static Wrap CreateElementStates()
    {
        var normal = new Button { Text = "Normal", UseMnemonic = false };
        var focusTarget = new Button { Text = "Focus target", UseMnemonic = false };
        var pressTarget = new Button { Text = "Press target", UseMnemonic = false };
        var selectedList = new ListView
        {
            Width = Length.Cells(20),
            Height = Length.Cells(2),
            Items = new object?[] { "Selected row", "Other row" }
        };
        var disabled = new Button
        {
            Text = "Disabled",
            IsEnabled = false,
            UseMnemonic = false
        };

        var states = new Wrap
        {
            Width = Length.Percent(100),
            Spacing = 2,
            LineSpacing = 1
        };
        states.Children.Add(normal);
        states.Children.Add(focusTarget);
        states.Children.Add(pressTarget);
        states.Children.Add(selectedList);
        states.Children.Add(disabled);
        return states;
    }
}
