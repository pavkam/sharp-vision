// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents CommandBarSeparator passivity, availability, Unicode fallback, and style evidence.</summary>
internal sealed class CommandBarSeparatorPane: CompositeControlBase
{
    /// <summary>Initializes the retained CommandBarSeparator documentation page.</summary>
    internal CommandBarSeparatorPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "CommandBarSeparator";

    /// <summary>Creates the retained documentation page and its owner-backed separator specimen.</summary>
    /// <returns>The complete page root.</returns>
    private static DocPage CreateContent()
    {
        var separatorLog = new Text("CommandBarSeparator.PropertyChanged: waiting") { Overflow = Overflow.Wrap };
        var neighborLog = new Text("Neighbor item event: waiting") { Overflow = Overflow.Wrap };
        var glyphs = new[]
        {
            new ControlGlyph(new Rune('╎'), new Rune('|')),
            new ControlGlyph(new Rune('┃'), new Rune('|'))
        };
        var glyphIndex = 0;
        var separator = new CommandBarSeparator
        {
            Style = CommandBarSeparatorStyle.Default with
            {
                Face = CommandBarSeparatorStyle.Default.Face with { Foreground = SemanticColor.Accent },
                Glyph = glyphs[glyphIndex]
            }
        };
        separator.PropertyChanged += (_, eventArgs) =>
            separatorLog.Content =
                $"CommandBarSeparator.PropertyChanged: {eventArgs.PropertyName} · " +
                $"{separator.Visibility} · glyph {separator.ActualStyle.Glyph.Value}";

        var compile = new CommandBarItem { Text = "&Compile" };
        var publish = new CommandBarItem { Text = "&Publish" };
        compile.Invoked += (_, eventArgs) =>
            neighborLog.Content = $"Neighbor item event: Compile ({eventArgs.Cause})";
        publish.Invoked += (_, eventArgs) =>
            neighborLog.Content = $"Neighbor item event: Publish ({eventArgs.Cause})";

        var bar = new CommandBar
        {
            Width = Length.Cells(38),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        bar.Items.Add(compile);
        bar.Items.Add(separator);
        bar.Items.Add(publish);

        var cycleGlyph = new Button { Text = "Cycle &glyph" };
        cycleGlyph.Click += (_, _) =>
        {
            glyphIndex = (glyphIndex + 1) % glyphs.Length;
            separator.Style = separator.ActualStyle with { Glyph = glyphs[glyphIndex] };
        };
        var toggleVisibility = new Button { Text = "Toggle &visibility" };
        toggleVisibility.Click += (_, _) =>
            separator.Visibility = separator.Visibility == Visibility.Visible
                ? Visibility.Hidden
                : Visibility.Visible;

        return new DocPage(
            Title,
            "<info>CommandBarSeparator</info> is a passive one-cell divider retained by a CommandBar. It participates in owner normalization but never becomes a focus, pointer, selection, or activation target.",
            new DocSection(
                "│",
                "Passive semantic divider",
                "The Unicode rule uses an ASCII fallback and an Accent semantic face. Change its style or visibility through public properties; each change is observable on the named separator itself.",
                new DocExample(
                    "Styled separator between live commands",
                    "Cycle the preferred glyph, hide or show the separator, and activate either neighbor. The separator's PropertyChanged line proves its own state while the separate item line proves interaction skips it.",
                    new DocColumn(
                        bar,
                        new DocRow(cycleGlyph, toggleVisibility),
                        separatorLog,
                        neighborLog),
                    "var separator = new CommandBarSeparator\n{\n    Style = CommandBarSeparatorStyle.Default with\n    {\n        Glyph = new ControlGlyph(new Rune('╎'), new Rune('|'))\n    }\n};\ncommands.Items.Add(separator);")));
    }
}
