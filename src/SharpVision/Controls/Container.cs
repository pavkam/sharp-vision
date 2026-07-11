using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;

namespace SharpVision.Controls;

/// <summary>Defines a mutable control that owns an ordered child collection.</summary>
public abstract class Container: Control
{
    /// <summary>Initializes an empty ordered child collection.</summary>
    protected Container() => Children = new Children(this);

    /// <summary>Gets the owned ordered children.</summary>
    public Children Children { get; }

    /// <inheritdoc/>
    public override Control? HitTest(Point point)
    {
        var hit = base.HitTest(point);

        if (hit is null)
        {
            return null;
        }

        for (var index = Children.Count - 1; index >= 0; index--)
        {
            if (Children[index].HitTest(point) is { } child)
            {
                return child;
            }
        }

        return this;
    }

    /// <inheritdoc/>
    internal override void VisitChildren(Action<Control> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        foreach (var child in Children)
        {
            visitor(child);
        }
    }

    /// <inheritdoc/>
    internal override void DisposeChildren()
    {
        while (Children.Count > 0)
        {
            Children[^1].Dispose();
        }
    }

    /// <inheritdoc/>
    internal override void RenderChildren(Canvas canvas)
    {
        foreach (var child in Children)
        {
            child.Render(canvas);
        }
    }
}
