// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Styling;

/// <summary>Creates immutable primary and secondary complete-style definitions. A leaf control
/// style declares no theme section of its own: it resolves entirely from a code-owned default,
/// one declared one-hop fallback to inherit another style type's theme customization, and a
/// locally assigned <c>Style</c>. Only the six well-known base types (<see cref="ControlStyle"/>
/// and its five siblings) resolve against a <c>styles.*</c> theme section, and they do so through
/// <see cref="Theme.GetStyleSet{TStyle}(TStyle)"/> directly rather than through this
/// class.</summary>
[PublicAPI]
public static class StyleDefinitions
{
    /// <summary>Creates a primary control-style definition with one declared one-hop fallback -
    /// used by every leaf control style. Every state borrows the fallback type's own resolved
    /// per-state contribution, recombined via <paramref name="complete"/>; a leaf authors no theme
    /// section of its own; its only sources of appearance are its code-owned default, the
    /// fallback's resolved states, and a locally assigned <c>Style</c>.</summary>
    /// <typeparam name="TStyle">The immutable complete style value.</typeparam>
    /// <typeparam name="TFallback">The declared fallback style type (typically one of the six
    /// well-known base types).</typeparam>
    /// <param name="fallbackTo">Resolves the fallback type's complete per-state set for one Theme.</param>
    /// <param name="complete">Completes one fallback-contributed style into this control's own style. The Theme argument is how a completion consults theme-level values beyond the fallback's own resolved appearance - e.g. the glyph-aware styles read <c>theme.Glyphs</c> to complete their own structural members.</param>
    /// <param name="compare">Returns the earliest phase affected by structural or directly themed members.</param>
    /// <param name="applyBorderlessFocusFallback">Whether to force the terminal's reverse-video
    /// attribute onto Focused/FocusWithin as a last-resort safety net when this style's own
    /// borderless geometry would otherwise leave them colorwise identical to Normal. See
    /// <see cref="Theme.GetInteractiveControlStyleSet"/>'s remarks for the full rationale; opt in
    /// only for a chromeless leaf with no other visible focus cue.</param>
    /// <returns>An immutable primary-style definition.</returns>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static StyleDefinition<TStyle> Control<TStyle, TFallback>(
        Func<Theme, StyleStates<TFallback>> fallbackTo,
        Func<TFallback, VisualState, Theme, TStyle> complete,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare,
        bool applyBorderlessFocusFallback = false)
        where TStyle : ControlStyle
        where TFallback : ControlStyle
        => CreateControl(fallbackTo, complete, compare, preserveCompleteLocalAppearance: false, applyBorderlessFocusFallback: applyBorderlessFocusFallback);

    /// <summary>Creates a primary control-style definition whose completion may add code-owned
    /// state defaults for Theme-owned styles while a complete local style remains authoritative
    /// in every state.</summary>
    internal static StyleDefinition<TStyle> ControlWithThemeOwnedStateDefaults<TStyle, TFallback>(
        Func<Theme, StyleStates<TFallback>> fallbackTo,
        Func<TFallback, VisualState, Theme, TStyle> complete,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare)
        where TStyle : ControlStyle
        where TFallback : ControlStyle
        => CreateControl(fallbackTo, complete, compare, preserveCompleteLocalAppearance: true);

    /// <summary>Creates a primary control-style definition for a leaf rendered on a continuous Bar
    /// plane. Physical hover omits only <c>Face.Background</c>; disabled restores Bar. Both retain
    /// every other authored Face, Border, and Shadow contribution.</summary>
    /// <typeparam name="TStyle">The immutable complete style value.</typeparam>
    /// <typeparam name="TFallback">The declared fallback style type.</typeparam>
    /// <param name="fallbackTo">Resolves the fallback type's complete per-state set for one Theme.</param>
    /// <param name="complete">Completes one fallback-contributed style into this control's style.</param>
    /// <param name="compare">Returns the earliest phase affected by resolved members.</param>
    /// <param name="applyBorderlessFocusFallback">Whether to force the terminal's reverse-video
    /// attribute onto Focused/FocusWithin as a last-resort safety net when this style's own
    /// borderless geometry would otherwise leave them colorwise identical to Normal.</param>
    /// <returns>An immutable primary-style definition for a Bar surface.</returns>
    internal static StyleDefinition<TStyle> BarControlWithThemeOwnedStateDefaults<TStyle, TFallback>(
        Func<Theme, StyleStates<TFallback>> fallbackTo,
        Func<TFallback, VisualState, Theme, TStyle> complete,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare,
        bool applyBorderlessFocusFallback = false)
        where TStyle : ControlStyle
        where TFallback : ControlStyle =>
        CreateControl(
            fallbackTo,
            complete,
            compare,
            preserveCompleteLocalAppearance: true,
            applyBorderlessFocusFallback: applyBorderlessFocusFallback,
            transformAppearance: BarAppearance.Rebase);

    private static StyleDefinition<TStyle> CreateControl<TStyle, TFallback>(
        Func<Theme, StyleStates<TFallback>> fallbackTo,
        Func<TFallback, VisualState, Theme, TStyle> complete,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare,
        bool preserveCompleteLocalAppearance,
        bool applyBorderlessFocusFallback = false,
        Func<AppearanceStates, AppearanceStates>? transformAppearance = null)
        where TStyle : ControlStyle
        where TFallback : ControlStyle
    {
        ArgumentNullException.ThrowIfNull(fallbackTo);
        ArgumentNullException.ThrowIfNull(complete);
        ArgumentNullException.ThrowIfNull(compare);
        StyleStates<TFallback> ResolveFallback(Theme theme) =>
            RequireResult(fallbackTo(theme), nameof(fallbackTo));
        TStyle Complete(TFallback fallback, VisualState state, Theme theme) =>
            RequireResult(complete(fallback, state, theme), nameof(complete));

        return new StyleDefinition<TStyle>(
            (local, theme) => local ?? ResolveNormal(theme ?? ThemeCatalog.Dark, ResolveFallback, Complete),
            (style, theme) => TransformAppearance(
                (theme ?? ThemeCatalog.Dark).BuildFallbackAwareStates(
                    style,
                    ResolveFallback,
                    Complete,
                    applyBorderlessFocusFallback)),
            (previous, previousTheme, current, currentTheme) => MaximumImpact(
                compare(previous, previousTheme, current, currentTheme),
                GetInheritedImpact(previous, previousTheme, current, currentTheme)),
            localAppearance: preserveCompleteLocalAppearance
                ? static (style, _) => new AppearanceStates(
                    new ControlAppearance(style.Face, style.Border, style.Shadow))
                : (style, theme) =>
                    TransformAppearance(
                        (theme ?? ThemeCatalog.Dark).BuildCodeOwnedStates(
                            style,
                            ResolveFallback(Theme.Unthemed).Normal,
                            Complete,
                            applyBorderlessFocusFallback)));

        AppearanceStates TransformAppearance(AppearanceStates appearance) =>
            transformAppearance?.Invoke(appearance) ?? appearance;
    }

    // Used to also overlay this key's own "normal" JSON on top of the completed fallback. A leaf
    // no longer has any theme section of its own to overlay, so this is now exactly the fallback's
    // own resolved Normal completed into this style's shape - identical to what
    // BuildFallbackAwareStates itself recomputes for Normal internally.
    private static TStyle ResolveNormal<TStyle, TFallback>(
        Theme theme,
        Func<Theme, StyleStates<TFallback>> fallbackTo,
        Func<TFallback, VisualState, Theme, TStyle> complete)
        where TStyle : ControlStyle
        where TFallback : ControlStyle =>
        complete(fallbackTo(theme).Normal, VisualState.Normal, theme);

    private static InvalidationImpact GetInheritedImpact<TStyle>(
        TStyle previous,
        Theme? previousTheme,
        TStyle current,
        Theme? currentTheme)
        where TStyle : ControlStyle =>
        previous is WindowStyle previousWindow && current is WindowStyle currentWindow
            ? WindowStyle.GetInteractionChromeImpact(previousWindow, previousTheme, currentWindow, currentTheme)
            : InvalidationImpact.None;

    private static InvalidationImpact MaximumImpact(InvalidationImpact left, InvalidationImpact right) =>
        (int) left >= (int) right ? left : right;

    /// <summary>Creates a secondary style definition that does not own its control's appearance states.</summary>
    /// <typeparam name="TStyle">The immutable complete style value.</typeparam>
    /// <param name="fallback">Resolves the complete fallback for one inherited Theme.</param>
    /// <param name="compare">Returns the earliest phase affected by resolved members.</param>
    /// <returns>An immutable secondary-style definition.</returns>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static StyleDefinition<TStyle> Part<TStyle>(
        Func<Theme?, TStyle> fallback,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare)
        where TStyle : ControlStyle
    {
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(compare);
        return new StyleDefinition<TStyle>(
            (local, theme) => local ?? RequireResult(fallback(theme), nameof(fallback)),
            null,
            compare,
            StyleDefinitionKind.Part);
    }

    /// <summary>Creates a primary-named aggregate style definition that projects complete values
    /// onto heterogeneous retained parts without owning the aggregate control's appearance.</summary>
    /// <typeparam name="TStyle">The immutable complete aggregate style value.</typeparam>
    /// <param name="fallback">Resolves the complete fallback for one inherited Theme.</param>
    /// <param name="compare">Returns the earliest phase affected by resolved members.</param>
    /// <returns>An immutable aggregate-style definition.</returns>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static StyleDefinition<TStyle> Aggregate<TStyle>(
        Func<Theme?, TStyle> fallback,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare)
        where TStyle : ControlStyle
    {
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(compare);
        return new StyleDefinition<TStyle>(
            (local, theme) => local ?? RequireResult(fallback(theme), nameof(fallback)),
            null,
            compare,
            StyleDefinitionKind.Aggregate);
    }

    private static TResult RequireResult<TResult>(TResult? result, string callbackName)
        where TResult : class =>
        result ?? throw new InvalidOperationException($"The {callbackName} callback returned null.");
}
