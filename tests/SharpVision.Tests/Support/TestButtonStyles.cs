// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Builds complete Button styles for tests that exercise style-owned chrome.</summary>
internal static class TestButtonStyles
{
    /// <summary>Gets a borderless, shadowless Button presentation.</summary>
    internal static ButtonStyle Flat { get; } = WithShadow(AppearanceTestValues.Shadow(visible: false));

    /// <summary>Builds a borderless, shadowless Button presentation with explicit padding.</summary>
    internal static ButtonStyle FlatWithPadding(Thickness padding)
    {
        var style = Flat;
        return new ButtonStyle(padding, style.Appearance);
    }

    /// <summary>Builds a borderless Button presentation with one explicit shadow.</summary>
    /// <param name="shadow">The complete shadow presentation.</param>
    /// <returns>A validated complete Button style.</returns>
    internal static ButtonStyle WithShadow(Shadow shadow) => Create(
        AppearanceTestValues.Border(BorderSide.None),
        shadow);

    /// <summary>Builds a shadowless Button presentation with one explicit border.</summary>
    /// <param name="border">The complete border presentation.</param>
    /// <returns>A validated complete Button style.</returns>
    internal static ButtonStyle WithBorder(Border border) => Create(
        border,
        AppearanceTestValues.Shadow(visible: false));

    private static ButtonStyle Create(Border border, Shadow shadow)
    {
        var baseline = ButtonStyle.Standard;
        var appearance = baseline.Appearance;
        var normal = new ThemeAppearance(appearance.Normal.Face, border, shadow);
        var profile = new ThemeProfile(
            normal,
            appearance.PointerOver,
            appearance.FocusWithin,
            appearance.Focused,
            appearance.Current,
            appearance.Selected,
            appearance.Checked,
            appearance.Indeterminate,
            appearance.Pressed,
            appearance.Disabled);

        return new ButtonStyle(baseline.Padding, profile);
    }
}
