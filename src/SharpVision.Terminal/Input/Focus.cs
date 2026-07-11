namespace SharpVision.Terminal.Input;

/// <summary>Represents a terminal focus transition.</summary>
public readonly record struct Focus
{
    /// <summary>Initializes a terminal focus transition.</summary>
    /// <param name="gained">Whether terminal focus was gained rather than lost.</param>
    public Focus(bool gained) => Gained = gained;

    /// <summary>Gets whether terminal focus was gained rather than lost.</summary>
    public bool Gained { get; }

    /// <summary>Deconstructs the focus transition.</summary>
    /// <param name="gained">Receives whether terminal focus was gained.</param>
    public void Deconstruct(out bool gained) => gained = Gained;
}
