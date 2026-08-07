// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Demonstrates intrinsic shadow modes, signed offsets, glyphs, and appearance options.</summary>
internal sealed class ShadowPane: CompositeControlBase
{
    /// <summary>Initializes the retained Shadow concept page.</summary>
    internal ShadowPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Shadow";

    private static DocPage CreateContent()
    {
        var compositeMode = CreateShadowSample("Composite mode", ShadowMode.Composite, new Point(1, 1));
        var blockMode = CreateShadowSample("Block-glyph mode", ShadowMode.BlockGlyph, new Point(1, 1));
        var fractionalMode = CreateShadowSample(
            "Fractional block · half-row depth",
            ShadowMode.FractionalBlock,
            new Point(1, 1));
        blockMode.Shadow = new Shadow(
            true,
            ShadowMode.BlockGlyph,
            new Point(1, 1),
            new Rune('░'),
            Color.Rgb(0xCD, 0xCD, 0x00),
            Color.Rgb(0x40, 0x40, 0x40),
            TerminalAttributes.Dim);

        var lowerRight = CreateShadowSample("Composite · lower right", ShadowMode.Composite, new Point(2, 1));
        var lowerLeft = CreateShadowSample("Block glyph · lower left", ShadowMode.BlockGlyph, new Point(-2, 1));
        var upperRight = CreateShadowSample("Composite · upper right", ShadowMode.Composite, new Point(2, -1));
        var upperLeft = CreateShadowSample("Block glyph · upper left", ShadowMode.BlockGlyph, new Point(-2, -1));
        upperLeft.Shadow = SampleShadow(ShadowMode.BlockGlyph, new Point(-2, -1), new Rune('▓'));

        var toggleTarget = CreateShadowSample("Toggle my shadow", ShadowMode.Composite, new Point(2, 1));
        var toggle = new CheckBox
        {
            Text = "Show shadow",
            IsChecked = true,
            UseMnemonic = false
        };
        toggle.StateChanged += (_, _) =>
            toggleTarget.Shadow = SetVisibility(toggleTarget.Shadow, toggle.IsChecked == true);

        var zeroOffset = CreateShadowSample("Offset (0,0) has no footprint", ShadowMode.Composite, default);

        return new DocPage(
            Title,
            "The complete <info>Shadow</info> value defines visibility, mode, offset, glyph, color channels, and attributes for intrinsic depth chrome.",
            new DocSection(
                "🌘",
                "Composition modes",
                "<info>Composite</info> restyles covered graphemes; <info>BlockGlyph</info> uses one configured glyph; <info>FractionalBlock</info> sculpts half-row depth with code-owned block glyphs.",
                new DocExample(
                    "Composite, block-glyph, and fractional shadows",
                    "The fractional specimen interprets Y as half-row steps. The block specimen uses a custom glyph and explicit appearance.",
                    new DocColumn(compositeMode, blockMode, fractionalMode),
                    "control.Shadow = new Shadow(true, ShadowMode.FractionalBlock, new Point(1, 1), new Rune('▓'), SemanticColor.ControlShadow, Color.Transparent, SemanticDecoration.Shadow);")),
            new DocSection(
                "🧭",
                "Signed placement",
                "Positive and negative X/Y offsets cast into every quadrant. Shadow expands visual overflow but never changes the control's desired size or hit target.",
                new DocExample(
                    "All four offset quadrants",
                    "Each specimen reserves ordinary margin so its translated footprint remains visible without pretending shadow participates in layout.",
                    new DocColumn(lowerRight, lowerLeft, upperRight, upperLeft),
                    "control.Shadow = new Shadow(true, ShadowMode.Composite, new Point(-2, -1), new Rune('▓'), SemanticColor.ControlShadow, Color.Transparent, SemanticDecoration.Shadow);")),
            new DocSection(
                "🎛️",
                "Enablement and zero offset",
                "<info>Shadow.Visible</info> is independent of its other values. A visible shadow at (0,0) deliberately has no detached footprint.",
                new DocExample(
                    "Live enable switch",
                    "Toggle the retained control without rebuilding or wrapping it.",
                    new DocColumn(toggle, toggleTarget, zeroOffset))));
    }

    private static Dock CreateShadowSample(string caption, ShadowMode mode, Point offset) => new()
    {
        Width = Length.Cells(34),
        Height = Length.Cells(4),
        Margin = new Thickness(3, 2),
        Border = new Border(
            BorderSide.All,
            BorderGlyphStyle.Rounded,
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border),
        Face = new Face(
            SemanticColor.ControlText,
            Color.Rgb(0x30, 0x30, 0x3A),
            SemanticDecoration.NormalText,
            Underline.None,
            Color.Default),
        Shadow = SampleShadow(mode, offset),
        Padding = new Thickness(1, 0),
        Children = { new Text(caption) }
    };

    private static Shadow SampleShadow(ShadowMode mode, Point offset, Rune glyph = default) => new(
        true,
        mode,
        offset,
        glyph,
        SemanticColor.ControlShadow,
        Color.Transparent,
        SemanticDecoration.Shadow);

    private static Shadow SetVisibility(Shadow shadow, bool visible) => new(
        visible,
        shadow.Mode,
        shadow.Offset,
        shadow.Glyph,
        shadow.Foreground,
        shadow.Background,
        shadow.Attributes);
}
