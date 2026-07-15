// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Buffers;


/// <summary>Arranges owned children sequentially on one terminal-cell axis.</summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Stack is the approved concise terminal control name, not a collection type.")]
public sealed class Stack: Container
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
    protected override Size MeasureOverride(Constraint constraint)
    {
        int axis = 0;
        int cross = 0;
        int count = 0;

        foreach (Control child in Children)
        {
            child.Measure(Orientation == Orientation.Vertical
                ? new Constraint(constraint.Width, height: null)
                : new Constraint(width: null, constraint.Height));

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            int desiredAxis = Orientation == Orientation.Vertical
                ? Add(child.DesiredSize.Height, child.Margin.Vertical)
                : Add(child.DesiredSize.Width, child.Margin.Horizontal);
            int desiredCross = Orientation == Orientation.Vertical
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
    protected override void ArrangeOverride(Rect bounds)
    {
        int count = CountParticipants();

        if (count == 0)
        {
            return;
        }

        Control[] rentedChildren = ArrayPool<Control>.Shared.Rent(count);
        Length[] rentedLengths = ArrayPool<Length>.Shared.Rent(count);
        int[] rentedAutomatic = ArrayPool<int>.Shared.Rent(count);
        int[] rentedMinimum = ArrayPool<int>.Shared.Rent(count);
        int[] rentedMaximum = ArrayPool<int>.Shared.Rent(count);
        int[] rentedExtents = ArrayPool<int>.Shared.Rent(count);
        Span<Control> children = rentedChildren.AsSpan(0, count);
        Span<Length> lengths = rentedLengths.AsSpan(0, count);
        Span<int> automatic = rentedAutomatic.AsSpan(0, count);
        Span<int> minimum = rentedMinimum.AsSpan(0, count);
        Span<int> maximum = rentedMaximum.AsSpan(0, count);
        Span<int> extents = rentedExtents.AsSpan(0, count);

        try
        {
            Fill(children, lengths, automatic, minimum, maximum);
            int axis = Orientation == Orientation.Vertical ? bounds.Height : bounds.Width;
            int spacing = SpacingExtent(count, axis);
            int margins = SumMargins(children);
            int available = Math.Max(0, axis - spacing - margins);

            // Percentages use the complete final content axis. Converting the
            // resolved request to cells lets margins reserve their own space
            // without changing that percentage base or star remainder.
            for (int index = 0; index < count; index++)
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
    internal override void RenderContent(TerminalCanvas canvas)
    {
        if (!Reverse)
        {
            base.RenderContent(canvas);
            return;
        }

        for (int index = Children.Count - 1; index >= 0; index--)
        {
            Children[index].Render(canvas);
        }
    }

    // The running arrange-time origin (left) legitimately goes negative once
    // this stack is scrolled content inside an armed AutoScroll container:
    // ResolveContentSlot shifts the content slot by the current offset, so a
    // stack flush against its parent's origin arranges at a negative Y/X once
    // scrolled past zero. The increment (right) — an extent, margin, or
    // spacing value — remains non-negative in every caller.
    private static int Add(int left, int right)
    {
        Debug.Assert(right >= 0, "Stack accumulation uses a non-negative increment.");

        long value = (long) left + right;
        return value >= int.MaxValue ? int.MaxValue : (int) value;
    }

    private static int Percent(int axis, double value)
    {
        Debug.Assert(axis >= 0, "Percentage base axis is non-negative.");

        double result = Math.Round(axis * value / 100, MidpointRounding.AwayFromZero);
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private int CountParticipants()
    {
        int count = 0;

        foreach (Control child in Children)
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
        int position = 0;

        for (int offset = 0; offset < Children.Count; offset++)
        {
            int index = Reverse ? Children.Count - offset - 1 : offset;
            Control child = Children[index];

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
        Debug.Assert(children.Length >= 0, "Stack margin sum requires a valid span.");

        int result = 0;

        foreach (Control child in children)
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
        Debug.Assert(children.Length == extents.Length, "Every arranged child must have one extent.");
        Debug.Assert(spacing >= 0, "Stack spacing is non-negative.");

        int origin = Orientation == Orientation.Vertical ? bounds.Y : bounds.X;
        int remainingSpacing = spacing;

        for (int index = 0; index < children.Length; index++)
        {
            Control child = children[index];
            int margin = Orientation == Orientation.Vertical
                ? child.Margin.Vertical
                : child.Margin.Horizontal;
            int outer = Add(extents[index], margin);
            Rect slot = Orientation == Orientation.Vertical
                ? new Rect(bounds.X, origin, bounds.Width, outer)
                : new Rect(origin, bounds.Y, outer, bounds.Height);

            child.Arrange(
                slot,
                widthResolved: Orientation == Orientation.Horizontal,
                heightResolved: Orientation == Orientation.Vertical);
            origin = Add(origin, outer);

            if (index < children.Length - 1)
            {
                int gap = Math.Min(Spacing, remainingSpacing);
                origin = Add(origin, gap);
                remainingSpacing -= gap;
            }
        }
    }

    private int SpacingExtent(int count, int limit)
    {
        Debug.Assert(count >= 0, "Participant count is non-negative.");
        Debug.Assert(limit >= 0, "Spacing limit is non-negative.");

        if (count <= 1)
        {
            return 0;
        }

        long requested = (long) Spacing * (count - 1);
        return (int) Math.Min(limit, Math.Min(int.MaxValue, requested));
    }
}
