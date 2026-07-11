namespace SharpVision.Terminal.Rendering;

/// <summary>Identifies filled quarter-cell areas in a Block Elements glyph.</summary>
[Flags]
public enum Quadrants
{
    /// <summary>No quadrant is filled.</summary>
    None = 0,

    /// <summary>The upper-left quarter is filled.</summary>
    UpperLeft = 1,

    /// <summary>The upper-right quarter is filled.</summary>
    UpperRight = 2,

    /// <summary>The lower-left quarter is filled.</summary>
    LowerLeft = 4,

    /// <summary>The lower-right quarter is filled.</summary>
    LowerRight = 8,

    /// <summary>Both upper quarters are filled.</summary>
    Upper = UpperLeft | UpperRight,

    /// <summary>Both lower quarters are filled.</summary>
    Lower = LowerLeft | LowerRight,

    /// <summary>Every quadrant is filled.</summary>
    All = Upper | Lower,
}
