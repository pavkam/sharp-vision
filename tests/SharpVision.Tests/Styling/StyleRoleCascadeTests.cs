// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies the four role sections layered onto the original six - <c>button</c>,
/// <c>toggle</c>, <c>item</c>, and <c>panel</c> - inherit exactly what their parent roles resolve
/// until a theme authors them, and win over that inheritance once it does.</summary>
public sealed class StyleRoleCascadeTests
{
    private static readonly string[] _interactiveStates = ["pointerOver", "focusWithin", "focused", "current", "checked", "pressed", "selected", "disabled"];

    /// <summary>Verifies every bundled theme that leaves <c>button</c> unauthored resolves the same
    /// chrome in every state as its <c>input</c> section, plus the code-owned padding - the exact
    /// appearance a Button had when it was a leaf falling back to <c>input</c>.</summary>
    [Fact]
    public void GetStyleSet_WhenButtonIsUnauthored_ResolvesInputChromeWithCodeOwnedPadding()
    {
        foreach (var slug in ThemeCatalog.Slugs.Where(static slug => slug != "turbo-vision"))
        {
            var theme = ThemeCatalog.Load(slug);
            var input = theme.GetStyleSet(InputStyle.Default);
            var button = theme.GetStyleSet(ButtonStyle.Default);

            button.Normal.Padding.ShouldBe(ButtonStyle.Standard.Padding, slug);
            button.Normal.AffixGap.ShouldBe(input.Normal.AffixGap, slug);
            button.Normal.DropDownGlyph.ShouldBe(input.Normal.DropDownGlyph, slug);
            ChromeOf(button.Normal).ShouldBe(ChromeOf(input.Normal), slug);

            foreach (var state in _interactiveStates)
            {
                var expected = StateOf(input, state);
                var actual = StateOf(button, state);

                (actual is null).ShouldBe(expected is null, $"{slug} button {state}");

                if (expected is not null)
                {
                    ChromeOf(actual!).ShouldBe(ChromeOf(expected), $"{slug} button {state}");
                }
            }
        }
    }

    /// <summary>Verifies every bundled theme that leaves <c>toggle</c> unauthored resolves the same
    /// chrome in every state as its <c>input</c> section, so check boxes and radio buttons look
    /// exactly as they did when they fell back to <c>input</c> directly.</summary>
    [Fact]
    public void GetStyleSet_WhenToggleIsUnauthored_ResolvesInputChrome()
    {
        foreach (var slug in ThemeCatalog.Slugs.Where(static slug => slug != "turbo-vision"))
        {
            var theme = ThemeCatalog.Load(slug);
            var input = theme.GetStyleSet(InputStyle.Default);
            var toggle = theme.GetStyleSet(ToggleStyle.Default);

            ChromeOf(toggle.Normal).ShouldBe(ChromeOf(input.Normal), slug);
            toggle.Normal.AffixGap.ShouldBe(input.Normal.AffixGap, slug);

            foreach (var state in _interactiveStates)
            {
                var expected = StateOf(input, state);
                var actual = StateOf(toggle, state);

                (actual is null).ShouldBe(expected is null, $"{slug} toggle {state}");

                if (expected is not null)
                {
                    ChromeOf(actual!).ShouldBe(ChromeOf(expected), $"{slug} toggle {state}");
                }
            }
        }
    }

    /// <summary>Verifies an unauthored <c>item</c> section is the selectable-row rule: Normal is the
    /// passive control face, hover changes only the text while keeping the row's own background,
    /// and press, selection, and disablement follow <c>input</c>'s deltas.</summary>
    [Fact]
    public void GetStyleSet_WhenItemIsUnauthored_ResolvesTheSelectableRowRule()
    {
        foreach (var slug in ThemeCatalog.Slugs.Where(static slug => slug != "turbo-vision"))
        {
            var theme = ThemeCatalog.Load(slug);
            var control = theme.GetStyleSet(ControlStyle.Default);
            var input = theme.GetStyleSet(InputStyle.Default);
            var item = theme.GetStyleSet(ItemStyle.Default);

            ChromeOf(item.Normal).ShouldBe(ChromeOf(control.Normal), slug);
            item.Normal.Border.Sides.ShouldBe(BorderSide.None, slug);

            var hover = item.IsPointerOver.ShouldNotBeNull(slug);
            hover.Face.Background.ShouldBe(control.Normal.Face.Background, $"{slug} hover keeps the row background");
            hover.Face.Foreground.ShouldBe(input.IsPointerOver!.Face.Foreground, $"{slug} hover adopts the input text");

            var pressed = item.Pressed.ShouldNotBeNull(slug);
            pressed.Face.Background.ShouldBe(input.Pressed!.Face.Background, $"{slug} press adopts the input fill");

            var selected = item.Selected.ShouldNotBeNull(slug);
            selected.Face.Background.ShouldBe(input.Selected!.Face.Background, $"{slug} selection adopts the input fill");
        }
    }

    /// <summary>Verifies the row set every list, tree, table, tab, and navigation row resolves is
    /// the <c>item</c> section itself, so authoring <c>styles.item</c> reaches every row consumer
    /// through the one path they already share.</summary>
    [Fact]
    public void GetInteractiveRowStyleSet_WhenResolved_IsTheItemSection()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var item = theme.GetStyleSet(ItemStyle.Default);
            var rows = theme.GetInteractiveRowStyleSet();

            rows.Normal.ShouldBeSameAs(item.Normal, slug);
            rows.IsPointerOver.ShouldBeSameAs(item.IsPointerOver, slug);
            rows.Selected.ShouldBeSameAs(item.Selected, slug);
            rows.Disabled.ShouldBeSameAs(item.Disabled, slug);
        }
    }

    /// <summary>Verifies an unauthored <c>panel</c> section is the passive control face with no
    /// interaction states, so every layout panel paints exactly as it did before the section
    /// existed.</summary>
    [Fact]
    public void GetStyleSet_WhenPanelIsUnauthored_ResolvesThePassiveControlFace()
    {
        foreach (var slug in ThemeCatalog.Slugs.Where(static slug => slug != "turbo-vision"))
        {
            var theme = ThemeCatalog.Load(slug);
            var control = theme.GetStyleSet(ControlStyle.Default);
            var panel = theme.GetStyleSet(PanelStyle.Default);

            ChromeOf(panel.Normal).ShouldBe(ChromeOf(control.Normal), slug);
            panel.IsPointerOver.ShouldBeNull(slug);
            panel.Focused.ShouldBeNull(slug);
            panel.Pressed.ShouldBeNull(slug);
            panel.Selected.ShouldBeNull(slug);
        }
    }

    /// <summary>Verifies a <c>button</c> section wins over the <c>input</c> inheritance member by
    /// member: an authored face replaces the input face, an authored border and shadow replace the
    /// input chrome, an authored structural member (padding) replaces the code-owned one, and a
    /// state the section does not author still follows <c>input</c>'s own state delta.</summary>
    [Fact]
    public void GetStyleSet_WhenButtonIsAuthored_WinsOverInputAndStillInheritsUnauthoredStates()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            inputStates: """, "pressed": { "face": { "background": "pressedControl" } }""",
            extraStyles: """
                , "button": { "normal": {
                    "padding": { "x": 3, "y": 0 },
                    "face": { "background": "accent" },
                    "border": { "sides": "none" },
                    "shadow": { "visible": true, "mode": "fractionalBlock", "offset": { "x": 1, "y": 1 } }
                } }
                """));
        var input = theme.GetStyleSet(InputStyle.Default);

        var button = theme.GetStyleSet(ButtonStyle.Default);

        button.Normal.Padding.ShouldBe(new Thickness(horizontal: 3, vertical: 0));
        button.Normal.Face.Background.ShouldBe((ControlColor) SemanticColor.Accent);
        button.Normal.Face.Foreground.ShouldBe(input.Normal.Face.Foreground);
        button.Normal.Border.Sides.ShouldBe(BorderSide.None);
        button.Normal.Shadow.IsVisible.ShouldBeTrue();
        button.Normal.Shadow.Mode.ShouldBe(ShadowMode.FractionalBlock);
        button.Pressed.ShouldNotBeNull().Face.Background.ShouldBe((ControlColor) SemanticColor.PressedControl);
        button.Focused.ShouldNotBeNull().Face.Foreground.ShouldBe((ControlColor) SemanticColor.FocusedText);
    }

    /// <summary>Verifies a structural member the child type shares with its parent type follows the
    /// parent: an <c>input</c> affix gap reaches <c>button</c> and <c>toggle</c> without either
    /// section repeating it, exactly as the leaf completions that predated the roles forwarded it.</summary>
    [Fact]
    public void GetStyleSet_WhenInputAuthorsAffixGap_ButtonAndToggleAdoptIt()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(inputExtra: ", \"affixGap\": 3"));

        theme.GetStyleSet(ButtonStyle.Default).Normal.AffixGap.ShouldBe(3);
        theme.GetStyleSet(ToggleStyle.Default).Normal.AffixGap.ShouldBe(3);
    }

    /// <summary>Verifies a role two hops from <c>control</c> still receives what <c>control</c>
    /// authored, through <c>input</c>: a theme that colors <c>control</c> and never mentions
    /// <c>input</c> or <c>button</c> still resolves those colors on a button.</summary>
    [Fact]
    public void GetStyleSet_WhenOnlyControlIsAuthored_ButtonInheritsThroughInput()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(stylesOverride: /*lang=json,strict*/ """
            { "control": { "normal": { "face": { "foreground": "controlText", "background": "control" } } } }
            """));

        var button = theme.GetStyleSet(ButtonStyle.Default);

        button.Normal.Face.Foreground.ShouldBe((ControlColor) SemanticColor.ControlText);
        button.Normal.Face.Background.ShouldBe((ControlColor) SemanticColor.Control);
        button.Normal.Border.Sides.ShouldBe(InputStyle.Default.Border.Sides);
    }

    /// <summary>Verifies a transparent <c>panel</c> face is a legal authored value - the way a
    /// theme makes layout pure arrangement - and leaves the passive control face untouched.</summary>
    [Fact]
    public void GetStyleSet_WhenPanelAuthorsATransparentBackground_ControlKeepsItsOwnFace()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            extraStyles: """, "panel": { "normal": { "face": { "background": "transparent" } } }"""));

        theme.GetStyleSet(PanelStyle.Default).Normal.Face.Background.ShouldBe((ControlColor) Color.Transparent);
        theme.GetStyleSet(ControlStyle.Default).Normal.Face.Background.ShouldBe((ControlColor) SemanticColor.Control);
    }

    /// <summary>Verifies a programmatically configured <c>input</c> set cascades into the roles
    /// below it the same way a parsed section does, so a theme built in code sees its buttons and
    /// toggles follow the input it configured.</summary>
    [Fact]
    public void SetStyleSet_WhenInputIsConfiguredProgrammatically_ButtonFollowsIt()
    {
        var theme = new Theme();
        var input = InputStyle.Default with
        {
            Face = InputStyle.Default.Face with { Background = Color.Rgb(10, 20, 30) }
        };
        theme.SetStyleSet(new StyleStates<InputStyle> { Normal = input });
        theme.Freeze();

        theme.GetStyleSet(ButtonStyle.Default).Normal.Face.Background.ShouldBe((ControlColor) Color.Rgb(10, 20, 30));
    }

    private static ControlAppearance ChromeOf(ControlStyle style) => new(style.Face, style.Border, style.Shadow);

    private static TStyle? StateOf<TStyle>(StyleStates<TStyle> set, string state)
        where TStyle : ControlStyle => state switch
        {
            "pointerOver" => set.IsPointerOver,
            "focusWithin" => set.FocusWithin,
            "focused" => set.Focused,
            "current" => set.Current,
            "selected" => set.Selected,
            "checked" => set.Checked,
            "indeterminate" => set.Indeterminate,
            "pressed" => set.Pressed,
            "disabled" => set.Disabled,
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
}
