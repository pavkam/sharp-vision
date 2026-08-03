// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies the fixed global semantic theme document schema.</summary>
public sealed class ThemesSemanticSchemaTests
{
    /// <summary>Verifies semantic profiles load without naming CLR control types.</summary>
    [Fact]
    public void Parse_WhenSemanticDocumentIsValid_ResolvesButtonProfile()
    {
        var theme = Themes.Parse(CreateDocument(), "semantic-test");

        var normal = theme.Input.Resolve(VisualState.Normal);
        var hovered = theme.Input.Resolve(VisualState.PointerOver);

        normal.Border.Sides.ShouldBe(BorderSide.All);
        normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        normal.Border.Foreground.ThemeColor.ShouldBe(ThemeColor.ControlBorder);
        hovered.Border.Foreground.ThemeColor.ShouldBe(ThemeColor.ActiveBorder);
        theme.ResolveColor(ThemeColor.ActiveBorder).ShouldBe(Color.Rgb(0, 255, 255));
        theme.ResolveAttributes(ThemeDecoration.FocusedText).ShouldBe(TerminalAttributes.Bold);
    }

    /// <summary>Verifies the removed per-control selector root is rejected.</summary>
    [Fact]
    public void Parse_WhenControlsSelectorIsPresent_ThrowsInvalidDataException()
    {
        var json = CreateDocument().Replace(
            "\"styles\":",
            "\"controls\": {}, \"styles\":",
            StringComparison.Ordinal);

        _ = Should.Throw<InvalidDataException>(() => Themes.Parse(json, "semantic-test"));
    }

    private static string CreateDocument() => /*lang=json,strict*/ """
        {
          "name": "Semantic",
          "slug": "semantic",
          "colorScheme": "dark",
          "order": 1,
          "author": "SharpVision",
          "license": "MIT",
          "source": "https://example.invalid/semantic",
          "palette": {},
          "colors": {
            "window": "#000000",
            "windowText": "#ffffff",
            "surface": "#202020",
            "surfaceText": "#ffffff",
            "control": "#303030",
            "controlText": "#ffffff",
            "controlBorder": "#808080",
            "controlShadow": "#101010",
            "activeControl": "#404040",
            "activeText": "#ffffff",
            "activeBorder": "#00ffff",
            "focusedControl": "#303040",
            "focusedText": "#00ffff",
            "focusedBorder": "#00ffff",
            "pressedControl": "#404020",
            "pressedText": "#ffff00",
            "pressedBorder": "#ffff00",
            "selectedControl": "#0000ee",
            "selectedText": "#ffffff",
            "disabledControl": "#303030",
            "disabledText": "#606060",
            "disabledBorder": "#505050",
            "accent": "#00ffff",
            "muted": "#808080",
            "hotkey": "#ffff00",
            "error": "#ff0000",
            "warning": "#ffff00",
            "success": "#00ff00",
            "info": "#5c5cff"
          },
          "attributes": {
            "normalText": [],
            "activeText": [],
            "focusedText": "bold",
            "pressedText": [],
            "selectedText": [],
            "disabledText": [],
            "border": [],
            "shadow": "dim",
            "hotkey": "underline"
          },
          "styles": {
            "control": {
              "normal": {
                "face": {
                  "foreground": "controlText",
                  "background": "control",
                  "attributes": "normalText"
                },
                "border": {
                  "sides": "none",
                  "glyphStyle": "rounded",
                  "foreground": "controlBorder",
                  "background": "control",
                  "attributes": "border"
                },
                "shadow": {
                  "visible": false,
                  "mode": "composite",
                  "offset": { "x": 0, "y": 0 },
                  "glyph": "▓",
                  "foreground": "controlShadow",
                  "background": "transparent",
                  "attributes": "shadow"
                }
              },
              "pointerOver": {
                "face": { "foreground": "activeText", "background": "activeControl" },
                "border": { "foreground": "activeBorder" }
              },
              "focused": {
                "face": { "foreground": "focusedText", "background": "focusedControl", "attributes": "focusedText" },
                "border": { "foreground": "focusedBorder" }
              },
              "pressed": {
                "face": { "foreground": "pressedText", "background": "pressedControl" },
                "border": { "foreground": "pressedBorder" }
              },
              "selected": {
                "face": { "foreground": "selectedText", "background": "selectedControl" }
              },
              "disabled": {
                "face": { "foreground": "disabledText", "background": "disabledControl" },
                "border": { "foreground": "disabledBorder" }
              }
            },
            "input": {
              "normal": { "border": { "sides": "all", "glyphStyle": "heavy" } }
            },
            "container": {
              "normal": { "border": { "sides": "all", "glyphStyle": "light" } }
            },
            "window": {
              "normal": { "border": { "sides": "all", "glyphStyle": "paired" } }
            },
            "popup": {
              "normal": { "border": { "sides": "all", "glyphStyle": "rounded" } }
            }
          },
          "status": {}
        }
        """;
}
