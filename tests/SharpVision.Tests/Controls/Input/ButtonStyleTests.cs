// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies the immutable Button presentation record: its two code-owned presets, its
/// declared one-hop fallback to <see cref="InputStyle"/>'s "input" key, and its
/// invalidation policy.</summary>
public sealed class ButtonStyleTests
{
    /// <summary>Verifies Standard carries InputStyle's own default Face/Border/Shadow and the
    /// established one-cell horizontal padding.</summary>
    [Fact]
    public void Standard_ResolvesThemeInputStyleDefaultsWithOneCellPadding()
    {
        ButtonStyle.Standard.Face.ShouldBe(InputStyle.Default.Face);
        ButtonStyle.Standard.Border.ShouldBe(InputStyle.Default.Border);
        ButtonStyle.Standard.Shadow.ShouldBe(InputStyle.Default.Shadow);
        ButtonStyle.Standard.Padding.ShouldBe(new Thickness(horizontal: 1, vertical: 0));
    }

    /// <summary>Verifies the filled preset preserves the established compact fractional-shadow recipe.</summary>
    [Fact]
    public void Filled_UsesCompactFractionalProfile()
    {
        var actual = ButtonStyle.Filled;

        actual.Padding.ShouldBe(new Thickness(horizontal: 2, vertical: 0));
        actual.Border.Sides.ShouldBe(BorderSide.None);
        actual.Shadow.IsVisible.ShouldBeTrue();
        actual.Shadow.Mode.ShouldBe(ShadowMode.FractionalBlock);
        actual.Shadow.Offset.ShouldBe(new Point(1, 1));
    }

    /// <summary>Verifies equality compares every record member structurally, the free record behavior.</summary>
    [Fact]
    public void Equality_WhenEveryMemberMatches_IsEqual()
    {
        var equivalent = new ButtonStyle(ButtonStyle.Standard.Face, ButtonStyle.Standard.Border, ButtonStyle.Standard.Shadow, ButtonStyle.Standard.Padding);

        equivalent.ShouldBe(ButtonStyle.Standard);
        (equivalent == ButtonStyle.Standard).ShouldBeTrue();
        equivalent.ShouldNotBe(ButtonStyle.Filled);
    }

    /// <summary>Verifies an unauthored theme resolves to Standard with no local override.</summary>
    [Fact]
    public void Definition_Resolve_WhenNoLocalAndThemeDoesNotAuthorButton_FallsBackToInput()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var resolved = ButtonStyle.Definition.Resolve(null, theme);

        resolved.Padding.ShouldBe(new Thickness(horizontal: 1, vertical: 0));
        resolved.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
    }

    /// <summary>Verifies a local override always wins over both the theme and the fallback.</summary>
    [Fact]
    public void Definition_Resolve_WhenLocalIsSupplied_LocalWins()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var resolved = ButtonStyle.Definition.Resolve(ButtonStyle.Filled, theme);

        resolved.ShouldBe(ButtonStyle.Filled);
    }

    /// <summary>Verifies a complete local typed style remains authoritative in every interactive
    /// state rather than having fallback Theme deltas cascaded over it.</summary>
    [Theory]
    [InlineData(VisualState.IsPointerOver)]
    [InlineData(VisualState.FocusWithin)]
    [InlineData(VisualState.Focused)]
    [InlineData(VisualState.Pressed)]
    [InlineData(VisualState.Disabled)]
    [InlineData(VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed)]
    public void ResolveAppearance_WhenCompleteLocalStyleIsAssigned_LocalWinsThemeStates(VisualState state)
    {
        // Arrange
        var local = ButtonStyle.Standard with
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
        using var button = new Button { Style = local };
        var expected = button.ResolveAppearance(ThemeCatalog.Dark);

        // Act
        var actual = button.ResolveAppearance(ThemeCatalog.Dark, state);

        // Assert
        actual.ShouldBe(expected);
    }

    /// <summary>Verifies a Padding change is classified as a measure-affecting invalidation.</summary>
    [Fact]
    public void Definition_Compare_WhenPaddingChanges_IsMeasure()
    {
        var previous = ButtonStyle.Standard;
        var current = previous with { Padding = new Thickness(horizontal: 2, vertical: 0) };

        ButtonStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>Verifies a change that alters neither padding nor pressed-translation is non-invalidating.</summary>
    [Fact]
    public void Definition_Compare_WhenNothingRelevantChanges_IsNone()
    {
        var previous = ButtonStyle.Standard;
        var current = previous with { Face = previous.Face with { Foreground = SemanticColor.Accent } };

        ButtonStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.None);
    }
    /// <summary>Verifies ButtonStyle.Default is a ButtonStyle, like every other style type's.
    ///
    /// <para>ButtonStyle declared only Standard and Filled, so <c>ButtonStyle.Default</c> was still
    /// a legal expression - it resolved to the inherited <c>InputStyle</c> member and returned the
    /// base type, without <c>Padding</c>. <c>button.Style = ButtonStyle.Default</c> therefore failed
    /// to convert while the identical line compiled for every sibling control, and nothing at the
    /// use site signalled the difference.</para>
    /// </summary>
    [Fact]
    public void Default_WhenRead_IsAButtonStyleCarryingPadding()
    {
        _ = ButtonStyle.Default.ShouldBeOfType<ButtonStyle>();
        ButtonStyle.Default.ShouldBe(ButtonStyle.Standard);
        ButtonStyle.Default.Padding.ShouldBe(ButtonStyle.Standard.Padding);
    }

    /// <summary>Verifies the assignment the shadowing used to reject now compiles and round-trips,
    /// which is the consumer-visible half of the defect.</summary>
    [Fact]
    public void Style_WhenAssignedTheDefault_RoundTrips()
    {
        using var button = new Button();

        button.Style = ButtonStyle.Default;

        button.Style.ShouldBe(ButtonStyle.Standard);
    }

}
