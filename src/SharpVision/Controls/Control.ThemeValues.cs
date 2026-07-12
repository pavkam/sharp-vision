using System.ComponentModel;

using SharpVision.Styling;

using TerminalStyle = SharpVision.Terminal.Rendering.Style;

namespace SharpVision.Controls;

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
    protected T GetValue<T>(StyleProperty<T> property) =>
        ResolveProperty(property, GetVisualState());

    internal T ResolveProperty<T>(StyleProperty<T> property, State visualState)
    {
        EnsureThemeProperty(property);
        var key = (Property: (IStyleProperty) property, State: visualState);

        if (_resolvedPropertyCache.TryGetValue(key, out var cached) && cached is T typed)
        {
            return typed;
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
    protected void SetValue<T>(StyleProperty<T> property, T value)
    {
        ArgumentNullException.ThrowIfNull(property);
        EnsureThemeProperty(property);
        property.ValidateValue(value);
        VerifyMutable();
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
        ThemeContext = context;
        InvalidateResolvedStyleCache();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    internal void SetInstanceStyle(IControlStyle? style) => InstanceStyle = style;

    internal TerminalStyle GetResolvedStyle(State visualState) =>
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
        InvalidateResolvedStyleCache();
        Invalidate(property.Impact == Impact.Measure ? Invalidation.Measure : Invalidation.Render);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(GetThemeClrPropertyName(property)));
    }

    private static string GetThemeClrPropertyName<T>(StyleProperty<T> property) =>
        property.Name switch
        {
            "margin" => nameof(Margin),
            "padding" => nameof(Padding),
            "foreground" => nameof(Foreground),
            "background" => nameof(Background),
            "attributes" => nameof(Attributes),
            "underline" => nameof(Underline),
            "underline-color" => nameof(UnderlineColor),
            "fill-mode" => nameof(FillMode),
            "border-thickness" => nameof(BorderThickness),
            "border-style" => nameof(BorderStyle),
            "border-color" => nameof(BorderColor),
            "border-attributes" => nameof(BorderAttributes),
            "has-shadow" => nameof(HasShadow),
            "shadow-mode" => nameof(ShadowMode),
            "shadow-offset" => nameof(ShadowOffset),
            "shadow-glyph" => nameof(ShadowGlyph),
            "shadow-foreground" => nameof(ShadowForeground),
            "shadow-background" => nameof(ShadowBackground),
            "shadow-attributes" => nameof(ShadowAttributes),
            _ => property.Name,
        };
}
