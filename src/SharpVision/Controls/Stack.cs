namespace SharpVision.Controls;

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

using TerminalCanvas = Terminal.Rendering.Canvas;

/// <summary>Arranges owned children sequentially on one terminal-cell axis.</summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Stack is the approved concise terminal control name, not a collection type.")]
public class Stack: Container
{
    /// <summary>Initializes a stack that fills its parent cross-axis slot.</summary>
    public Stack() => HorizontalAlignment = HorizontalAlignment.Stretch;

    /// <summary>Gets or sets the sequential layout axis.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Orientation Orientation
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The orientation is unknown.");
            }

            _ = Set(ref field, value, Invalidation.Measure);
        }
    } = Orientation.Vertical;

    /// <summary>Gets or sets non-negative cells between non-collapsed children.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int Spacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = Set(ref field, value, Invalidation.Measure);
        }
    }

    /// <summary>Gets or sets whether visual and default navigation order is reversed.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool Reverse
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Arrange);
    }

    /// <inheritdoc/>
    internal override Control NavigationAt(int index) =>
        Reverse ? Children[Children.Count - index - 1] : Children[index];

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        var axis = 0;
        var cross = 0;
        var count = 0;

        foreach (var child in Children)
        {
            child.Measure(Orientation == Orientation.Vertical
                ? new Constraint(constraint.Width, height: null)
                : new Constraint(width: null, constraint.Height));

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var desiredAxis = Orientation == Orientation.Vertical
                ? Add(child.DesiredSize.Height, child.Margin.Vertical)
                : Add(child.DesiredSize.Width, child.Margin.Horizontal);
            var desiredCross = Orientation == Orientation.Vertical
                ? Add(child.DesiredSize.Width, child.Margin.Horizontal)
                : Add(child.DesiredSize.Height, child.Margin.Vertical);
            axis = Add(axis, desiredAxis);
            cross = Math.Max(cross, desiredCross);
            count++;
        }

        axis = Add(axis, SpacingExtent(count, int.MaxValue));
        return Orientation == Orientation.Vertical
            ? new Size(cross, axis)
            : new Size(axis, cross);
    }

    /// <inheritdoc/>
    protected override void ArrangeCore(Rect bounds)
    {
        var count = CountParticipants();

        if (count == 0)
        {
            return;
        }

        var rentedChildren = ArrayPool<Control>.Shared.Rent(count);
        var rentedLengths = ArrayPool<Length>.Shared.Rent(count);
        var rentedAutomatic = ArrayPool<int>.Shared.Rent(count);
        var rentedMinimum = ArrayPool<int>.Shared.Rent(count);
        var rentedMaximum = ArrayPool<int>.Shared.Rent(count);
        var rentedExtents = ArrayPool<int>.Shared.Rent(count);
        var children = rentedChildren.AsSpan(0, count);
        var lengths = rentedLengths.AsSpan(0, count);
        var automatic = rentedAutomatic.AsSpan(0, count);
        var minimum = rentedMinimum.AsSpan(0, count);
        var maximum = rentedMaximum.AsSpan(0, count);
        var extents = rentedExtents.AsSpan(0, count);

        try
        {
            Fill(children, lengths, automatic, minimum, maximum);
            var axis = Orientation == Orientation.Vertical ? bounds.Height : bounds.Width;
            var spacing = SpacingExtent(count, axis);
            var margins = SumMargins(children);
            var available = Math.Max(0, axis - spacing - margins);

            // Percentages use the complete final content axis. Converting the
            // resolved request to cells lets margins reserve their own space
            // without changing that percentage base or star remainder.
            for (var index = 0; index < count; index++)
            {
                if (lengths[index].Kind == Kind.Percent)
                {
                    lengths[index] = Length.Cells(Percent(axis, lengths[index].Value));
                }
            }

            Tracks.Resolve(available, lengths, automatic, minimum, maximum, extents);
            Arrange(children, extents, bounds, spacing);
        }
        finally
        {
            children.Clear();
            lengths.Clear();
            automatic.Clear();
            minimum.Clear();
            maximum.Clear();
            extents.Clear();
            ArrayPool<Control>.Shared.Return(rentedChildren, clearArray: true);
            ArrayPool<Length>.Shared.Return(rentedLengths);
            ArrayPool<int>.Shared.Return(rentedAutomatic);
            ArrayPool<int>.Shared.Return(rentedMinimum);
            ArrayPool<int>.Shared.Return(rentedMaximum);
            ArrayPool<int>.Shared.Return(rentedExtents);
        }
    }

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas)
    {
        if (!Reverse)
        {
            base.RenderChildren(canvas);
            return;
        }

        for (var index = Children.Count - 1; index >= 0; index--)
        {
            Children[index].Render(canvas);
        }

        if (Parent is null)
        {
            RenderPopupLayer(canvas);
        }
    }

    private static int Add(int left, int right)
    {
        var value = (long) left + right;
        return value >= int.MaxValue ? int.MaxValue : (int) value;
    }

    private static int Percent(int axis, double value)
    {
        var result = Math.Round(axis * value / 100, MidpointRounding.AwayFromZero);
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private int CountParticipants()
    {
        var count = 0;

        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Collapsed)
            {
                count++;
            }
        }

        return count;
    }

    private void Fill(
        Span<Control> children,
        Span<Length> lengths,
        Span<int> automatic,
        Span<int> minimum,
        Span<int> maximum)
    {
        var position = 0;

        for (var offset = 0; offset < Children.Count; offset++)
        {
            var index = Reverse ? Children.Count - offset - 1 : offset;
            var child = Children[index];

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            children[position] = child;
            lengths[position] = Orientation == Orientation.Vertical ? child.Height : child.Width;
            automatic[position] = Orientation == Orientation.Vertical
                ? child.DesiredSize.Height
                : child.DesiredSize.Width;
            minimum[position] = Orientation == Orientation.Vertical ? child.MinHeight : child.MinWidth;
            maximum[position] = Orientation == Orientation.Vertical ? child.MaxHeight : child.MaxWidth;
            position++;
        }

        Debug.Assert(position == children.Length, "Every participating child must have one track.");
    }

    private int SumMargins(ReadOnlySpan<Control> children)
    {
        var result = 0;

        foreach (var child in children)
        {
            result = Add(
                result,
                Orientation == Orientation.Vertical
                    ? child.Margin.Vertical
                    : child.Margin.Horizontal);
        }

        return result;
    }

    private void Arrange(
        ReadOnlySpan<Control> children,
        ReadOnlySpan<int> extents,
        Rect bounds,
        int spacing)
    {
        var origin = Orientation == Orientation.Vertical ? bounds.Y : bounds.X;
        var remainingSpacing = spacing;

        for (var index = 0; index < children.Length; index++)
        {
            var child = children[index];
            var margin = Orientation == Orientation.Vertical
                ? child.Margin.Vertical
                : child.Margin.Horizontal;
            var outer = Add(extents[index], margin);
            var slot = Orientation == Orientation.Vertical
                ? new Rect(bounds.X, origin, bounds.Width, outer)
                : new Rect(origin, bounds.Y, outer, bounds.Height);
            child.Arrange(
                slot,
                widthResolved: Orientation == Orientation.Horizontal,
                heightResolved: Orientation == Orientation.Vertical);
            origin = Add(origin, outer);

            if (index < children.Length - 1)
            {
                var gap = Math.Min(Spacing, remainingSpacing);
                origin = Add(origin, gap);
                remainingSpacing -= gap;
            }
        }
    }

    private int SpacingExtent(int count, int limit)
    {
        if (count <= 1)
        {
            return 0;
        }

        var requested = (long) Spacing * (count - 1);
        return (int) Math.Min(limit, Math.Min(int.MaxValue, requested));
    }
}
