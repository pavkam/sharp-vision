// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Test.Shared;

/// <summary>Builds complete semantic theme documents for focused loader tests.</summary>
public static class ThemeJson
{
    // Twelve roles the "colors" section has always filled from fixed literals rather than a
    // parameter - controlShadow, disabled*, three of the four status colors, and the six chromatic
    // colors (red/green/yellow/blue/magenta/cyan). Every "colors.*" entry must name a palette key
    // rather than embed a raw hex literal, so these are appended to the palette under reserved
    // "__"-prefixed keys regardless of what a caller passes as its own "palette" argument. "error"
    // and "muted" moved out to their own parameters below - see DefaultReservedPaletteEntryCount.
    private const string _reservedPalette =
        "\"__controlShadow\":\"#303030\",\"__disabledText\":\"#707070\",\"__disabledBorder\":\"#606060\"," +
        "\"__warning\":\"#ffff00\",\"__success\":\"#00ff00\",\"__info\":\"#0000ff\"," +
        "\"__red\":\"#ff0000\",\"__green\":\"#00ff00\",\"__yellow\":\"#ffff00\",\"__blue\":\"#0000ff\"," +
        "\"__magenta\":\"#ff00ff\",\"__cyan\":\"#00ffff\"";

    /// <summary>Number of palette entries <see cref="Create"/> always appends beyond a caller's own
    /// "palette" argument when every color-role parameter is left at its (hex) default - the
    /// twelve reserved roles above plus background/foreground/accent/muted/error, which default to
    /// hex literals and so each synthesize one more reserved entry. A caller computing an exact
    /// palette-entry-count boundary against the produced document must subtract this.</summary>
    public const int DefaultReservedPaletteEntryCount = 17;

    /// <summary>Creates one complete semantic theme document. <paramref name="background"/>,
    /// <paramref name="foreground"/>, <paramref name="accent"/>, <paramref name="muted"/>,
    /// <paramref name="error"/>, <paramref name="hotkey"/>,
    /// <paramref name="controlBorderForeground"/>, <paramref name="selectedText"/>, and
    /// <paramref name="selectedControl"/> each accept either a raw hex literal - which this
    /// method synthesizes into its own reserved palette entry, since a hex literal is no longer
    /// legal directly in "colors.*" - or the name of a key already present in
    /// <paramref name="palette"/>, passed straight through. <paramref name="selectedText"/> and
    /// <paramref name="selectedControl"/> default to <paramref name="foreground"/> and
    /// <paramref name="accent"/> respectively, matching every bundled theme's own convention.
    /// <paramref name="stylesOverride"/>, when non-null, replaces the entire generated "styles"
    /// object verbatim (a document's "styles" object is legally empty, so <c>"{}"</c> produces a
    /// theme where every well-known style resolves purely from its code-owned default - useful for
    /// proving behavior that only surfaces when a theme's "control" section is entirely
    /// unauthored, since every other <see cref="Create"/> call authors "control", and the five
    /// sibling well-known keys otherwise cascade its authored face/border/shadow delta onto their
    /// own code-owned defaults regardless of whether they author a "face" of their own.</summary>
    public static string Create(
        string palette = "\"bg\":\"#101010\",\"fg\":\"#e0e0e0\"",
        string name = "T",
        string background = "#101010",
        string foreground = "#e0e0e0",
        string accent = "#77aaff",
        string muted = "#707070",
        string error = "#ff0000",
        string? hotkey = null,
        string inputGlyphStyle = "\"heavy\"",
        string inputSides = "\"all\"",
        string containerSides = "\"all\"",
        string controlSides = "\"none\"",
        string inputBorderExtra = "",
        string inputExtra = "",
        string inputStates = "",
        string controlNormalExtra = "",
        string controlExtra = "",
        string windowExtra = "",
        string? controlBorderForeground = null,
        string extraStyles = "",
        string? glyphs = null,
        string? selectedText = null,
        string? selectedControl = null,
        string? stylesOverride = null)
    {
        var glyphsField = glyphs is null ? "" : $", \"glyphs\": \"{glyphs}\"";
        var (backgroundRef, backgroundEntry) = ColorRef("background", background);
        var (foregroundRef, foregroundEntry) = ColorRef("foreground", foreground);
        var (accentRef, accentEntry) = ColorRef("accent", accent);
        var (mutedRef, mutedEntry) = ColorRef("muted", muted);
        var (errorRef, errorEntry) = ColorRef("error", error);

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

        string selectedTextRef;
        string? selectedTextEntry;
        if (selectedText is null)
        {
            selectedTextRef = foregroundRef;
            selectedTextEntry = null;
        }
        else
        {
            (selectedTextRef, selectedTextEntry) = ColorRef("selectedText", selectedText);
        }

        string selectedControlRef;
        string? selectedControlEntry;
        if (selectedControl is null)
        {
            selectedControlRef = accentRef;
            selectedControlEntry = null;
        }
        else
        {
            (selectedControlRef, selectedControlEntry) = ColorRef("selectedControl", selectedControl);
        }

        var extraPalette = string.Concat(
            backgroundEntry,
            foregroundEntry,
            accentEntry,
            mutedEntry,
            errorEntry,
            hotkeyEntry,
            controlBorderEntry,
            selectedTextEntry,
            selectedControlEntry);

        var stylesField = stylesOverride ?? $$"""
            {
                "control": { "normal": {
                  "face": { "foreground":"controlText", "background":"control", "attributes":"normalText" },
                  "border": { "sides":{{controlSides}}, "glyphStyle":"rounded", "foreground":"controlBorder", "background":"control", "attributes":"border" },
                  "shadow": { "visible":false, "mode":"composite", "offset":{"x":0,"y":0}, "glyph":"▓", "foreground":"controlShadow", "background":"transparent", "attributes":"shadow" }
                  {{controlNormalExtra}}
                } {{controlExtra}} },
                "input": { "normal": { "border": { "sides":{{inputSides}}, "glyphStyle":{{inputGlyphStyle}}{{inputBorderExtra}} }
                {{inputExtra}} },
                "focused": { "face": { "foreground":"focusedText", "attributes":"focusedText" }, "border": { "foreground":"focusedBorder" } }
                {{inputStates}} },
                "container": { "normal": { "border": { "sides":{{containerSides}}, "glyphStyle":"light" } } },
                "window": { "normal": { "border": { "sides":"all", "glyphStyle":"paired" }{{windowExtra}} } },
                "popup": { "normal": { "border": { "sides":"all", "glyphStyle":"rounded" } } },
                "tooltip": { "normal": { "border": { "sides":"none" } } }
                {{extraStyles}}
              }
            """;

        return $$"""
            { "name": "{{name}}", "slug": "t", "colorScheme": "dark", "order": 1,
              "author": "A", "license": "MIT", "source": "https://example.invalid/theme"{{glyphsField}},
              "palette": { {{palette}}, {{_reservedPalette}}{{extraPalette}} },
              "colors": {
                "window":"{{backgroundRef}}", "windowSurface":"{{backgroundRef}}", "windowText":"{{foregroundRef}}",
                "surface":"{{backgroundRef}}", "surfaceText":"{{foregroundRef}}",
                "control":"{{backgroundRef}}", "controlText":"{{foregroundRef}}",
                "controlBorder":"{{controlBorderRef}}", "controlShadow":"__controlShadow",
                "reliefHighlight":"{{foregroundRef}}", "reliefShade":"__controlShadow",
                "activeControl":"{{backgroundRef}}", "activeText":"{{foregroundRef}}", "activeBorder":"{{accentRef}}",
                "focusedControl":"{{backgroundRef}}", "focusedText":"{{accentRef}}", "focusedBorder":"{{accentRef}}",
                "pressedControl":"{{backgroundRef}}", "pressedText":"{{accentRef}}", "pressedBorder":"{{accentRef}}",
                "selectedControl":"{{selectedControlRef}}", "selectedText":"{{selectedTextRef}}",
                "disabledControl":"{{backgroundRef}}", "disabledText":"__disabledText", "disabledBorder":"__disabledBorder",
                "accent":"{{accentRef}}", "muted":"{{mutedRef}}", "hotkey":"{{hotkeyRef}}",
                "error":"{{errorRef}}", "warning":"__warning", "success":"__success", "info":"__info",
                "red":"__red", "green":"__green", "yellow":"__yellow", "blue":"__blue",
                "magenta":"__magenta", "cyan":"__cyan"
              },
              "attributes": {
                "normalText":[], "activeText":[], "focusedText":"bold", "pressedText":[],
                "selectedText":[], "disabledText":[], "border":[], "shadow":"dim", "hotkey":"underline"
              },
              "styles": {{stylesField}} }
            """;
    }

    private static (string Reference, string? PaletteEntry) ColorRef(string role, string value) =>
        value.StartsWith('#')
            ? ($"__{role}", $",\"__{role}\":\"{value}\"")
            : (value, null);
}
