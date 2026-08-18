// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Styling;

/// <summary>Creates immutable primary and secondary complete-style definitions. A style
/// declares its own
/// <c>styles.*</c> key, resolved either as a self-contained root (the six well-known base types)
/// or through one declared fallback to inherit another style type's theme customization.</summary>
[PublicAPI]
public static class StyleDefinitions
{
    /// <summary>Creates a primary, self-contained root style definition with no cross-type
    /// fallback, for a style type that owns a code-defined default and inherits no other type's
    /// theme customization.</summary>
    /// <remarks>
    /// Prefer <see cref="Control{TStyle,TFallback}(Func{Theme,StyleStates{TFallback}},Func{TFallback,VisualState,TStyle},Func{TStyle,Theme?,TStyle,Theme?,InvalidationImpact})"/>
    /// unless the type genuinely has no sensible fallback: a root resolves entirely from its own
    /// key, so a theme that authors only <c>styles.control</c> moves nothing about it.
    ///
    /// <para>This overload takes <paramref name="codeOwnedDefault"/> eagerly. A type that declares
    /// its <c>Definition</c> above its <c>Default</c> therefore passes null - static initializers
    /// run in textual order - and the null check below throws from inside the static constructor,
    /// surfacing as a <see cref="TypeInitializationException"/> naming neither member. Declare
    /// <c>Default</c> first, or use a fallback definition, which takes a lambda instead.</para>
    /// </remarks>
    /// <typeparam name="TStyle">The immutable complete style value. Its name determines the
    /// <c>styles.*</c> key this root resolves - see <see cref="StyleKey"/>.</typeparam>
    /// <param name="codeOwnedDefault">The code-owned default this type falls back to when a theme
    /// authors neither the key itself nor its "normal" state.</param>
    /// <param name="compare">Returns the earliest phase affected by structural or directly themed members.</param>
    /// <returns>An immutable primary-style definition.</returns>
    /// <exception cref="ArgumentNullException">A delegate or <paramref name="codeOwnedDefault"/> is null.</exception>
    public static StyleDefinition<TStyle> Control<TStyle>(
        TStyle codeOwnedDefault,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare)
        where TStyle : ControlStyle =>
        Control(StyleKey.Of<TStyle>(), codeOwnedDefault, compare);

    /// <summary>Creates a self-contained root style definition under an explicitly named section,
    /// for a style type whose key cannot be derived from its name - in practice a third-party
    /// control, whose sections must be <c>vendor.control</c> namespaced to be admitted at all (a
    /// dot cannot appear in a type name). Library styles should use the derived overload so the key
    /// and the type stay in lockstep.</summary>
    /// <typeparam name="TStyle">The immutable complete style value.</typeparam>
    /// <param name="key">The exact <c>styles.*</c> key this root resolves (e.g. "acme.gauge").</param>
    /// <param name="codeOwnedDefault">The code-owned default this type falls back to when a theme
    /// authors neither the key itself nor its "normal" state.</param>
    /// <param name="compare">Returns the earliest phase affected by structural or directly themed members.</param>
    /// <returns>An immutable primary-style definition.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">A delegate or <paramref name="codeOwnedDefault"/> is null.</exception>
    public static StyleDefinition<TStyle> Control<TStyle>(
        string key,
        TStyle codeOwnedDefault,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare)
        where TStyle : ControlStyle
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(codeOwnedDefault);
        ArgumentNullException.ThrowIfNull(compare);
        return new StyleDefinition<TStyle>(
            (local, theme) => local ?? (theme ?? ThemeCatalog.Dark).GetStyleSet(key, codeOwnedDefault).Normal,
            (style, theme) => (theme ?? ThemeCatalog.Dark).BuildRootStates(style, key, codeOwnedDefault),
            compare);
    }

    /// <summary>Creates a primary control-style definition with one declared one-hop fallback -
    /// used by every leaf control style. A state this control's own key authors patches onto its
    /// own resolved Normal; an unauthored state instead borrows the fallback's contribution for
    /// that state, recombined via <paramref name="complete"/> using the recursive
    /// per-state fallback chain.</summary>
    /// <typeparam name="TStyle">The immutable complete style value. Its name determines the
    /// <c>styles.*</c> key this control resolves - see <see cref="StyleKey"/>.</typeparam>
    /// <typeparam name="TFallback">The declared fallback style type (typically one of the six
    /// well-known base types).</typeparam>
    /// <param name="fallbackTo">Resolves the fallback type's complete per-state set for one Theme.</param>
    /// <param name="complete">Completes one fallback-contributed style into this control's own style.</param>
    /// <param name="compare">Returns the earliest phase affected by structural or directly themed members.</param>
    /// <returns>An immutable primary-style definition.</returns>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static StyleDefinition<TStyle> Control<TStyle, TFallback>(
        Func<Theme, StyleStates<TFallback>> fallbackTo,
        Func<TFallback, VisualState, TStyle> complete,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare)
        where TStyle : ControlStyle
        where TFallback : ControlStyle =>
        Control(StyleKey.Of<TStyle>(), fallbackTo, complete, compare);

    /// <summary>Creates a one-hop fallback style definition under an explicitly named section, for
    /// a style type whose key cannot be derived from its name - see the derived overload's
    /// remarks.</summary>
    /// <typeparam name="TStyle">The immutable complete style value.</typeparam>
    /// <typeparam name="TFallback">The declared fallback style type.</typeparam>
    /// <param name="key">The exact <c>styles.*</c> key this control resolves (e.g. "acme.gauge").</param>
    /// <param name="fallbackTo">Resolves the fallback type's complete per-state set for one Theme.</param>
    /// <param name="complete">Completes one fallback-contributed style into this control's own style.</param>
    /// <param name="compare">Returns the earliest phase affected by structural or directly themed members.</param>
    /// <returns>An immutable primary-style definition.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static StyleDefinition<TStyle> Control<TStyle, TFallback>(
        string key,
        Func<Theme, StyleStates<TFallback>> fallbackTo,
        Func<TFallback, VisualState, TStyle> complete,
        Func<TStyle, Theme?, TStyle, Theme?, InvalidationImpact> compare)
        where TStyle : ControlStyle
        where TFallback : ControlStyle
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(fallbackTo);
        ArgumentNullException.ThrowIfNull(complete);
        ArgumentNullException.ThrowIfNull(compare);
        return new StyleDefinition<TStyle>(
            (local, theme) => local ?? ResolveNormal(theme ?? ThemeCatalog.Dark, key, fallbackTo, complete),
            (style, theme) => (theme ?? ThemeCatalog.Dark).BuildFallbackAwareStates(style, key, fallbackTo, complete),
            compare);
    }

    // The completed structural default (e.g. a control's own hardcoded Padding) still needs this
    // key's own "normal" JSON section applied on top, exactly like every other state does in
    // BuildFallbackAwareStates - otherwise a theme could restyle every other state but never the
    // resting Normal appearance or any of TStyle's own non-appearance members.
    private static TStyle ResolveNormal<TStyle, TFallback>(
        Theme theme,
        string key,
        Func<Theme, StyleStates<TFallback>> fallbackTo,
        Func<TFallback, VisualState, TStyle> complete)
        where TStyle : ControlStyle
        where TFallback : ControlStyle
    {
        var completed = complete(fallbackTo(theme).Normal, VisualState.Normal);
        var raw = theme.GetRawStyleSection(key);
        return raw?.TryGetValue("normal", out var overrides) == true
            ? (TStyle) theme.Overlay(completed, overrides, $"styles.{key}.normal")
            : completed;
    }

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
        return new StyleDefinition<TStyle>((local, theme) => local ?? fallback(theme), null, compare);
    }
}
