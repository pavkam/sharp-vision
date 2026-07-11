namespace SharpVision.Terminal.Protocols;

/// <summary>Identifies xterm mouse coordinate encodings.</summary>
public enum MouseCoordinates
{
    /// <summary>Use the original three-byte X10 coordinate encoding.</summary>
    Default = 0,

    /// <summary>Use UTF-8 encoded X10 fields through mode 1005.</summary>
    Utf8 = 1005,

    /// <summary>Use SGR cell coordinates through mode 1006.</summary>
    Sgr = 1006,

    /// <summary>Use urxvt decimal coordinates through mode 1015.</summary>
    Urxvt = 1015,

    /// <summary>Use SGR pixel coordinates through mode 1016.</summary>
    Pixel = 1016,
}
