// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.ComponentModel;



public abstract partial class Control
{
    private readonly Dictionary<IStyleProperty, object?> _localValues = [];
    private readonly Dictionary<(IStyleProperty Property, State State), object?> _resolvedPropertyCache = [];
    private TerminalStyle? _cachedResolvedStyle;
    private bool _cachedHasOpaqueFill;
    private State _cachedResolvedVisualState;
    private int _cachedThemeVersion = -1;
    private int _styleResolutionEpoch;

    /// <summary>Gets the internal theme snapshot published by the owning application.</summary>
    internal ThemeContext? ThemeContext { get; private set; }

    /// <summary>Gets the per-instance style overlay applied only to this control.</summary>
    internal IControlStyle? InstanceStyle { get; private set; }

    /// <summary>Removes one explicit local style-property override.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The registered style property.</param>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The property does not apply to the control's runtime type.
    /// </exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void ClearValue<T>(StyleProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        EnsureThemeProperty(property);
        VerifyMutable();

        if (!_localValues.Remove(property))
        {
            return;
        }

        InvalidateThemeProperty(property);
    }

    /// <summary>Reads one effective style-property value through the theme cascade.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The registered style property.</param>
    /// <returns>The resolved value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The property does not apply to the control's runtime type.
    /// </exception>
    public T GetValue<T>(StyleProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return ResolveProperty(property, GetVisualState());
    }

    internal T ResolveProperty<T>(StyleProperty<T> property, State visualState)
    {
        EnsureThemeProperty(property);
        (IStyleProperty Property, State State) key = (Property: property, State: visualState);

        if (_resolvedPropertyCache.TryGetValue(key, out var cached))
        {
            return (T) cached!;
        }

        var value = ThemeResolver.Resolve(this, property, visualState);
        _resolvedPropertyCache[key] = value;
        return value;
    }

    /// <summary>Records one explicit local style-property override.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The registered style property.</param>
    /// <param name="value">The validated local value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The property does not apply to the control's runtime type or the value is invalid.
    /// </exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void SetValue<T>(StyleProperty<T> property, T value)
    {
        ArgumentNullException.ThrowIfNull(property);
        EnsureThemeProperty(property);
        property.ValidateValue(value);
        VerifyMutable();

        if (_localValues.TryGetValue(property, out var stored) &&
            EqualityComparer<T>.Default.Equals((T) stored!, value))
        {
            return;
        }

        _localValues[property] = value;
        InvalidateThemeProperty(property);
    }

    internal bool TryGetLocalValue<T>(StyleProperty<T> property, out T value)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (_localValues.TryGetValue(property, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    internal void SetThemeContext(ThemeContext? context)
    {
        if (ReferenceEquals(ThemeContext, context))
        {
            return;
        }

        ThemeContext = context;
        InvalidateResolvedStyleCache();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>Applies one theme context to this control and its complete subtree.</summary>
    /// <param name="context">The published theme context, or null to inherit none.</param>
    internal void PropagateThemeContext(ThemeContext? context)
    {
        SetThemeContext(context);
        VisitChildren(child => child.PropagateThemeContext(context));
    }

    internal void SetInstanceStyle(IControlStyle? style) => InstanceStyle = style;

    /// <summary>Gets the composed terminal style for an explicit visual state.</summary>
    /// <param name="visualState">The active visual-state flags.</param>
    /// <returns>The resolved terminal style.</returns>
    protected internal TerminalStyle GetResolvedStyle(State visualState) =>
        GetResolvedAppearance(visualState).Style;

    internal ResolvedAppearance GetResolvedAppearance(State visualState)
    {
        var themeVersion = ThemeContext?.Version ?? -1;

        if (_cachedResolvedStyle is { } cached &&
            _cachedResolvedVisualState == visualState &&
            _cachedThemeVersion == themeVersion &&
            _styleResolutionEpoch == _cachedStyleResolutionEpoch)
        {
            return new ResolvedAppearance
            {
                Style = cached,
                HasOpaqueFill = _cachedHasOpaqueFill,
            };
        }

        var resolved = ControlAppearance.Resolve(this, visualState);
        _cachedResolvedStyle = resolved.Style;
        _cachedHasOpaqueFill = resolved.HasOpaqueFill;
        _cachedResolvedVisualState = visualState;
        _cachedThemeVersion = themeVersion;
        _cachedStyleResolutionEpoch = _styleResolutionEpoch;
        return resolved;
    }

    internal void InvalidateResolvedStyleCache()
    {
        _styleResolutionEpoch++;
        _resolvedPropertyCache.Clear();
    }

    /// <summary>Clears the resolved-style caches of this control and every descendant.</summary>
    /// <remarks>Used on structural moves where ancestor style scopes may have changed.</remarks>
    internal void InvalidateSubtreeResolvedStyleCache()
    {
        InvalidateResolvedStyleCache();
        VisitChildren(child => child.InvalidateSubtreeResolvedStyleCache());
    }

    private void CascadeStyleScopeInvalidation(Invalidation invalidation)
    {
        if (this is not IStyleScope)
        {
            return;
        }

        VisitChildren(child => child.InvalidateInheritedStyle(invalidation));
    }

    private void InvalidateInheritedStyle(Invalidation invalidation)
    {
        InvalidateResolvedStyleCache();
        Invalidate(invalidation);
        VisitChildren(child => child.InvalidateInheritedStyle(invalidation));
    }

    private int _cachedStyleResolutionEpoch = -1;

    private void EnsureThemeProperty<T>(StyleProperty<T> property)
    {
        if (!property.AppliesTo(GetType()))
        {
            throw new ArgumentException(
                $"The property '{property.Name}' does not apply to {GetType().Name}.",
                nameof(property));
        }
    }

    private void InvalidateThemeProperty<T>(StyleProperty<T> property)
    {
        Debug.Assert(property is not null, "Theme invalidation requires a non-null property.");

        InvalidateResolvedStyleCache();
        Invalidate(InvalidationFor(property.Impact));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property.ClrName));
    }
}
