namespace SharpVision.Tests.Support;

using SharpVision.Controls;

/// <summary>Provides a concrete parent for shared control infrastructure tests.</summary>
internal sealed class ProbeContainer: Container
{
    /// <summary>Initializes a probe with an optional child capacity.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    internal ProbeContainer(int capacity = int.MaxValue) : base(capacity)
    {
    }

    /// <summary>Gets or sets whether rendering clips owned descendants.</summary>
    internal bool ClipChildren { get; set; } = true;

    /// <inheritdoc/>
    internal override bool ClipsChildren => ClipChildren;
}
