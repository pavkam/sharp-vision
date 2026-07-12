namespace SharpVision.Styling;

/// <summary>Defines the mutable style contract used by controls and themes.</summary>
public interface IControlStyle
{
    /// <summary>Raised after one committed style mutation publishes a new snapshot.</summary>
    public event EventHandler<ThemeChangedEventArgs>? Changed;

    /// <summary>Gets the concrete control type targeted by this style.</summary>
    public Type TargetType { get; }

    /// <summary>Gets whether this style rejects further mutation.</summary>
    public bool IsFrozen { get; }

    /// <summary>Gets the earliest impact of the current style contents.</summary>
    public Impact AggregateImpact { get; }

    /// <summary>Creates an independent unfrozen copy of this style.</summary>
    /// <returns>A mutable style with the same values.</returns>
    internal IControlStyle CloneForTheme();

    /// <summary>Creates a frozen copy of this style.</summary>
    /// <returns>A frozen style with the same values.</returns>
    internal IControlStyle FreezeForTheme();

    /// <summary>Gets one stored value from the current immutable snapshot.</summary>
    /// <param name="property">The style property.</param>
    /// <param name="state">The visual state.</param>
    /// <param name="value">The stored value when present.</param>
    /// <returns>Whether a value exists.</returns>
    internal bool TryGetSnapshotValue(IStyleProperty property, State state, out object? value);
}
