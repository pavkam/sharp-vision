using System.Text;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;

namespace SharpVision.Tests.Support;

/// <summary>Provides a recording leaf for shared control infrastructure tests.</summary>
internal sealed class ProbeControl(Size intrinsic = default): Control
{
    /// <summary>Gets constraints received by the content measure extension point.</summary>
    internal List<Constraint> MeasureConstraints { get; } = [];

    /// <summary>Gets rectangles received by the content arrange extension point.</summary>
    internal List<Rect> ArrangeBounds { get; } = [];

    /// <summary>Gets or sets work invoked from inside the next measure pass.</summary>
    internal Action<ProbeControl>? Measuring { get; set; }

    /// <summary>Gets or sets work invoked from inside the next arrange pass.</summary>
    internal Action<ProbeControl>? Arranging { get; set; }

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        MeasureConstraints.Add(constraint);
        Measuring?.Invoke(this);
        return intrinsic;
    }

    /// <inheritdoc/>
    protected override void ArrangeCore(Rect bounds)
    {
        ArrangeBounds.Add(bounds);
        Arranging?.Invoke(this);
    }

    /// <summary>Draws one Rune using this control's resolved terminal style.</summary>
    internal void Draw(Canvas canvas, Rune value)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        _ = canvas.Draw(buffer[..length], new Point(Bounds.X, Bounds.Y), ResolvedStyle);
    }
}

/// <summary>Provides a concrete parent for shared control infrastructure tests.</summary>
internal sealed class ProbeContainer: Container;
