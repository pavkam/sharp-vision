namespace SharpVision.Styling;

using SharpVision.Controls;

/// <summary>Owns one style per control type and publishes immutable style-chain snapshots.</summary>
public sealed class Theme
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Type, IControlStyle> _styles = [];
    private readonly Dictionary<Type, IReadOnlyList<IControlStyle>> _styleChains = [];
    private readonly List<(IControlStyle Style, EventHandler<ThemeChangedEventArgs> Handler)> _subscriptions = [];

    /// <summary>Raised after one committed theme mutation publishes a new version.</summary>
    public event EventHandler<ThemeChangedEventArgs>? Changed;

    /// <summary>Gets whether this theme rejects further mutation.</summary>
    public bool IsFrozen { get; private set; }

    /// <summary>Gets the monotonically increasing published version.</summary>
    public int Version { get; private set; }

    /// <summary>Adds or replaces the style for one control type.</summary>
    /// <typeparam name="TControl">The targeted control type.</typeparam>
    /// <param name="style">The non-null style instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="style"/> targets a different control type.
    /// </exception>
    /// <exception cref="InvalidOperationException">The theme is frozen.</exception>
    public void SetStyle<TControl>(ControlStyle<TControl> style)
        where TControl : Control
    {
        ArgumentNullException.ThrowIfNull(style);

        if (style.TargetType != typeof(TControl))
        {
            throw new ArgumentException(
                "The supplied style targets a different control type.",
                nameof(style));
        }

        EnsureMutable();
        Impact impact;

        lock (_gate)
        {
            if (_styles.TryGetValue(typeof(TControl), out var existing))
            {
                Unsubscribe(existing);
            }

            _styles[typeof(TControl)] = style;
            Subscribe(style);
            InvalidateCaches();
            impact = style.AggregateImpact;
            Version++;
        }

        RaiseChanged(impact, typeof(TControl));
    }

    /// <summary>Removes the style for one control type when present.</summary>
    /// <typeparam name="TControl">The targeted control type.</typeparam>
    /// <returns>Whether a style was removed.</returns>
    /// <exception cref="InvalidOperationException">The theme is frozen.</exception>
    public bool RemoveStyle<TControl>()
        where TControl : Control
    {
        EnsureMutable();

        lock (_gate)
        {
            if (!_styles.Remove(typeof(TControl), out var existing))
            {
                return false;
            }

            Unsubscribe(existing);
            InvalidateCaches();
            Version++;
        }

        RaiseChanged(Impact.Measure, typeof(TControl));
        return true;
    }

    /// <summary>Gets the style for one control type when defined.</summary>
    /// <typeparam name="TControl">The targeted control type.</typeparam>
    /// <returns>The style instance or null.</returns>
    public ControlStyle<TControl>? GetStyle<TControl>()
        where TControl : Control
    {
        lock (_gate)
        {
            return _styles.TryGetValue(typeof(TControl), out var style) ? (ControlStyle<TControl>) style : null;
        }
    }

    /// <summary>Gets whether a style exists for one control type.</summary>
    /// <typeparam name="TControl">The targeted control type.</typeparam>
    /// <returns>Whether a style is defined.</returns>
    public bool HasStyle<TControl>()
        where TControl : Control => HasStyle(typeof(TControl));

    /// <summary>Gets whether a style exists for one control type.</summary>
    /// <param name="controlType">The concrete control type.</param>
    /// <returns>Whether a style is defined.</returns>
    public bool HasStyle(Type controlType)
    {
        ArgumentNullException.ThrowIfNull(controlType);

        lock (_gate)
        {
            return _styles.ContainsKey(controlType);
        }
    }

    /// <summary>Creates an independent unfrozen copy containing cloned styles.</summary>
    /// <returns>A mutable theme copy.</returns>
    public Theme Clone()
    {
        lock (_gate)
        {
            var clone = new Theme();

            foreach (var entry in _styles)
            {
                var cloned = CloneStyle(entry.Value);
                clone._styles[entry.Key] = cloned;
                clone.Subscribe(cloned);
            }

            clone.Version = Version;
            return clone;
        }
    }

    /// <summary>Freezes this theme and every referenced style snapshot.</summary>
    /// <exception cref="InvalidOperationException">The theme is already frozen.</exception>
    public void Freeze()
    {
        EnsureMutable();

        lock (_gate)
        {
            foreach (var entry in _styles.ToArray())
            {
                Unsubscribe(entry.Value);
                _styles[entry.Key] = FreezeStyle(entry.Value);
            }

            IsFrozen = true;
            InvalidateCaches();
            Version++;
        }
    }

    internal ThemeSnapshot CreateSnapshot()
    {
        lock (_gate)
        {
            return new ThemeSnapshot(Version, new Dictionary<Type, IControlStyle>(_styles));
        }
    }

    internal IReadOnlyList<IControlStyle> GetStyleChain(Type controlType)
    {
        lock (_gate)
        {
            if (_styleChains.TryGetValue(controlType, out var cached))
            {
                return cached;
            }

            var chain = BuildChain(controlType);
            _styleChains[controlType] = chain;
            return chain;
        }
    }

    private List<IControlStyle> BuildChain(Type controlType)
    {
        var chain = new List<IControlStyle>();

        foreach (var type in ControlHierarchy.BaseToDerived(controlType))
        {
            if (_styles.TryGetValue(type, out var style))
            {
                chain.Add(style);
            }
        }

        return chain;
    }

    private void EnsureMutable()
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("A frozen theme cannot be changed.");
        }
    }

    private void Subscribe(IControlStyle style)
    {
        void handler(object? _, ThemeChangedEventArgs args)
        {
            lock (_gate)
            {
                InvalidateCaches();
                Version++;
            }

            RaiseChanged(args.Impact, args.TargetType);
        }

        style.Changed += handler;
        _subscriptions.Add((style, handler));
    }

    private void Unsubscribe(IControlStyle style)
    {
        for (var index = _subscriptions.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(_subscriptions[index].Style, style))
            {
                style.Changed -= _subscriptions[index].Handler;
                _subscriptions.RemoveAt(index);
            }
        }
    }

    private void InvalidateCaches() => _styleChains.Clear();

    private void RaiseChanged(Impact impact, Type targetType) =>
        Changed?.Invoke(this, new ThemeChangedEventArgs(targetType, impact));

    private static IControlStyle CloneStyle(IControlStyle style) => ((IStyleLifecycle) style).CloneForTheme();

    private static IControlStyle FreezeStyle(IControlStyle style) => ((IStyleLifecycle) style).FreezeForTheme();
}
