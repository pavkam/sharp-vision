namespace SharpVision.Terminal.Input;

/// <summary>Identifies a key or pointer transition.</summary>
public enum Action
{
    /// <summary>The input became active.</summary>
    Press,

    /// <summary>The active input repeated.</summary>
    Repeat,

    /// <summary>The input became inactive.</summary>
    Release,
}
