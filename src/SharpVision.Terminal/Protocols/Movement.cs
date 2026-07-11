namespace SharpVision.Terminal.Protocols;

/// <summary>Identifies a relative cursor movement direction.</summary>
public enum Movement
{
    /// <summary>Move toward the top of the display.</summary>
    Up,

    /// <summary>Move toward the bottom of the display.</summary>
    Down,

    /// <summary>Move toward increasing columns.</summary>
    Forward,

    /// <summary>Move toward decreasing columns.</summary>
    Back,
}
