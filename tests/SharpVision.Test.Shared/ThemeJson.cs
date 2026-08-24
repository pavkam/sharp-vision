// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Test.Shared;

/// <summary>Builds complete semantic theme documents for focused loader tests.</summary>
public static class ThemeJson
{
    // Fourteen roles the "colors" section has always filled from fixed literals rather than a
    // parameter - controlShadow, disabled*, the four status colors plus muted, and the six
    // chromatic colors (red/green/yellow/blue/magenta/cyan). Every "colors.*" entry must name a
    // palette key rather than embed a raw hex literal, so these are appended to the palette under
    // reserved "__"-prefixed keys regardless of what a caller passes as its own "palette" argument.
    private const string _reservedPalette =
        "\"__controlShadow\":\"#303030\",\"__disabledText\":\"#707070\",\"__disabledBorder\":\"#606060\"," +
        "\"__error\":\"#ff0000\",\"__warning\":\"#ffff00\",\"__success\":\"#00ff00\",\"__info\":\"#0000ff\"," +
        "\"__muted\":\"#707070\"," +
        "\"__red\":\"#ff0000\",\"__green\":\"#00ff00\",\"__yellow\":\"#ffff00\",\"__blue\":\"#0000ff\"," +
        "\"__magenta\":\"#ff00ff\",\"__cyan\":\"#00ffff\"";

    /// <summary>Number of palette entries <see cref="Create"/> always appends beyond a caller's own
    /// "palette" argument when every color-role parameter is left at its (hex) default - the
    /// fourteen reserved roles above plus background/foreground/accent, which default to hex
    /// literals and so each synthesize one more reserved entry. A caller computing an exact
    /// palette-entry-count boundary against the produced document must subtract this.</summary>
    public const int DefaultReservedPaletteEntryCount = 17;

    /// <summary>Creates one complete semantic theme document. <paramref name="background"/>,
    /// <paramref name="foreground"/>, <paramref name="accent"/>, <paramref name="hotkey"/>, and
    /// <paramref name="controlBorderForeground"/> each accept either a raw hex literal - which this
    /// method synthesizes into its own reserved palette entry, since a hex literal is no longer
    /// legal directly in "colors.*" - or the name of a key already present in
    /// <paramref name="palette"/>, passed straight through.</summary>
    public static string Create(
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
        string extraStyles = "",
        string? glyphs = null)
    {
        var glyphsField = glyphs is null ? "" : $", \"glyphs\": \"{glyphs}\"";
        var (backgroundRef, backgroundEntry) = ColorRef("background", background);
        var (foregroundRef, foregroundEntry) = ColorRef("foreground", foreground);
        var (accentRef, accentEntry) = ColorRef("accent", accent);

        string hotkeyRef;
        string? hotkeyEntry;
        if (hotkey is null)
        {
            hotkeyRef = accentRef;
            hotkeyEntry = null;
        }
        else
        {
            (hotkeyRef, hotkeyEntry) = ColorRef("hotkey", hotkey);
        }

        string controlBorderRef;
        string? controlBorderEntry;
        if (controlBorderForeground is null)
        {
            controlBorderRef = foregroundRef;
            controlBorderEntry = null;
        }
        else
        {
            (controlBorderRef, controlBorderEntry) = ColorRef("controlBorder", controlBorderForeground);
        }

        var extraPalette = string.Concat(
            backgroundEntry, foregroundEntry, accentEntry, hotkeyEntry, controlBorderEntry);

        return $$"""
            { "name": "{{name}}", "slug": "t", "colorScheme": "dark", "order": 1,
              "author": "A", "license": "MIT", "source": "s"{{glyphsField}},
              "palette": { {{palette}}, {{_reservedPalette}}{{extraPalette}} },
              "colors": {
                "window":"{{backgroundRef}}", "windowSurface":"{{backgroundRef}}", "windowText":"{{foregroundRef}}",
                "surface":"{{backgroundRef}}", "surfaceText":"{{foregroundRef}}",
                "control":"{{backgroundRef}}", "controlText":"{{foregroundRef}}",
                "controlBorder":"{{controlBorderRef}}", "controlShadow":"__controlShadow",
                "activeControl":"{{backgroundRef}}", "activeText":"{{foregroundRef}}", "activeBorder":"{{accentRef}}",
                "focusedControl":"{{backgroundRef}}", "focusedText":"{{accentRef}}", "focusedBorder":"{{accentRef}}",
                "pressedControl":"{{backgroundRef}}", "pressedText":"{{accentRef}}", "pressedBorder":"{{accentRef}}",
                "selectedControl":"{{accentRef}}", "selectedText":"{{foregroundRef}}",
                "disabledControl":"{{backgroundRef}}", "disabledText":"__disabledText", "disabledBorder":"__disabledBorder",
                "accent":"{{accentRef}}", "muted":"__muted", "hotkey":"{{hotkeyRef}}",
                "error":"__error", "warning":"__warning", "success":"__success", "info":"__info",
                "red":"__red", "green":"__green", "yellow":"__yellow", "blue":"__blue",
                "magenta":"__magenta", "cyan":"__cyan"
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
                } {{controlExtra}} },
                "input": { "normal": { "border": { "sides":{{inputSides}}, "glyphStyle":{{inputGlyphStyle}} }
                {{inputExtra}} },
                "focused": { "face": { "foreground":"focusedText", "attributes":"focusedText" }, "border": { "foreground":"focusedBorder" } }
                {{inputStates}} },
                "container": { "normal": { "border": { "sides":{{containerSides}}, "glyphStyle":"light" } } },
                "window": { "normal": { "border": { "sides":"all", "glyphStyle":"paired" }{{windowExtra}} } },
                "popup": { "normal": { "border": { "sides":"all", "glyphStyle":"rounded" } } },
                "tooltip": { "normal": { "border": { "sides":"none" } } }
                {{extraStyles}}
              } }
            """;
    }

    private static (string Reference, string? PaletteEntry) ColorRef(string role, string value) =>
        value.StartsWith('#')
            ? ($"__{role}", $",\"__{role}\":\"{value}\"")
            : (value, null);
}
