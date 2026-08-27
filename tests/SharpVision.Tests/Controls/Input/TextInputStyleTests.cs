// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies the immutable TextInput presentation record: its code-owned default, its
/// declared one-hop fallback to <see cref="InputStyle"/>'s "input" key, and its invalidation
/// policy.</summary>
public sealed class TextInputStyleTests
{
    /// <summary>Verifies Default carries InputStyle's own default Face/Border/Shadow verbatim -
    /// TextInput adds no structural members of its own.</summary>
    [Fact]
    public void Default_ResolvesThemeInputStyleDefaults()
    {
        TextInputStyle.Default.Face.ShouldBe(InputStyle.Default.Face);
        TextInputStyle.Default.Border.ShouldBe(InputStyle.Default.Border);
        TextInputStyle.Default.Shadow.ShouldBe(InputStyle.Default.Shadow);
        TextInputStyle.Default.DropDownGlyph.ShouldBe(InputStyle.Default.DropDownGlyph);
        TextInputStyle.Default.AffixGap.ShouldBe(InputStyle.Default.AffixGap);
    }

    /// <summary>Verifies equality compares every record member structurally, the free record behavior.</summary>
    [Fact]
    public void Equality_WhenEveryMemberMatches_IsEqual()
    {
        var equivalent = new TextInputStyle(TextInputStyle.Default.Face, TextInputStyle.Default.Border, TextInputStyle.Default.Shadow);

        equivalent.ShouldBe(TextInputStyle.Default);
        (equivalent == TextInputStyle.Default).ShouldBeTrue();
    }

    /// <summary>Verifies an unauthored theme resolves to the "input" role section's own customization.</summary>
    [Fact]
    public void Definition_Resolve_WhenNoLocalAndThemeDoesNotAuthorTextInput_FallsBackToInput()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(inputGlyphStyle: "\"rounded\""));

        var resolved = TextInputStyle.Definition.Resolve(null, theme);

        resolved.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Rounded);
    }

    /// <summary>Verifies a local override always wins over both the theme and the fallback.</summary>
    [Fact]
    public void Definition_Resolve_WhenLocalIsSupplied_LocalWins()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(inputGlyphStyle: "\"rounded\""));
        var local = TextInputStyle.Default with
        {
            Border = TextInputStyle.Default.Border with { GlyphStyle = BorderGlyphStyle.Ascii }
        };

        var resolved = TextInputStyle.Definition.Resolve(local, theme);

        resolved.ShouldBe(local);
    }

    /// <summary>Verifies a complete local typed style remains authoritative in every interactive
    /// state rather than having fallback Theme deltas cascaded over it.</summary>
    [Theory]
    [InlineData(VisualState.IsPointerOver)]
    [InlineData(VisualState.FocusWithin)]
    [InlineData(VisualState.Focused)]
    [InlineData(VisualState.Disabled)]
    [InlineData(VisualState.FocusWithin | VisualState.Focused)]
    public void ResolveAppearance_WhenCompleteLocalStyleIsAssigned_LocalWinsThemeStates(VisualState state)
    {
        // Arrange
        var local = TextInputStyle.Default with
        {
            Face = new Face(
                Color.Rgb(1, 2, 3),
                Color.Rgb(4, 5, 6),
                TerminalAttributes.Bold,
                Underline.None,
                Color.Default),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Paired,
                Color.Rgb(7, 8, 9),
                Color.Rgb(10, 11, 12),
                TerminalAttributes.Italic),
            Shadow = ControlStyle.NoShadow
        };
        using var input = new TextInput { Style = local };
        var expected = input.ResolveAppearance(ThemeCatalog.Dark);

        // Act
        var actual = input.ResolveAppearance(ThemeCatalog.Dark, state);

        // Assert
        actual.ShouldBe(expected);
    }

    /// <summary>Verifies an AffixGap change is classified as a measure-affecting invalidation.</summary>
    [Fact]
    public void Definition_Compare_WhenAffixGapChanges_IsMeasure()
    {
        var previous = TextInputStyle.Default;
        var current = previous with { AffixGap = previous.AffixGap + 1 };

        TextInputStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>Verifies a change that alters neither the affix gap nor any semantic paint member is non-invalidating.</summary>
    [Fact]
    public void Definition_Compare_WhenNothingRelevantChanges_IsNone()
    {
        var previous = TextInputStyle.Default;
        var current = previous with { AffixGap = previous.AffixGap };

        TextInputStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.None);
    }

    /// <summary>Verifies TextInputStyle.Default is a TextInputStyle, like every other style type's -
    /// without the explicit <c>new</c> hider, the expression would resolve to the inherited
    /// <see cref="InputStyle"/> member instead.</summary>
    [Fact]
    public void Default_WhenRead_IsATextInputStyle() =>
        TextInputStyle.Default.ShouldBeOfType<TextInputStyle>();

    /// <summary>Verifies the Default preset round-trips through the public Style surface.</summary>
    [Fact]
    public void Style_WhenAssignedTheDefault_RoundTrips()
    {
        using var input = new TextInput();

        input.Style = TextInputStyle.Default;

        input.Style.ShouldBe(TextInputStyle.Default);
    }
}
