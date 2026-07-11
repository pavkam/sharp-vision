using SharpVision.Layout;

namespace SharpVision.Controls;

/// <summary>Stores one weakly attached Dock side.</summary>
internal sealed class DockPlacement
{
    /// <summary>Gets or sets the physical edge.</summary>
    internal Side Side { get; set; }
}
