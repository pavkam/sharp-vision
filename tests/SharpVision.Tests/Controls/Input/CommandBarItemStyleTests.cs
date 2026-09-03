// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies the immutable CommandBarItem presentation and fallback forwarding.</summary>
public sealed class CommandBarItemStyleTests
{
    /// <summary>Verifies the default is one-row compact and chromeless while retaining input state colors.</summary>
    [Fact]
    public void Default_WhenRead_UsesCompactBorderlessPresentation()
    {
        var style = CommandBarItemStyle.Default;

        style.Face.ShouldBe(InputStyle.Default.Face with { Background = SemanticColor.Bar });
        style.Border.Sides.ShouldBe(BorderSide.None);
        style.Shadow.IsVisible.ShouldBeFalse();
        style.Padding.ShouldBe(new Thickness(horizontal: 1, vertical: 0));
        style.DropDownGlyph.ShouldBe(InputStyle.Default.DropDownGlyph);
        style.AffixGap.ShouldBe(InputStyle.Default.AffixGap);
    }

    /// <summary>Verifies a themed fallback forwards DropDownGlyph and AffixGap instead of pinning literals.</summary>
    [Fact]
    public void Definition_WhenInputFallbackChanges_ForwardsSharedMembers()
    {
        var input = InputStyle.Default with
        {
            DropDownGlyph = new Rune('v'),
            AffixGap = 3
        };
        var theme = new Theme();
        theme.SetStyleSet(new StyleStates<InputStyle> { Normal = input });

        var resolved = CommandBarItemStyle.Definition.Resolve(null, theme);

        resolved.DropDownGlyph.ShouldBe(new Rune('v'));
        resolved.AffixGap.ShouldBe(3);
    }

    /// <summary>Verifies either local structural member requires measurement.</summary>
    [Fact]
    public void Definition_Compare_WhenPaddingOrAffixGapChanges_IsMeasure()
    {
        var style = CommandBarItemStyle.Default;

        CommandBarItemStyle.Definition.Compare(
                style,
                null,
                style with { Padding = new Thickness(2) },
                null)
            .ShouldBe(InvalidationImpact.Measure);
        CommandBarItemStyle.Definition.Compare(
                style,
                null,
                style with { AffixGap = 2 },
                null)
            .ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>Verifies local item styling round-trips through the typed slot.</summary>
    [Fact]
    public void Style_WhenAssigned_RoundTrips()
    {
        using var item = new CommandBarItem();
        var local = CommandBarItemStyle.Default with { Padding = default };

        item.Style = local;

        item.Style.ShouldBe(local);
        item.ActualStyle.ShouldBe(local);
    }

    /// <summary>Verifies Bar supplies the resting, hovered, and disabled plane without erasing
    /// inherited state foregrounds, active backgrounds, or complete local-style precedence.</summary>
    [Fact]
    public void ResolveAppearance_WhenBarAndStatesAreAuthored_PreservesStateAndLocalPrecedence()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            bar: "#345678",
            controlExtra: """, "disabled": { "face": { "foreground":"disabledText", "background":"disabledControl" } }""",
            inputStates: """, "pointerOver": { "face": { "foreground":"activeText", "background":"activeControl" } }, "selected": { "face": { "foreground":"selectedText", "background":"selectedControl" } }"""));
        using var item = new CommandBarItem();

        var normal = item.ResolveAppearance(theme);
        var hovered = item.ResolveAppearance(theme, VisualState.IsPointerOver);
        var selectedHovered = item.ResolveAppearance(
            theme,
            VisualState.IsPointerOver | VisualState.Selected);
        var focused = item.ResolveAppearance(theme, VisualState.Focused);
        var selected = item.ResolveAppearance(theme, VisualState.Selected);
        var disabled = item.ResolveAppearance(theme, VisualState.Disabled);
        var selectedDisabled = item.ResolveAppearance(
            theme,
            VisualState.Selected | VisualState.Disabled);

        normal.Face.Background.Literal.ShouldBe(theme.ResolveColor(SemanticColor.Bar));
        hovered.Face.Background.ShouldBe(normal.Face.Background);
        hovered.Face.Foreground.Literal.ShouldBe(theme.ResolveColor(SemanticColor.ActiveText));
        focused.Face.Background.ShouldBe(normal.Face.Background);
        focused.Face.Foreground.Literal.ShouldBe(theme.ResolveColor(SemanticColor.FocusedText));
        focused.Face.Attributes.Literal.ShouldBe(TerminalAttributes.Bold);
        selected.Face.Background.Literal.ShouldBe(theme.ResolveColor(SemanticColor.SelectedControl));
        selectedHovered.Face.Foreground.ShouldBe(selected.Face.Foreground);
        selectedHovered.Face.Background.ShouldBe(selected.Face.Background);
        disabled.Face.Foreground.Literal.ShouldBe(theme.ResolveColor(SemanticColor.DisabledText));
        disabled.Face.Background.ShouldBe(normal.Face.Background);
        selectedDisabled.Face.Foreground.ShouldBe(disabled.Face.Foreground);
        selectedDisabled.Face.Background.ShouldBe(normal.Face.Background);

        var localBackground = Color.Rgb(120, 30, 60);
        item.Style = CommandBarItemStyle.Default with
        {
            Face = CommandBarItemStyle.Default.Face with { Background = localBackground }
        };

        foreach (var state in new[]
                 {
                     VisualState.IsPointerOver,
                     VisualState.Focused,
                     VisualState.Selected,
                     VisualState.Disabled
                 })
        {
            item.ResolveAppearance(theme, state).Face.Background.Literal.ShouldBe(localBackground);
        }
    }

    /// <summary>Verifies FocusWithin is visibly distinct from Normal under a bundled theme.
    /// Unlike Focused - which every bundled theme explicitly authors an "input.focused.face"
    /// background for (<c>focusedControl</c>), giving a command-bar item a real background swap
    /// away from its own Bar-rebased Normal even before this fix - no bundled theme authors an
    /// "input.focusWithin" section at all. FocusWithin therefore falls all the way through to
    /// <see cref="BarAppearance.CompleteFace{TStyle}"/>'s own Bar-preserving branch exactly like
    /// Normal does, leaving it colorwise byte-identical to Normal (same Bar background, same
    /// foreground) - the borderless-focus-indication gap this issue describes, reachable here via
    /// <see cref="StyleDefinitions.BarControlWithThemeOwnedStateDefaults{TStyle,TFallback}"/>'s use
    /// of <c>Theme.BuildFallbackAwareStates</c>. Confirmed empirically (revert-and-observe) before
    /// fixing: pre-fix, FocusWithin resolved with no distinguishing attribute either.</summary>
    [Fact]
    public void ResolveAppearance_WhenFocusWithinUnderBundledTheme_DiffersFromNormal()
    {
        using var item = new CommandBarItem();

        var normal = item.ResolveAppearance(ThemeCatalog.Dark);
        var focusWithin = item.ResolveAppearance(ThemeCatalog.Dark, VisualState.FocusWithin);

        // The color collapse itself: no bundled theme authors "input.focusWithin", so this state's
        // colors resolve exactly like Normal's own Bar-plane colors - exactly what would leave a
        // borderless command-bar item with no visible cue if the reverse-video safety net were not
        // engaged.
        focusWithin.Face.Foreground.ShouldBe(normal.Face.Foreground);
        focusWithin.Face.Background.ShouldBe(normal.Face.Background);

        // The safety net forces Reverse on top, making the two states visibly distinct in spite of
        // the color collapse above.
        focusWithin.ShouldNotBe(normal);
        focusWithin.Face.Attributes.IsLiteral.ShouldBeTrue();
        focusWithin.Face.Attributes.Literal.HasFlag(TerminalAttributes.Reverse).ShouldBeTrue();
    }

    /// <summary>Verifies Focused already differs from Normal under a bundled theme through its own
    /// authored background (every bundled theme's "input.focused.face.background" maps to
    /// <c>focusedControl</c>, which <see cref="BarAppearance.CompleteFace{TStyle}"/> honors instead
    /// of rebasing onto Bar, and which always differs from a theme's own <c>bar</c> color) - a case
    /// the reverse-video safety net must recognize as already-visible and leave alone rather than
    /// fight. Reverse must NOT be forced here, since that would double up an already-adequate cue
    /// and this control's Face.Attributes would then depend on the safety net rather than the
    /// theme's own authored Focused decoration.</summary>
    [Fact]
    public void ResolveAppearance_WhenFocusedUnderBundledTheme_UsesOwnAuthoredBackgroundWithoutFallback()
    {
        using var item = new CommandBarItem();

        var normal = item.ResolveAppearance(ThemeCatalog.Dark);
        var focused = item.ResolveAppearance(ThemeCatalog.Dark, VisualState.Focused);

        // Already visibly distinct via background alone - focusedControl vs Bar - even though the
        // foreground happens to collapse (both resolve to controlText/focusedText's shared white).
        focused.Face.Foreground.ShouldBe(normal.Face.Foreground);
        focused.Face.Background.ShouldNotBe(normal.Face.Background);
        focused.Face.Background.Literal.ShouldBe(ThemeCatalog.Dark.ResolveColor(SemanticColor.FocusedControl));

        // The fallback must not fight the theme's own authored decoration by forcing Reverse on
        // top: Focused already carries a real background difference, so the fallback recognizes it
        // as already-visible and leaves it exactly as authored (bold, no reverse).
        focused.Face.Attributes.IsLiteral.ShouldBeTrue();
        focused.Face.Attributes.Literal.ShouldBe(TerminalAttributes.Bold);
    }
}
