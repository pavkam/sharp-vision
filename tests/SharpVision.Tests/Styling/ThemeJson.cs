// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Builds compact schema-two theme documents for focused loader tests.</summary>
internal static class ThemeJson
{
    private const string _glyphs = /*lang=json,strict*/ """
        {"chrome":{"topLeft":{"value":"╭","fallback":"+"},"top":{"value":"─","fallback":"-"},"topRight":{"value":"╮","fallback":"+"},"right":{"value":"│","fallback":"|"},"bottomRight":{"value":"╯","fallback":"+"},"bottom":{"value":"─","fallback":"-"},"bottomLeft":{"value":"╰","fallback":"+"},"left":{"value":"│","fallback":"|"},"shadow":{"value":"▓","fallback":"#"},"windowClose":{"value":"✕","fallback":"x"}},"progress":{"empty":{"value":"░","fallback":"."},"full":{"value":"█","fallback":"#"},"indeterminate":{"value":"▒","fallback":"?"},"horizontalFractions":[{"value":"░","fallback":"."},{"value":"▏","fallback":":"},{"value":"▎","fallback":"-"},{"value":"▍","fallback":"="},{"value":"▌","fallback":"+"},{"value":"▋","fallback":"*"},{"value":"▊","fallback":"%"},{"value":"▉","fallback":"@"},{"value":"█","fallback":"#"}],"verticalFractions":[{"value":"░","fallback":"."},{"value":"▁","fallback":":"},{"value":"▂","fallback":"-"},{"value":"▃","fallback":"="},{"value":"▄","fallback":"+"},{"value":"▅","fallback":"*"},{"value":"▆","fallback":"%"},{"value":"▇","fallback":"@"},{"value":"█","fallback":"#"}]},"disclosure":{"collapsed":{"value":"▶","fallback":">"},"expanded":{"value":"▼","fallback":"v"},"dropDown":{"value":"▼","fallback":"v"}},"selection":{"checkBoxBracketUnchecked":{"value":" ","fallback":" "},"checkBoxBracketChecked":{"value":"✓","fallback":"x"},"checkBoxBracketIndeterminate":{"value":"─","fallback":"-"},"checkBoxTickUnchecked":{"value":"○","fallback":"o"},"checkBoxTickChecked":{"value":"✓","fallback":"x"},"checkBoxTickIndeterminate":{"value":"−","fallback":"-"},"checkBoxSquareUnchecked":{"value":"☐","fallback":"o"},"checkBoxSquareChecked":{"value":"☑","fallback":"x"},"checkBoxSquareIndeterminate":{"value":"◩","fallback":"-"},"radioUnchecked":{"value":"○","fallback":"o"},"radioChecked":{"value":"◉","fallback":"x"},"menuCheckUnchecked":{"value":" ","fallback":" "},"menuCheckChecked":{"value":"✓","fallback":"x"},"menuRadioUnchecked":{"value":"○","fallback":"o"},"menuRadioChecked":{"value":"◉","fallback":"x"}},"navigation":{"itemIdle":{"value":"·","fallback":"."},"itemCurrent":{"value":"›","fallback":">"},"groupCollapsed":{"value":"▶","fallback":">"},"groupExpanded":{"value":"▼","fallback":"v"},"separator":{"value":"─","fallback":"-"}},"scrollBars":{"verticalDecrement":{"value":"▲","fallback":"^"},"verticalIncrement":{"value":"▼","fallback":"v"},"horizontalDecrement":{"value":"◀","fallback":"<"},"horizontalIncrement":{"value":"▶","fallback":">"},"blockTrack":{"value":"░","fallback":"."},"blockThumb":{"value":"▓","fallback":"#"},"horizontalLineTrack":{"value":"─","fallback":"-"},"horizontalLineThumb":{"value":"━","fallback":"="},"verticalLineTrack":{"value":"│","fallback":"|"},"verticalLineThumb":{"value":"┃","fallback":"#"}},"separators":{"horizontal":{"value":"─","fallback":"-"},"vertical":{"value":"│","fallback":"|"},"menu":{"value":"─","fallback":"-"},"tableHorizontal":{"value":"─","fallback":"-"},"tableVertical":{"value":"│","fallback":"|"},"tableCross":{"value":"┼","fallback":"+"},"tabDivider":{"value":"│","fallback":"|"},"tabUnderline":{"value":"─","fallback":"-"}},"text":{"ellipsis":{"value":"…","fallback":"."}}}
        """;

    /// <summary>Gets the complete raw glyph object for malformed-document tests.</summary>
    internal static string Glyphs => _glyphs;

    /// <summary>Creates one complete schema-two theme document from raw palette and role members.</summary>
    /// <param name="roles">Raw comma-separated role members.</param>
    /// <param name="palette">Raw comma-separated palette members.</param>
    /// <param name="name">The JSON-escaped display-name content.</param>
    /// <returns>A complete strict JSON document.</returns>
    internal static string Create(
        string roles,
        string palette = "\"bg\":\"#101010\",\"fg\":\"#e0e0e0\"",
        string name = "T") => $$"""
        { "version": 2, "name": "{{name}}", "slug": "t", "colorScheme": "dark", "order": 1,
          "author": "A", "license": "MIT", "source": "s",
          "palette": { {{palette}} }, "roles": { {{roles}} }, "glyphs": {{_glyphs}} }
        """;
}
