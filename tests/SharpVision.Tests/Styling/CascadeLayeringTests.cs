// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Verifies both branches of the per-state cascade layer rather than replace, and that
/// what cascades is what the source theme actually authored rather than its whole resolved value.
///
/// <para>The two branches used to disagree. The root path - the five well-known styles other than
/// <c>control</c> - built a basis of <c>normal + control's delta</c> and then overlaid the key's own
/// JSON: additive, so a narrowing override refines what the parent said. The leaf path did the
/// opposite, returning early the moment the leaf's own key authored the state at all, discarding
/// everything the fallback had authored for it.</para>
///
/// <para>Normal had the mirror-image problem in the other direction: it copied <c>control</c>'s
/// Face, Border, and Shadow wholesale, guarded only by "this theme has no <c>control</c> section",
/// so authoring one silently replaced every sibling's code-owned chrome. Since a border's
/// <c>Sides</c> reserves layout space, that moved measured widths application-wide.</para>
///
/// <para>Latent for the fifteen bundled themes - none authors a non-<c>normal</c> state on a leaf
/// key, and all fifteen re-author <c>border</c> on every sibling section, which is the workaround
/// that made the Normal defect invisible while looking like ordinary redundancy.</para>
/// </summary>
public sealed class CascadeLayeringTests
{
    /// <summary>The regression the leaf half exists to pin, in the shape the report gives it: a
    /// Button authoring only a disabled border still inherits the disabled foreground its fallback
    /// authored.</summary>
    [Fact]
    public void BuildFallbackAwareStates_WhenTheLeafNarrowsAnAuthoredState_KeepsTheFallbacksContribution()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            inputStates: """, "disabled": { "face": { "foreground": "disabledText" } } """,
            extraStyles: """, "button": { "disabled": { "border": { "foreground": "disabledBorder" } } } """));

        var resolved = ButtonStyle.Definition.Appearance!(ButtonStyle.Definition.Resolve(null, theme), theme)
            .Resolve(VisualState.Disabled);

        resolved.Face.Foreground.ShouldBe(
            (ControlColor) SemanticColor.DisabledText,
            "the leaf authored a border, which should narrow the fallback rather than replace it");
        resolved.Border.Foreground.ShouldBe((ControlColor) SemanticColor.DisabledBorder);
    }

    /// <summary>The counter-case that makes "layer" mean something: where both sides author the
    /// same member, the leaf still wins. Layering must not turn a leaf override into a suggestion.</summary>
    [Fact]
    public void BuildFallbackAwareStates_WhenBothAuthorTheSameMember_TheLeafWins()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            inputStates: """, "disabled": { "face": { "foreground": "disabledText" } } """,
            extraStyles: """, "button": { "disabled": { "face": { "foreground": "accent" } } } """));

        ButtonStyle.Definition.Appearance!(ButtonStyle.Definition.Resolve(null, theme), theme)
            .Resolve(VisualState.Disabled)
            .Face.Foreground.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>Verifies the diagnosis the report calls hardest to attribute: deleting the leaf's
    /// own block must not <em>restore</em> a member the leaf never touched. Before the fix the two
    /// spellings disagreed, so the fix looked like the cause.</summary>
    [Fact]
    public void BuildFallbackAwareStates_WhenTheLeafBlockIsRemoved_TheFallbackForegroundIsUnchanged()
    {
        var withLeafBlock = ThemeCatalog.Parse(ThemeJson.Create(
            inputStates: """, "disabled": { "face": { "foreground": "disabledText" } } """,
            extraStyles: """, "button": { "disabled": { "border": { "foreground": "disabledBorder" } } } """));
        var withoutLeafBlock = ThemeCatalog.Parse(ThemeJson.Create(
            inputStates: """, "disabled": { "face": { "foreground": "disabledText" } } """));

        DisabledForeground(withLeafBlock).ShouldBe(DisabledForeground(withoutLeafBlock));
    }

    /// <summary>Verifies a theme authoring only <c>control</c> colors leaves every sibling's
    /// code-owned border intact. Input's Heavy/All, Container's Light, Window's Paired, and Popup's
    /// Rounded were all replaced by Control's borderless default.</summary>
    [Fact]
    public void GetStyleSet_WhenOnlyControlColorsAreAuthored_SiblingsKeepTheirCodeOwnedChrome()
    {
        var theme = ThemeCatalog.Parse(_colorsOnlyTheme);

        theme.GetStyleSet(InputStyle.Default).Normal.Border.Sides.ShouldBe(InputStyle.Default.Border.Sides);
        theme.GetStyleSet(InputStyle.Default).Normal.Border.GlyphStyle.ShouldBe(InputStyle.Default.Border.GlyphStyle);
        theme.GetStyleSet(ContainerStyle.Default).Normal.Border.GlyphStyle
            .ShouldBe(ContainerStyle.Default.Border.GlyphStyle);
        theme.GetStyleSet(WindowStyle.Default).Normal.Border.GlyphStyle.ShouldBe(WindowStyle.Default.Border.GlyphStyle);
        theme.GetStyleSet(PopupStyle.Default).Normal.Border.GlyphStyle.ShouldBe(PopupStyle.Default.Border.GlyphStyle);
    }

    /// <summary>Verifies Window's composite shadow survives too - the one sibling whose distinctive
    /// default is more than a border, and the one whose loss changes the visual footprint.</summary>
    [Fact]
    public void GetStyleSet_WhenOnlyControlColorsAreAuthored_WindowKeepsItsShadow()
    {
        var shadow = ThemeCatalog.Parse(_colorsOnlyTheme).GetStyleSet(WindowStyle.Default).Normal.Shadow;

        shadow.Visible.ShouldBe(WindowStyle.Default.Shadow.Visible);
        shadow.Mode.ShouldBe(WindowStyle.Default.Shadow.Mode);
        shadow.Offset.ShouldBe(WindowStyle.Default.Shadow.Offset);
    }

    /// <summary>The point of the cascade, which the fix must not break: what <c>control</c> DOES
    /// author still reaches every sibling.</summary>
    [Fact]
    public void GetStyleSet_WhenControlAuthorsColors_SiblingsStillInheritThem()
    {
        var theme = ThemeCatalog.Parse(_colorsOnlyTheme);

        theme.GetStyleSet(InputStyle.Default).Normal.Face.Foreground
            .ShouldBe((ControlColor) SemanticColor.ControlText);
        theme.GetStyleSet(ContainerStyle.Default).Normal.Face.Background
            .ShouldBe((ControlColor) SemanticColor.Control);
    }

    /// <summary>Verifies a theme that explicitly authors <c>control</c>'s border still cascades it.
    /// The fix narrows the cascade to what the theme wrote, not to colors: an author who writes
    /// <c>control.normal.border.sides</c> means it.</summary>
    [Fact]
    public void GetStyleSet_WhenControlAuthorsItsBorder_SiblingsInheritThatToo()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        theme.GetStyleSet(TooltipStyle.Default).Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Rounded);
    }

    /// <summary>Verifies the bundled themes' repeated sibling <c>border</c> blocks still win, so the
    /// change makes them redundant rather than inverting them.</summary>
    [Fact]
    public void GetStyleSet_WhenASiblingReAuthorsItsBorder_ThatStillWins()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(inputSides: "\"all\"", inputGlyphStyle: "\"light\""));

        var border = theme.GetStyleSet(InputStyle.Default).Normal.Border;

        border.Sides.ShouldBe(BorderSide.All);
        border.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
    }

    /// <summary>Verifies a root style honours an assigned local value as its resting appearance.
    /// The root factory discarded the resolved style when it built appearance, so a control using it
    /// as its primary slot reported the local value from <c>ActualStyle</c> while every rendered cell
    /// came from the theme.</summary>
    [Fact]
    public void BuildRootStates_WhenALocalStyleIsAssigned_ItBecomesTheNormalAppearance()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());
        var local = RootDefault() with
        {
            Face = ControlStyle.DefaultFace with { Foreground = Color.Rgb(9, 8, 7) }
        };

        var states = theme.BuildRootStates(local, "test.root", RootDefault());

        states.Normal.Face.Foreground.ShouldBe((ControlColor) Color.Rgb(9, 8, 7));
    }

    /// <summary>Verifies the theme's own per-state contributions survive a local style, since a
    /// local value replaces the resting appearance rather than the theme's opinion about how this
    /// type reacts to a state.</summary>
    [Fact]
    public void BuildRootStates_WhenALocalStyleIsAssigned_TheThemesStateDeltasStillApply()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            extraStyles: """, "test.root": { "pressed": { "face": { "foreground": "accent" } } } """));
        var local = RootDefault() with
        {
            Face = ControlStyle.DefaultFace with { Foreground = Color.Rgb(9, 8, 7) }
        };

        var states = theme.BuildRootStates(local, "test.root", RootDefault());

        states.Normal.Face.Foreground.ShouldBe((ControlColor) Color.Rgb(9, 8, 7));
        states.Resolve(VisualState.Pressed).Face.Foreground.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>The counter-case: with no local style the result is exactly the theme's own set.</summary>
    [Fact]
    public void BuildRootStates_WhenNoLocalStyleIsAssigned_MatchesTheThemesOwnSet()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            extraStyles: """, "test.root": { "pressed": { "face": { "foreground": "accent" } } } """));
        var set = theme.GetStyleSet("test.root", RootDefault());

        var states = theme.BuildRootStates(set.Normal, "test.root", RootDefault());

        states.Normal.ShouldBe(set.ToAppearanceStates().Normal);
        states.Resolve(VisualState.Pressed).ShouldBe(set.ToAppearanceStates().Resolve(VisualState.Pressed));
    }

    // Authors control's colors and nothing else - no border, no shadow, and no sibling sections at
    // all. That is the shape the whole Normal-cascade defect turns on, and no bundled theme has it.
    [StringSyntax(StringSyntaxAttribute.Json)]
    private const string _colorsOnlyTheme = """
        { "name": "T", "slug": "t", "colorScheme": "dark", "order": 1,
          "author": "A", "license": "MIT", "source": "s",
          "colors": {
            "window":"#101010", "windowSurface":"#101010", "windowText":"#e0e0e0",
            "surface":"#101010", "surfaceText":"#e0e0e0",
            "control":"#101010", "controlText":"#e0e0e0",
            "controlBorder":"#e0e0e0", "controlShadow":"#303030",
            "activeControl":"#101010", "activeText":"#e0e0e0", "activeBorder":"#77aaff",
            "focusedControl":"#101010", "focusedText":"#77aaff", "focusedBorder":"#77aaff",
            "pressedControl":"#101010", "pressedText":"#77aaff", "pressedBorder":"#77aaff",
            "selectedControl":"#77aaff", "selectedText":"#e0e0e0",
            "disabledControl":"#101010", "disabledText":"#707070", "disabledBorder":"#606060",
            "accent":"#77aaff", "muted":"#707070", "hotkey":"#77aaff",
            "error":"#ff0000", "warning":"#ffff00", "success":"#00ff00", "info":"#0000ff"
          },
          "attributes": {
            "normalText":[], "activeText":[], "focusedText":"bold", "pressedText":[],
            "selectedText":[], "disabledText":[], "border":[], "shadow":"dim", "hotkey":"underline"
          },
          "styles": {
            "control": { "normal": {
              "face": { "foreground":"controlText", "background":"control", "attributes":"normalText" }
            } }
          } }
        """;

    private static ControlColor DisabledForeground(Theme theme) =>
        ButtonStyle.Definition.Appearance!(ButtonStyle.Definition.Resolve(null, theme), theme)
            .Resolve(VisualState.Disabled)
            .Face.Foreground;

    private static TestRootStyle RootDefault() =>
        new(ControlStyle.DefaultFace, ControlStyle.NoBorder, ControlStyle.NoShadow);

    private sealed record TestRootStyle: ControlStyle
    {
        [SetsRequiredMembers]
        public TestRootStyle(Face face, Border border, Shadow shadow) : base(face, border, shadow)
        {
        }
    }
}
