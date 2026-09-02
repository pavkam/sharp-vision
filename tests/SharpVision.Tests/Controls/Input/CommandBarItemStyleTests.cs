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

    /// <summary>Verifies Bar supplies the resting plane without erasing inherited focus,
    /// selection, disabled, or complete local-style precedence.</summary>
    [Fact]
    public void ResolveAppearance_WhenBarAndStatesAreAuthored_PreservesStateAndLocalPrecedence()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            bar: "#345678",
            controlExtra: """, "disabled": { "face": { "foreground":"disabledText", "background":"disabledControl" } }""",
            inputStates: """, "selected": { "face": { "foreground":"selectedText", "background":"selectedControl" } }"""));
        using var item = new CommandBarItem();

        var normal = item.ResolveAppearance(theme);
        var focused = item.ResolveAppearance(theme, VisualState.Focused);
        var selected = item.ResolveAppearance(theme, VisualState.Selected);
        var disabled = item.ResolveAppearance(theme, VisualState.Disabled);

        normal.Face.Background.Literal.ShouldBe(theme.ResolveColor(SemanticColor.Bar));
        focused.Face.Background.ShouldBe(normal.Face.Background);
        focused.Face.Foreground.Literal.ShouldBe(theme.ResolveColor(SemanticColor.FocusedText));
        focused.Face.Attributes.Literal.ShouldBe(TerminalAttributes.Bold);
        selected.Face.Background.Literal.ShouldBe(theme.ResolveColor(SemanticColor.SelectedControl));
        disabled.Face.Foreground.Literal.ShouldBe(theme.ResolveColor(SemanticColor.DisabledText));
        disabled.Face.Background.Literal.ShouldBe(theme.ResolveColor(SemanticColor.DisabledControl));

        var localBackground = Color.Rgb(120, 30, 60);
        item.Style = CommandBarItemStyle.Default with
        {
            Face = CommandBarItemStyle.Default.Face with { Background = localBackground }
        };

        foreach (var state in new[] { VisualState.Focused, VisualState.Selected, VisualState.Disabled })
        {
            item.ResolveAppearance(theme, state).Face.Background.Literal.ShouldBe(localBackground);
        }
    }
}
