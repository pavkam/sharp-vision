namespace SharpVision.Input;

/// <summary>Identifies the direction of one routed-event pass.</summary>
public enum Phase
{
    /// <summary>Travels from the root toward the original target.</summary>
    Preview,

    /// <summary>Travels from the original target toward the root.</summary>
    Bubble,
}
