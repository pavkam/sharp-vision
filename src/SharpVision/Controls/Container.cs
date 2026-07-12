using SharpVision.Terminal.Geometry;

using TerminalCanvas = SharpVision.Terminal.Rendering.Canvas;

namespace SharpVision.Controls;

/// <summary>Defines a mutable control that owns an ordered child collection.</summary>
public abstract class Container: Control
{
    /// <summary>Initializes an empty ordered child collection.</summary>
    protected Container() : this(int.MaxValue)
    {
    }

    /// <summary>Initializes an empty ordered child collection with a finite capacity.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    protected Container(int capacity) => Children = new Children(this, capacity);

    /// <summary>Gets the owned ordered children.</summary>
    public Children Children { get; }

    /// <summary>Gets the number of children participating in default navigation.</summary>
    internal virtual int NavigationCount => Children.Count;

    /// <summary>Gets one child in default navigation order.</summary>
    /// <param name="index">The zero-based navigation index.</param>
    /// <returns>The child at the requested navigation position.</returns>
    internal virtual Control NavigationAt(int index) => Children[index];

    /// <inheritdoc/>
    public override Control? HitTest(Point point)
    {
        if (HitTestPopup(point) is { } popup)
        {
            return popup;
        }

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
    internal override Control? HitTestPopup(Point point)
    {
        for (var index = Children.Count - 1; index >= 0; index--)
        {
            if (Children[index].HitTestPopup(point) is { } popup)
            {
                return popup;
            }
        }

        return null;
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
    internal override void RenderChildren(TerminalCanvas canvas)
    {
        foreach (var child in Children)
        {
            child.Render(canvas);
        }

        if (Parent is null)
        {
            RenderOwnedPopupLayer(canvas);
        }
    }

    /// <inheritdoc/>
    internal override void RenderPopupLayer(TerminalCanvas canvas)
        => RenderOwnedPopupLayer(canvas);

    private void RenderOwnedPopupLayer(TerminalCanvas canvas)
    {
        foreach (var child in Children)
        {
            child.RenderPopupLayer(canvas);
        }
    }
}
