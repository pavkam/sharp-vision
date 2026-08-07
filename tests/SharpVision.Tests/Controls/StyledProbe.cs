// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Exercises the protected complete-style resolution seam through a Button-shaped test style.</summary>
public sealed class StyledProbe: ControlBase
{
    private readonly StyleSlot<ButtonStyle> _style;

    /// <summary>Initializes one framework-owned primary style slot.</summary>
    public StyledProbe()
    {
        var definition = new StyleDefinition<ButtonStyle>(
            Resolve,
            SelectAppearance,
            Compare);
        _style = InitializeStyle(definition);
    }

    /// <summary>Gets or sets the complete local test style, or null for Theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ButtonStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, Theme-owned, or fallback style.</summary>
    public ButtonStyle ActualStyle => _style.Actual;

    /// <summary>Gets the protected complete appearance profile selected by the resolved style.</summary>
    public AppearanceStates Profile => AppearanceStates;

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
    public Theme? AppearanceStatesFailureTheme { get; set; }

    /// <summary>Gets or sets the complete profile supplied by the protected property override.</summary>
    public AppearanceStates? AppearanceStatesOverride { get; set; }

    /// <summary>Gets the number of completed attachment callbacks.</summary>
    public int AttachedCalls { get; private set; }

    /// <summary>Gets the number of completed detachment callbacks.</summary>
    public int DetachedCalls { get; private set; }

    /// <inheritdoc/>
    protected override AppearanceStates AppearanceStates =>
        AppearanceStatesOverride ?? SelectAppearance(ResolveStyle(Style, Theme), Theme);

    /// <inheritdoc/>
    protected override AppearanceStates GetAppearanceStates(Theme? theme) =>
        ReferenceEquals(theme, Theme)
            ? AppearanceStates
            : AppearanceStatesFailureTheme is not null &&
              ReferenceEquals(theme, AppearanceStatesFailureTheme)
            ? throw new InvalidOperationException("appearance-profile")
            : SelectAppearance(ResolveStyle(Style, theme), theme);

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
        ButtonStyle.Definition.Resolve(localStyle, theme);

    private ButtonStyle Resolve(ButtonStyle? localStyle, Theme? theme)
    {
        return ThrowOnStyleResolution && localStyle is not null
            ? throw new InvalidOperationException("resolve")
            : AppearanceStatesFailureTheme is not null && ReferenceEquals(theme, AppearanceStatesFailureTheme)
            ? throw new InvalidOperationException("appearance-profile")
            : ThrowOnResolvedStyleSelection && !ReferenceEquals(theme, Theme)
            ? throw new InvalidOperationException("resolved-style")
            : ResolveStyle(localStyle, theme);
    }

    private AppearanceStates SelectAppearance(ButtonStyle style, Theme? theme) => ThrowOnAppearanceSelection
        ? throw new InvalidOperationException("appearance")
        : ButtonStyle.Definition.Appearance!(style, theme);

    private InvalidationImpact Compare(
        ButtonStyle previous,
        Theme? previousTheme,
        ButtonStyle current,
        Theme? currentTheme)
    {
        if (!ReferenceEquals(previousTheme, currentTheme))
        {
            ThemeObservedDuringImpact = Theme;
            if (ThrowOnThemeImpact)
            {
                throw new InvalidOperationException("theme-impact");
            }
        }

        return ThrowOnCompareStructure
            ? throw new InvalidOperationException("compare")
            : ReturnInvalidImpact
            ? (InvalidationImpact) int.MaxValue
            : CompareStructure(previous, previousTheme, current, currentTheme);
    }

    private static InvalidationImpact CompareStructure(
        ButtonStyle previous,
        Theme? previousTheme,
        ButtonStyle current,
        Theme? currentTheme)
    {
        _ = previousTheme;
        _ = currentTheme;
        return previous.Padding == current.Padding ? InvalidationImpact.None : InvalidationImpact.Measure;
    }
}
