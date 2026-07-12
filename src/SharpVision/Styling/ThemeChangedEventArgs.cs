namespace SharpVision.Styling;

/// <summary>Describes one committed theme or control-style change.</summary>
public sealed class ThemeChangedEventArgs: EventArgs
{
    /// <summary>Initializes one validated style-resource change.</summary>
    /// <param name="targetType">The control type affected by the change.</param>
    /// <param name="impact">The earliest affected control phase.</param>
    /// <exception cref="ArgumentNullException"><paramref name="targetType"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="impact"/> is unknown.</exception>
    public ThemeChangedEventArgs(Type targetType, Impact impact)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        if (!Enum.IsDefined(impact))
        {
            throw new ArgumentOutOfRangeException(nameof(impact), impact, "The style impact is unknown.");
        }

        TargetType = targetType;
        Impact = impact;
    }

    /// <summary>Gets the control type whose style changed.</summary>
    public Type TargetType { get; }

    /// <summary>Gets the earliest affected control phase.</summary>
    public Impact Impact { get; }
}
