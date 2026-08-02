// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Exercises the protected complete-style resolution seam through a Button-shaped test style.</summary>
public sealed class StyledProbe: Control
{
    /// <summary>Gets or sets the complete local test style, or null for Theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ButtonStyle? Style
    {
        get;
        set => _ = SetControlStyle(
            ref field,
            value,
            ThrowOnStyleResolution
                ? static (local, theme) => local is not null
                    ? throw new InvalidOperationException("resolve")
                    : ResolveStyle(local, theme)
                : ResolveStyle,
            ThrowOnCompareStructure
                ? static (_, _, _, _) => throw new InvalidOperationException("compare")
                : ReturnInvalidImpact
                    ? static (_, _, _, _) => (InvalidationImpact) int.MaxValue
                    : CompareStructure,
            ThrowOnAppearanceSelection
                ? static _ => throw new InvalidOperationException("appearance")
                : static style => style.Appearance,
            nameof(Style),
            nameof(ActualStyle));
    }

    /// <summary>Gets the complete local, Theme-owned, or fallback style.</summary>
    public ButtonStyle ActualStyle => ResolveStyle(Style, Theme);

    /// <summary>Gets the protected complete appearance profile selected by the resolved style.</summary>
    public ThemeProfile Profile => AppearanceProfile;

    /// <summary>Gets the number of content measurement callbacks.</summary>
    public int MeasureCalls { get; private set; }

    /// <summary>Gets the inherited Theme observed while the most recent prospective Theme impact was calculated.</summary>
    public Theme? ThemeObservedDuringImpact { get; private set; }

    /// <summary>Gets or sets whether prospective local-style resolution throws.</summary>
    public bool ThrowOnStyleResolution { get; set; }

    /// <summary>Gets or sets whether prospective structural comparison throws.</summary>
    public bool ThrowOnCompareStructure { get; set; }

    /// <summary>Gets or sets whether prospective appearance selection throws.</summary>
    public bool ThrowOnAppearanceSelection { get; set; }

    /// <summary>Gets or sets whether prospective structural comparison returns an invalid impact.</summary>
    public bool ReturnInvalidImpact { get; set; }

    /// <summary>Gets or sets whether prospective Theme impact calculation throws.</summary>
    public bool ThrowOnThemeImpact { get; set; }

    /// <summary>Gets or sets whether prospective resolved-style publication selection throws.</summary>
    public bool ThrowOnResolvedStyleSelection { get; set; }

    /// <summary>Gets or sets the prospective Theme whose appearance-profile resolution throws.</summary>
    public Theme? AppearanceProfileFailureTheme { get; set; }

    /// <summary>Gets or sets the complete profile supplied by the protected property override.</summary>
    public ThemeProfile? AppearanceProfileOverride { get; set; }

    /// <summary>Gets the number of completed attachment callbacks.</summary>
    public int AttachedCalls { get; private set; }

    /// <summary>Gets the number of completed detachment callbacks.</summary>
    public int DetachedCalls { get; private set; }

    /// <inheritdoc/>
    protected override ThemeProfile AppearanceProfile =>
        AppearanceProfileOverride ?? ResolveStyle(Style, Theme).Appearance;

    /// <inheritdoc/>
    protected override ThemeProfile GetAppearanceProfile(Theme? theme) =>
        ReferenceEquals(theme, Theme)
            ? AppearanceProfile
            : AppearanceProfileFailureTheme is not null &&
              ReferenceEquals(theme, AppearanceProfileFailureTheme)
            ? throw new InvalidOperationException("appearance-profile")
            : ResolveStyle(Style, theme).Appearance;

    /// <inheritdoc/>
    protected override InvalidationImpact GetThemeChangeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace)
    {
        if (ThrowOnThemeImpact)
        {
            throw new InvalidOperationException("theme-impact");
        }

        ThemeObservedDuringImpact = Theme;
        return GetControlStyleThemeImpact(
            Style,
            previous,
            current,
            ResolveStyle,
            CompareStructure,
            static style => style.Appearance,
            previousParentAmbientFace,
            currentParentAmbientFace);
    }

    /// <inheritdoc/>
    protected override string? GetThemeResolvedStylePropertyName(Theme? previous, Theme? current) =>
        ThrowOnResolvedStyleSelection
            ? throw new InvalidOperationException("resolved-style")
            : Style is null && ResolveStyle(Style, previous) != ResolveStyle(Style, current)
                ? nameof(ActualStyle)
                : null;

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        AttachedCalls++;
    }

    /// <inheritdoc/>
    protected override void OnDetached()
    {
        base.OnDetached();
        DetachedCalls++;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        MeasureCalls++;
        return default;
    }

    private static ButtonStyle ResolveStyle(ButtonStyle? localStyle, Theme? theme) =>
        localStyle ?? new ButtonStyle(ButtonStyle.Standard.Padding, (theme ?? Themes.Dark).Input);

    private static InvalidationImpact CompareStructure(
        ButtonStyle previous,
        Theme? previousTheme,
        ButtonStyle current,
        Theme? currentTheme) =>
        previous.Padding == current.Padding ? InvalidationImpact.None : InvalidationImpact.Measure;
}
