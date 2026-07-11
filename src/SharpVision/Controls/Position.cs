using SharpVision.Layout;

namespace SharpVision.Controls;

/// <summary>Stores weakly attached optional Canvas offsets.</summary>
internal sealed class Position
{
    /// <summary>Gets or sets the leading horizontal offset.</summary>
    internal Length? Left { get; set; }

    /// <summary>Gets or sets the leading vertical offset.</summary>
    internal Length? Top { get; set; }

    /// <summary>Gets or sets the trailing horizontal offset.</summary>
    internal Length? Right { get; set; }

    /// <summary>Gets or sets the trailing vertical offset.</summary>
    internal Length? Bottom { get; set; }
}
