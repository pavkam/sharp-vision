namespace SharpVision.Layout;

/// <summary>Identifies how one layout axis resolves its requested extent.</summary>
public enum Kind
{
    /// <summary>Uses the content's intrinsic desired extent.</summary>
    Auto,

    /// <summary>Uses an exact integer terminal-cell extent.</summary>
    Cells,

    /// <summary>Uses a percentage of the final containing content extent.</summary>
    Percent,

    /// <summary>Uses a weighted share of remaining bounded space.</summary>
    Star,
}
