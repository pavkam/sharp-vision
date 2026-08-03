// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Styling;

/// <summary>Owns one nullable local style and its cached complete resolved value.</summary>
/// <typeparam name="TStyle">The small immutable complete style value.</typeparam>
[PublicAPI]
public sealed class StyleSlot<TStyle>
    where TStyle : struct, IEquatable<TStyle>
{
    private readonly Action<TStyle, TStyle>? _changed;
    private readonly List<StyleSlot<TStyle>> _targets = [];
    private TStyle? _cache;
    private TStyle? _cacheKey;
    private Theme? _cacheTheme;

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
            if (_cache is { } cached && _cacheKey.Equals(LocalValue) && ReferenceEquals(_cacheTheme, theme))
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

        target.Source = this;
        _targets.Add(target);
        target.Owner.CommitStyle(target, LocalValue, fromBinding: true);
    }

    /// <summary>Forwards one committed nullable local value to every bound target.</summary>
    internal void ForwardLocal()
    {
        foreach (var target in _targets)
        {
            target.Owner.CommitStyle(target, LocalValue, fromBinding: true);
        }
    }

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

    internal ThemeRole Role => Definition.Role ?? ThemeRole.Control;

    internal ThemeProfile GetAppearance(Theme? theme) =>
        (Definition.Appearance ?? throw new InvalidOperationException("A secondary style cannot own appearance."))(
            Definition.Resolve(LocalValue, theme));

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

    internal string? GetThemeResolvedProperty(Theme? previous, Theme? current) =>
        LocalValue is null && !Definition.Resolve(null, previous).Equals(Definition.Resolve(null, current))
            ? ActualPropertyName
            : null;

    internal void ClearResolvedCache() => ClearCache();
}
