// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Defines one typed, immutable non-appearance Theme value consumed by controls.</summary>
/// <remarks>
/// A dependency instance must be stable across its owner's lifetime, because
/// <see cref="ControlBase.ResolveThemeValue{T}(ThemeValueDependency{T})"/> and
/// <see cref="ControlBase.SetThemeValueDependency(IThemeValueDependency, bool)"/> track it by
/// reference to detect Theme changes it is affected by. A resolver that only reads the Theme
/// argument, with no captured instance state, belongs in a <see langword="static readonly"/> field
/// shared by every instance of the owning type - most built-in controls do this. A resolver that
/// also reads captured instance state (for example a chart's authored series colors) needs its own
/// dependency per owning instance instead, held in a per-instance field, the way
/// <see cref="Charts.ChartControlBase"/> does.
/// </remarks>
/// <typeparam name="T">The immutable resolved value type.</typeparam>
public sealed class ThemeValueDependency<T>: IThemeValueDependency
{
    private readonly Func<Theme, T> _resolver;
    private readonly IEqualityComparer<T> _comparer;
    private readonly InvalidationImpact _impact;

    /// <summary>Initializes a dependency with its pure resolver, impact, and equality policy.</summary>
    /// <param name="resolver">Resolves the immutable value from one non-null Theme.</param>
    /// <param name="impact">The earliest phase affected when resolved values differ.</param>
    /// <param name="comparer">The optional resolved-value equality policy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resolver"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="impact"/> is none or undefined.</exception>
    public ThemeValueDependency(
        Func<Theme, T> resolver,
        InvalidationImpact impact,
        IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        if (impact is not (InvalidationImpact.Render or InvalidationImpact.Arrange or InvalidationImpact.Measure))
        {
            throw new ArgumentOutOfRangeException(nameof(impact), impact, "A Theme value dependency must affect a UI phase.");
        }

        _resolver = resolver;
        _impact = impact;
        _comparer = comparer ?? EqualityComparer<T>.Default;
    }

    /// <summary>Resolves the registered value against one Theme or the library fallback.</summary>
    /// <param name="theme">The Theme to resolve, or null for the library fallback.</param>
    /// <returns>The immutable resolved value.</returns>
    internal T Resolve(Theme? theme) => _resolver(theme ?? ThemeCatalog.Dark);

    /// <inheritdoc/>
    public InvalidationImpact GetImpact(Theme? previous, Theme? current) =>
        _comparer.Equals(Resolve(previous), Resolve(current))
            ? InvalidationImpact.None
            : _impact;
}
