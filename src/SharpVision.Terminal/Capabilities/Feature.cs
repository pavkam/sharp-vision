namespace SharpVision.Terminal.Capabilities;

/// <summary>Represents one optional feature and the origin of its evidence.</summary>
public readonly record struct Feature
{
    /// <summary>Initializes validated feature evidence.</summary>
    /// <param name="state">The support confidence.</param>
    /// <param name="origin">The evidence origin.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="state"/> or <paramref name="origin"/> is unknown.
    /// </exception>
    public Feature(Support state, Origin origin)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The support state is unknown.");
        }

        if (!Enum.IsDefined(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), origin, "The evidence origin is unknown.");
        }

        State = state;
        Origin = origin;
    }

    /// <summary>Gets a conservative unknown feature.</summary>
    public static Feature Unknown { get; } = new(Support.Unknown, Origin.Default);

    /// <summary>Gets the support confidence.</summary>
    public Support State { get; }

    /// <summary>Gets the evidence origin.</summary>
    public Origin Origin { get; }

    /// <summary>Gets whether safe behavior may actively use the feature.</summary>
    public bool IsSupported => State == Support.Supported;

    /// <summary>Deconstructs the feature evidence.</summary>
    /// <param name="state">Receives the support confidence.</param>
    /// <param name="origin">Receives the evidence origin.</param>
    public void Deconstruct(out Support state, out Origin origin)
    {
        state = State;
        origin = Origin;
    }
}
