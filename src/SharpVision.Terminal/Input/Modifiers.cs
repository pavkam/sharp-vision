namespace SharpVision.Terminal.Input;

/// <summary>Identifies independently composable keyboard modifiers.</summary>
[Flags]
public enum Modifiers
{
    /// <summary>No modifier is active.</summary>
    None = 0,

    /// <summary>Shift is active.</summary>
    Shift = 1 << 0,

    /// <summary>Alt is active.</summary>
    Alt = 1 << 1,

    /// <summary>Control is active.</summary>
    Control = 1 << 2,

    /// <summary>Super is active.</summary>
    Super = 1 << 3,

    /// <summary>Hyper is active.</summary>
    Hyper = 1 << 4,

    /// <summary>Meta is active.</summary>
    Meta = 1 << 5,

    /// <summary>Caps Lock is active.</summary>
    CapsLock = 1 << 6,

    /// <summary>Num Lock is active.</summary>
    NumLock = 1 << 7,
}
