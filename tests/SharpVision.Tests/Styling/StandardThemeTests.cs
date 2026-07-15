// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;



/// <summary>Verifies frozen standard theme semantic values.</summary>
public sealed class StandardThemeTests
{
    /// <summary>Verifies the dark theme supplies indexed foreground and background defaults.</summary>
    [Fact]
    public void Dark_WhenResolvedOnControl_UsesIndexedSemanticCells()
    {
        var control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, Themes.Dark);

        ThemeTestSupport.Resolve(control, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(15));
        ThemeTestSupport.Resolve(control, Control.BackgroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(0));
        ThemeTestSupport.Resolve(control, Control.BorderColorProperty, State.Normal)
            .ShouldBe(Color.Indexed(8));
    }

    /// <summary>Verifies the white theme supplies inverted indexed defaults.</summary>
    [Fact]
    public void White_WhenResolvedOnControl_UsesIndexedSemanticCells()
    {
        var control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, Themes.White);

        ThemeTestSupport.Resolve(control, Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(0));
        ThemeTestSupport.Resolve(control, Control.BackgroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(15));
    }

    /// <summary>Verifies the standard base focus state never decorates arbitrary control cells with underline.</summary>
    [Fact]
    public void Dark_WhenBaseControlIsFocused_DoesNotApplyUnderline()
    {
        var control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, Themes.Dark);

        var attributes = ThemeTestSupport.Resolve(
            control,
            Control.AttributesProperty,
            State.Focused);

        (attributes.GetValueOrDefault() & Attributes.Underline).ShouldBe(Attributes.None);
    }

    /// <summary>Verifies a focused Button uses the semantic accent on its frame.</summary>
    [Fact]
    public void Dark_WhenButtonIsFocused_UsesAccentBorder()
    {
        var control = new Button();
        ThemeTestSupport.ApplyTheme(control, Themes.Dark);

        var border = ThemeTestSupport.Resolve(
            control,
            Control.BorderColorProperty,
            State.Focused);
        var borderColor = border.ShouldNotBeNull();
        borderColor.Kind.ShouldBe(ColorKind.Indexed);
        borderColor.Red.ShouldBe((byte) 14);
    }

    /// <summary>Verifies a focused Button uses accent foreground without underline attributes.</summary>
    [Fact]
    public void Dark_WhenButtonIsFocused_UsesAccentForegroundWithoutUnderline()
    {
        var control = new Button();
        ThemeTestSupport.ApplyTheme(control, Themes.Dark);

        var foreground = ThemeTestSupport.Resolve(
            control,
            Control.ForegroundProperty,
            State.Focused);
        var foregroundColor = foreground.ShouldNotBeNull();
        foregroundColor.Kind.ShouldBe(ColorKind.Indexed);
        foregroundColor.Red.ShouldBe((byte) 14);
        var attributes = ThemeTestSupport.Resolve(
            control,
            Control.AttributesProperty,
            State.Focused);
        (attributes.GetValueOrDefault() & Attributes.Underline).ShouldBe(Attributes.None);
    }

    /// <summary>Verifies a focused TextInput uses the semantic accent foreground.</summary>
    [Fact]
    public void Dark_WhenTextInputIsFocused_UsesAccentForeground()
    {
        var control = new TextInput();
        ThemeTestSupport.ApplyTheme(control, Themes.Dark);

        var foreground = ThemeTestSupport.Resolve(
            control,
            Control.ForegroundProperty,
            State.Focused);
        var foregroundColor = foreground.ShouldNotBeNull();
        foregroundColor.Kind.ShouldBe(ColorKind.Indexed);
        foregroundColor.Red.ShouldBe((byte) 14);
    }

    /// <summary>Verifies a focused ScrollBar colors its repeated rail glyphs without decorating them.</summary>
    [Fact]
    public void Dark_WhenScrollBarIsFocused_UsesAccentWithoutUnderline()
    {
        var control = new ScrollBar();
        ThemeTestSupport.ApplyTheme(control, Themes.Dark);

        var foreground = ThemeTestSupport.Resolve(
            control,
            Control.ForegroundProperty,
            State.Focused);
        var foregroundColor = foreground.ShouldNotBeNull();
        foregroundColor.Kind.ShouldBe(ColorKind.Indexed);
        foregroundColor.Red.ShouldBe((byte) 14);
        var attributes = ThemeTestSupport.Resolve(
            control,
            Control.AttributesProperty,
            State.Focused);
        (attributes.GetValueOrDefault() & Attributes.Underline).ShouldBe(Attributes.None);
    }

    /// <summary>Verifies checked choice controls accent only their marks and retain the normal background.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Dark_WhenChoiceIsChecked_UsesAccentWithoutSelectionBackground(bool checkBox)
    {
        Control control = checkBox
            ? new CheckBox { IsChecked = true }
            : new RadioButton { IsChecked = true };
        ThemeTestSupport.ApplyTheme(control, Themes.Dark);

        var foreground = ThemeTestSupport.Resolve(
            control,
            Control.ForegroundProperty,
            State.Checked);
        var foregroundColor = foreground.ShouldNotBeNull();
        foregroundColor.Kind.ShouldBe(ColorKind.Indexed);
        foregroundColor.Red.ShouldBe((byte) 14);
        var background = ThemeTestSupport.Resolve(
            control,
            Control.BackgroundProperty,
            State.Checked);
        var backgroundColor = background.ShouldNotBeNull();
        backgroundColor.Kind.ShouldBe(ColorKind.Indexed);
        backgroundColor.Red.ShouldBe((byte) 0);
    }

    /// <summary>Verifies the standard theme publishes one compact scrollbar policy to standalone and owning controls.</summary>
    [Fact]
    public void Dark_WhenScrollBarPolicyResolves_UsesThinLinePresentation()
    {
        var rail = new ScrollBar();
        var container = new Stack();
        var editor = new TextInput();
        ThemeTestSupport.ApplyTheme(rail, Themes.Dark);
        ThemeTestSupport.ApplyTheme(container, Themes.Dark);
        ThemeTestSupport.ApplyTheme(editor, Themes.Dark);

        rail.Chrome.ShouldBe(ScrollBarChrome.Thin);
        rail.Fill.ShouldBe(ScrollBarFill.Line);
        container.ScrollBarChrome.ShouldBe(ScrollBarChrome.Thin);
        container.ScrollBarFill.ShouldBe(ScrollBarFill.Line);
        editor.ScrollBarChrome.ShouldBe(ScrollBarChrome.Thin);
        editor.ScrollBarFill.ShouldBe(ScrollBarFill.Line);
    }

    /// <summary>Verifies the standard themes are frozen, stable singletons distinct from each other.</summary>
    [Fact]
    public void Themes_AreCachedFrozenInstances()
    {
        Themes.Dark.IsFrozen.ShouldBeTrue();
        ReferenceEquals(Themes.Dark, Themes.Dark).ShouldBeTrue();
        ReferenceEquals(Themes.White, Themes.Dark).ShouldBeFalse();
    }
}
