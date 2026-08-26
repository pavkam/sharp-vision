// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

using Styling;

/// <summary>Owns one nullable local style and its cached complete resolved value.</summary>
/// <typeparam name="TStyle">The immutable complete style value.</typeparam>
[PublicAPI]
public sealed class StyleSlot<TStyle>
    where TStyle : ControlStyle
{
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> _comparableFields = new();
    private readonly Action<TStyle, TStyle>? _changed;
    private readonly List<StyleSlot<TStyle>> _targets = [];
    private TStyle? _cache;
    private TStyle? _cacheKey;
    private Theme? _cacheTheme;
    private long _commitVersion;

    /// <summary>Initializes one framework-owned slot.</summary>
    internal StyleSlot(
        ControlBase owner,
        StyleDefinition<TStyle> definition,
        string propertyName,
        string actualPropertyName,
        bool ownsAppearance,
        Action<TStyle, TStyle>? changed)
    {
        Owner = owner;
        Definition = definition;
        PropertyName = propertyName;
        ActualPropertyName = actualPropertyName;
        OwnsAppearance = ownsAppearance;
        _changed = changed;
    }

    /// <summary>Gets or sets the complete local style, or null to return ownership to the Theme.</summary>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public TStyle? Local
    {
        get => LocalValue;
        set => Owner.CommitStyle(this, value);
    }

    /// <summary>Gets the cached complete local, Theme-owned, or fallback style.</summary>
    public TStyle Actual
    {
        get
        {
            var theme = Owner.Theme;
            if (_cache is { } cached &&
                EqualityComparer<TStyle?>.Default.Equals(_cacheKey, LocalValue) &&
                ReferenceEquals(_cacheTheme, theme))
            {
                return cached;
            }

            var resolved = Definition.Resolve(LocalValue, theme);
            _cache = resolved;
            _cacheKey = LocalValue;
            _cacheTheme = theme;
            return resolved;
        }
    }

    internal TStyle? LocalValue { get; set; }

    /// <summary>Gets the optional slot that exclusively supplies this slot's local value.</summary>
    internal StyleSlot<TStyle>? Source { get; private set; }

    internal StyleDefinition<TStyle> Definition { get; }

    internal void ClearCache()
    {
        _cache = null;
        _cacheKey = null;
        _cacheTheme = null;
    }

    internal void PublishChanged(TStyle previous, TStyle current) => _changed?.Invoke(previous, current);

    /// <summary>Publishes one Theme-owned resolved transition after the new Theme commits.</summary>
    internal void PublishThemeChanged(Theme? previous, Theme? current)
    {
        if (_changed is null)
        {
            return;
        }

        _changed(Definition.Resolve(LocalValue, previous), Definition.Resolve(LocalValue, current));
    }

    /// <summary>Adds one validated downstream slot and forwards the nullable local value.</summary>
    internal void Bind(StyleSlot<TStyle> target)
    {
        if (_targets.Contains(target))
        {
            throw new InvalidOperationException("The style slots are already bound.");
        }

        if (target.Source is not null)
        {
            throw new InvalidOperationException("A style slot can have only one upstream owner.");
        }

        for (var source = this; source is not null; source = source.Source)
        {
            if (ReferenceEquals(source, target))
            {
                throw new InvalidOperationException("Style-slot bindings cannot form a cycle.");
            }
        }

        var previousVersion = target._commitVersion;
        target.Source = this;
        _targets.Add(target);

        try
        {
            target.Owner.CommitStyle(target, LocalValue, fromBinding: true);
        }
        catch
        {
            if (target._commitVersion == previousVersion)
            {
                _ = _targets.Remove(target);
                target.Source = null;
            }

            throw;
        }
    }

    /// <summary>Collects this slot and its transitive downstream graph in publication order.</summary>
    internal List<StyleSlot<TStyle>> GetPropagationGraph()
    {
        var graph = new List<StyleSlot<TStyle>> { this };

        for (var index = 0; index < graph.Count; index++)
        {
            graph.AddRange(graph[index]._targets);
        }

        return graph;
    }

    /// <summary>Advances and returns the version that supersedes older publication.</summary>
    internal long AdvanceCommitVersion() => ++_commitVersion;

    /// <summary>Gets whether one staged publication still describes this slot.</summary>
    internal bool IsCurrentVersion(long version) => _commitVersion == version;

    /// <summary>Releases every graph edge owned by this slot during control disposal.</summary>
    internal void DisposeBindings()
    {
        if (Source is { } source)
        {
            _ = source._targets.Remove(this);
            Source = null;
        }

        foreach (var target in _targets)
        {
            target.Source = null;
        }

        _targets.Clear();
    }

    internal string PropertyName { get; }

    internal string ActualPropertyName { get; }

    internal ControlBase Owner { get; }

    internal bool OwnsAppearance { get; }

    internal AppearanceStates GetAppearance(Theme? theme) =>
        GetAppearance(Definition.Resolve(LocalValue, theme), LocalValue is not null, theme);

    /// <summary>Resolves appearance for one prospective local value.</summary>
    internal AppearanceStates GetAppearance(TStyle style, bool hasLocalValue, Theme? theme) =>
        hasLocalValue
            ? (Definition.LocalAppearance ??
               throw new InvalidOperationException("A secondary style cannot own appearance."))(style, theme)
            : (Definition.Appearance ?? throw new InvalidOperationException("A secondary style cannot own appearance."))(
                style,
                theme);

    /// <summary>Releases the upstream edge when its source owner is no longer an ancestor.</summary>
    internal void ReleaseInvalidBinding()
    {
        if (Source is not { } source)
        {
            return;
        }

        for (var current = Owner.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, source.Owner))
            {
                return;
            }
        }

        _ = source._targets.Remove(this);
        Source = null;
    }

    internal InvalidationImpact GetThemeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace) =>
        Owner.GetStyleThemeImpact(
            this,
            previous,
            current,
            previousParentAmbientFace,
            currentParentAmbientFace);

    // A raw TStyle.Equals(...) compares symbolic ControlColor references (e.g. SemanticColor.
    // ControlText), which stay identical across two Theme instances that map the same JSON key to
    // the same semantic color role even when their underlying palettes resolve that role to
    // different literal colors - exactly the common case when only a Theme's colors change.
    // ResolvedValuesEqual walks the same reflective shape Theme.Overlay already uses, resolving
    // every ControlColor member against its OWN Theme before comparing, instead of comparing raw
    // symbolic values. A per-type Compare function is NOT a
    // substitute here - Compare answers "does this warrant re-measuring/re-arranging" (e.g.
    // ButtonStyle.Compare only inspects Padding and shadow-translation parity, never Face/Border
    // colors), a narrower question than "did the resolved value change at all".
    internal string? GetThemeResolvedProperty(Theme? previous, Theme? current)
    {
        if (LocalValue is not null)
        {
            return null;
        }

        var resolvedPrevious = Definition.Resolve(null, previous);
        var resolvedCurrent = Definition.Resolve(null, current);
        return !ResolvedValuesEqual(resolvedPrevious, previous, resolvedCurrent, current)
            ? ActualPropertyName
            : null;
    }

    /// <summary>Returns render impact when any semantic paint member resolves differently.</summary>
    internal static InvalidationImpact GetSemanticValueImpact(
        TStyle previous,
        Theme? previousTheme,
        TStyle current,
        Theme? currentTheme) =>
        ResolvedSemanticValuesEqual(previous, previousTheme, current, currentTheme, isStyleRoot: true)
            ? InvalidationImpact.None
            : InvalidationImpact.Render;

    [Pure]
    private static bool ResolvedSemanticValuesEqual(
        object? previous,
        Theme? previousTheme,
        object? current,
        Theme? currentTheme,
        bool isStyleRoot)
    {
        if (previous is null || current is null || previous.GetType() != current.GetType())
        {
            return true;
        }

        if (previous is ControlColor previousColor && current is ControlColor currentColor)
        {
            return previousColor.Resolve(previousTheme) == currentColor.Resolve(currentTheme);
        }

        if (previous is ControlDecoration previousDecoration && current is ControlDecoration currentDecoration)
        {
            return previousDecoration.Resolve(previousTheme) == currentDecoration.Resolve(currentTheme);
        }

        var fields = GetComparableFields(previous.GetType());
        if (fields.Length == 0)
        {
            return true;
        }

        foreach (var field in fields)
        {
            if (isStyleRoot && field.DeclaringType == typeof(ControlStyle))
            {
                continue;
            }

            if (!ResolvedSemanticValuesEqual(
                    field.GetValue(previous),
                    previousTheme,
                    field.GetValue(current),
                    currentTheme,
                    isStyleRoot: false))
            {
                return false;
            }
        }

        return true;
    }

    [Pure]
    private static bool ResolvedValuesEqual(object? previous, Theme? previousTheme, object? current, Theme? currentTheme)
    {
        if (previous is null || current is null)
        {
            return ReferenceEquals(previous, current);
        }

        var type = previous.GetType();
        if (type != current.GetType())
        {
            return false;
        }

        if (previous is ControlColor previousColor && current is ControlColor currentColor)
        {
            return previousColor.Resolve(previousTheme) == currentColor.Resolve(currentTheme);
        }

        // ControlDecoration has the same semantic/literal split, and every bundled theme authors
        // Face/Border/Shadow attributes symbolically. Two themes referring to the same
        // SemanticDecoration therefore compared equal however far apart their attributes tables
        // were - default-light and default-dark differ on focusedText alone this way - so nothing
        // bound to ActualStyle ever refreshed across that swap.
        if (previous is ControlDecoration previousDecoration && current is ControlDecoration currentDecoration)
        {
            return previousDecoration.Resolve(previousTheme) == currentDecoration.Resolve(currentTheme);
        }

        // ImmutableArray, arrays, and third-party ordered collection members may all represent
        // identical resolved presentation through independently allocated storage. Compare their
        // elements through this same semantic-aware walk instead of relying on container identity.
        if (previous is not string &&
            previous is IEnumerable previousItems &&
            current is IEnumerable currentItems)
        {
            return ResolvedSequencesEqual(previousItems, previousTheme, currentItems, currentTheme);
        }

        var fields = GetComparableFields(type);
        if (fields.Length == 0)
        {
            return Equals(previous, current);
        }

        foreach (var field in fields)
        {
            if (!ResolvedValuesEqual(field.GetValue(previous), previousTheme, field.GetValue(current), currentTheme))
            {
                return false;
            }
        }

        return true;
    }

    private static FieldInfo[] GetComparableFields(Type type) =>
        _comparableFields.GetOrAdd(
            type,
            static candidate =>
            [
                .. candidate
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(static property => property.GetIndexParameters().Length == 0)
                    .Select(static property => property.DeclaringType?.GetField(
                        $"<{property.Name}>k__BackingField",
                        BindingFlags.NonPublic | BindingFlags.Instance))
                    .OfType<FieldInfo>()
            ]);

    [Pure]
    private static bool ResolvedSequencesEqual(
        IEnumerable previous,
        Theme? previousTheme,
        IEnumerable current,
        Theme? currentTheme)
    {
        var previousEnumerator = previous.GetEnumerator();
        var currentEnumerator = current.GetEnumerator();

        try
        {
            while (true)
            {
                var previousHasValue = previousEnumerator.MoveNext();
                var currentHasValue = currentEnumerator.MoveNext();
                if (previousHasValue != currentHasValue)
                {
                    return false;
                }

                if (!previousHasValue)
                {
                    return true;
                }

                if (!ResolvedValuesEqual(
                        previousEnumerator.Current,
                        previousTheme,
                        currentEnumerator.Current,
                        currentTheme))
                {
                    return false;
                }
            }
        }
        finally
        {
            (previousEnumerator as IDisposable)?.Dispose();
            (currentEnumerator as IDisposable)?.Dispose();
        }
    }

    internal void ClearResolvedCache() => ClearCache();
}
