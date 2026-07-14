// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

        Control? hit = base.HitTest(point);

        if (hit is null)
        {
            return null;
        }

        for (int index = Children.Count - 1; index >= 0; index--)
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

        foreach (Control child in Children)
        {
            visitor(child);
        }
    }

    /// <inheritdoc/>
    internal override Control? HitTestPopup(Point point)
    {
        for (int index = Children.Count - 1; index >= 0; index--)
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
        foreach (Control child in Children)
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
        foreach (Control child in Children)
        {
            child.RenderPopupLayer(canvas);
        }
    }

    #region Grow and shrink

    /// <summary>Gets or sets whether this container sizes its border box to its content, overriding stretch and star sizing.</summary>
    /// <remarks>Honors <see cref="Control.MinWidth"/>/<see cref="Control.MaxWidth"/> and the height equivalents. See <see cref="AutoSizeMode"/>.</remarks>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public bool AutoSize
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Measure);
    }

    /// <summary>Gets or sets whether an auto-sizing axis may shrink below its explicit fixed-cell size.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public AutoSizeMode AutoSizeMode
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The auto-size mode is unknown.");
            }

            _ = Set(ref field, value, Invalidation.Measure);
        }
    } = AutoSizeMode.GrowAndShrink;

    /// <inheritdoc/>
    internal override bool ShrinkWrapsWidth => AutoSize;

    /// <inheritdoc/>
    internal override bool ShrinkWrapsHeight => AutoSize;

    /// <inheritdoc/>
    internal override Size OnMeasuredDesired(Size desired)
    {
        if (!AutoSize || AutoSizeMode != AutoSizeMode.GrowOnly)
        {
            return desired;
        }

        // GrowOnly never shrinks below an explicit fixed-cell size on that axis.
        int width = Width.Kind == Kind.Cells ? Math.Max(desired.Width, (int) Width.Value) : desired.Width;
        int height = Height.Kind == Kind.Cells ? Math.Max(desired.Height, (int) Height.Value) : desired.Height;
        return new Size(width, height);
    }

    #endregion
}
