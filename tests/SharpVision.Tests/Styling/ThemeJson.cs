// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Builds complete semantic theme documents for focused loader tests.</summary>
internal static class ThemeJson
{
    /// <summary>Creates one complete semantic theme document.</summary>
    internal static string Create(
        string palette = "\"bg\":\"#101010\",\"fg\":\"#e0e0e0\"",
        string name = "T",
        string background = "#101010",
        string foreground = "#e0e0e0",
        string accent = "#77aaff",
        string? hotkey = null,
        string inputGlyphStyle = "\"heavy\"",
        string inputSides = "\"all\"",
        string containerSides = "\"all\"",
        string inputExtra = "",
        string inputStates = "",
        string controlExtra = "",
        string windowExtra = "",
        string? controlBorderForeground = null,
        string extraStyles = "") => $$"""
            { "name": "{{name}}", "slug": "t", "colorScheme": "dark", "order": 1,
              "author": "A", "license": "MIT", "source": "s",
              "palette": { {{palette}} },
              "colors": {
                "window":"{{background}}", "windowSurface":"{{background}}", "windowText":"{{foreground}}",
                "surface":"{{background}}", "surfaceText":"{{foreground}}",
                "control":"{{background}}", "controlText":"{{foreground}}",
                "controlBorder":"{{controlBorderForeground ?? foreground}}", "controlShadow":"#303030",
                "activeControl":"{{background}}", "activeText":"{{foreground}}", "activeBorder":"{{accent}}",
                "focusedControl":"{{background}}", "focusedText":"{{accent}}", "focusedBorder":"{{accent}}",
                "pressedControl":"{{background}}", "pressedText":"{{accent}}", "pressedBorder":"{{accent}}",
                "selectedControl":"{{accent}}", "selectedText":"{{foreground}}",
                "disabledControl":"{{background}}", "disabledText":"#707070", "disabledBorder":"#606060",
                "accent":"{{accent}}", "muted":"#707070", "hotkey":"{{hotkey ?? accent}}",
                "error":"#ff0000", "warning":"#ffff00", "success":"#00ff00", "info":"#0000ff"
              },
              "attributes": {
                "normalText":[], "activeText":[], "focusedText":"bold", "pressedText":[],
                "selectedText":[], "disabledText":[], "border":[], "shadow":"dim", "hotkey":"underline"
              },
              "styles": {
                "control": { "normal": {
                  "face": { "foreground":"controlText", "background":"control", "attributes":"normalText" },
                  "border": { "sides":"none", "glyphStyle":"rounded", "foreground":"controlBorder", "background":"control", "attributes":"border" },
                  "shadow": { "visible":false, "mode":"composite", "offset":{"x":0,"y":0}, "glyph":"▓", "foreground":"controlShadow", "background":"transparent", "attributes":"shadow" }
                }, "focused": { "face": { "foreground":"focusedText", "attributes":"focusedText" }, "border": { "foreground":"focusedBorder" } }
                {{controlExtra}} },
                "input": { "normal": { "border": { "sides":{{inputSides}}, "glyphStyle":{{inputGlyphStyle}} }
                {{inputExtra}} }
                {{inputStates}} },
                "container": { "normal": { "border": { "sides":{{containerSides}}, "glyphStyle":"light" } } },
                "window": { "normal": { "border": { "sides":"all", "glyphStyle":"paired" }{{windowExtra}} } },
                "popup": { "normal": { "border": { "sides":"all", "glyphStyle":"rounded" } } },
                "tooltip": { "normal": { "border": { "sides":"none" } } }
                {{extraStyles}}
              } }
            """;
}
