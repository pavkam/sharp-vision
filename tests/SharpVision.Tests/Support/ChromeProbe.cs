using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

namespace SharpVision.Tests.Support;

/// <summary>Exposes protected content bounds for chrome layout tests.</summary>
internal sealed class ChromeProbe: Control
{
    /// <summary>Gets the arranged content rectangle after border and padding deflation.</summary>
    internal Rect ExposedContentBounds => ContentBounds;

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint) => default;
}
