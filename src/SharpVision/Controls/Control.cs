using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Threading;

namespace SharpVision.Controls;

/// <summary>
/// Defines a traditional mutable UI element with dispatcher affinity and box layout.
/// </summary>
/// <remarks>
/// Detached controls may be assembled on any thread. Once attached, every
/// mutation and disposal must run on <see cref="Dispatcher"/>.
/// </remarks>
public abstract class Control: INotifyPropertyChanged, IDisposable
{
    /// <summary>Raised after one public property has committed a changed value.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the owning parent, or null for a detached/root control.</summary>
    public Container? Parent { get; private set; }

    /// <summary>Gets the owning dispatcher while attached.</summary>
    public Dispatcher? Dispatcher { get; private set; }

    /// <summary>Gets or sets the requested border-box width.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Length Width
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Measure);
    }

    /// <summary>Gets or sets the requested border-box height.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Length Height
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Measure);
    }

    /// <summary>Gets or sets the non-negative minimum border-box width.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="ArgumentException">The value exceeds <see cref="MaxWidth"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int MinWidth
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (value > MaxWidth)
            {
                throw new ArgumentException("Minimum width cannot exceed maximum width.", nameof(value));
            }

            _ = Set(ref field, value, Invalidation.Measure);
        }
    }

    /// <summary>Gets or sets the non-negative minimum border-box height.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="ArgumentException">The value exceeds <see cref="MaxHeight"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int MinHeight
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (value > MaxHeight)
            {
                throw new ArgumentException("Minimum height cannot exceed maximum height.", nameof(value));
            }

            _ = Set(ref field, value, Invalidation.Measure);
        }
    }

    /// <summary>Gets or sets the maximum border-box width.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="ArgumentException">The value is below <see cref="MinWidth"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int MaxWidth
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (value < MinWidth)
            {
                throw new ArgumentException("Maximum width cannot be below minimum width.", nameof(value));
            }

            _ = Set(ref field, value, Invalidation.Measure);
        }
    } = int.MaxValue;

    /// <summary>Gets or sets the maximum border-box height.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="ArgumentException">The value is below <see cref="MinHeight"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int MaxHeight
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (value < MinHeight)
            {
                throw new ArgumentException("Maximum height cannot be below minimum height.", nameof(value));
            }

            _ = Set(ref field, value, Invalidation.Measure);
        }
    } = int.MaxValue;

    /// <summary>Gets or sets external non-collapsing cell edges.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Thickness Margin
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Measure);
    }

    /// <summary>Gets or sets internal cell edges around content.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Thickness Padding
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Measure);
    }

    /// <summary>Gets or sets horizontal placement within the arranged slot.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public HorizontalAlignment HorizontalAlignment
    {
        get;
        set
        {
            Validate(value);
            _ = Set(ref field, value, Invalidation.Arrange);
        }
    } = HorizontalAlignment.Stretch;

    /// <summary>Gets or sets vertical placement within the arranged slot.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public VerticalAlignment VerticalAlignment
    {
        get;
        set
        {
            Validate(value);
            _ = Set(ref field, value, Invalidation.Arrange);
        }
    } = VerticalAlignment.Stretch;

    /// <summary>Gets or sets local layout/render/input participation.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Visibility Visibility
    {
        get;
        set
        {
            Validate(value);
            var invalidation = value == Visibility.Collapsed || field == Visibility.Collapsed
                ? Invalidation.Measure
                : Invalidation.Render;

            if (Set(ref field, value, invalidation))
            {
                InvalidateDescendants(invalidation);
            }
        }
    } = Visibility.Visible;

    /// <summary>Gets or sets whether local behavior accepts input.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool IsEnabled
    {
        get;
        set
        {
            if (Set(ref field, value, Invalidation.Render))
            {
                InvalidateDescendants(Invalidation.Render);
            }
        }
    } = true;

    /// <summary>Gets whether this control and every ancestor are enabled.</summary>
    public bool EffectiveIsEnabled => IsEnabled && (Parent?.EffectiveIsEnabled ?? true);

    /// <summary>Gets whether this control and every ancestor are visible.</summary>
    public bool EffectiveIsVisible => Visibility == Visibility.Visible &&
        (Parent?.EffectiveIsVisible ?? true);

    /// <summary>Gets or sets whether the control may receive keyboard focus.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool CanFocus
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    }

    /// <summary>Gets or sets the deterministic tab-order key.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int TabIndex
    {
        get;
        set => _ = Set(ref field, value, Invalidation.None);
    }

    /// <summary>Gets the desired border-box size from the last successful measure.</summary>
    public Size DesiredSize { get; internal set; }

    /// <summary>Gets the committed border-box rectangle from the last successful arrange.</summary>
    public Rect Bounds { get; internal set; }

    /// <summary>Gets whether this control has released its owned resources.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Gets dirty phases for the next root transaction.</summary>
    internal Invalidation Pending { get; private set; } = Invalidation.All;

    private Constraint? LastMeasureConstraint { get; set; }

    private Rect? LastArrangeSlot { get; set; }

    private bool IsMeasuring { get; set; }

    private bool IsArranging { get; set; }

    /// <summary>Attaches a root and its descendants to one dispatcher atomically.</summary>
    /// <param name="dispatcher">The non-null owning dispatcher.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatcher"/> is null.</exception>
    /// <exception cref="ArgumentException">Any descendant is already attached.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">Any descendant is disposed.</exception>
    internal void Attach(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        dispatcher.VerifyAccess();
        ValidateAttachment();
        SetDispatcher(dispatcher);
    }

    /// <summary>Detaches this subtree from its dispatcher.</summary>
    internal void Detach()
    {
        var dispatcher = Dispatcher;

        if (dispatcher is null)
        {
            return;
        }

        dispatcher.VerifyAccess();
        SetDispatcher(null);
    }

    /// <summary>Clears selected phases after a successful transaction.</summary>
    /// <param name="value">The completed phases.</param>
    internal void Clear(Invalidation value) => Pending &= ~value;

    /// <summary>Measures the border box within a possibly unbounded slot.</summary>
    /// <param name="constraint">The non-negative outer constraint.</param>
    /// <exception cref="InvalidOperationException">
    /// The attached control is accessed off-dispatcher or measure is reentered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    internal void Measure(Constraint constraint)
    {
        VerifyMutable();

        if (IsMeasuring)
        {
            throw new InvalidOperationException("Measure cannot be reentered.");
        }

        if ((Pending & Invalidation.Measure) == 0 && LastMeasureConstraint == constraint)
        {
            return;
        }

        IsMeasuring = true;
        Clear(Invalidation.Measure);

        try
        {
            if (Visibility == Visibility.Collapsed)
            {
                DesiredSize = default;
                LastMeasureConstraint = constraint;
                Invalidate(Invalidation.Arrange);
                return;
            }

            var contentConstraint = CreateContentConstraint(constraint);
            var content = MeasureCore(contentConstraint);
            var desired = ResolveDesiredSize(constraint, content);

            DesiredSize = desired;
            LastMeasureConstraint = constraint;
            Invalidate(Invalidation.Arrange);
        }
        catch
        {
            Invalidate(Invalidation.Measure);
            throw;
        }
        finally
        {
            IsMeasuring = false;
        }
    }

    /// <summary>Arranges and commits the border box within a final outer slot.</summary>
    /// <param name="slot">The final non-negative outer rectangle.</param>
    /// <exception cref="InvalidOperationException">
    /// The attached control is accessed off-dispatcher or arrange is reentered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    internal void Arrange(Rect slot)
    {
        VerifyMutable();

        if (IsArranging)
        {
            throw new InvalidOperationException("Arrange cannot be reentered.");
        }

        if ((Pending & Invalidation.Arrange) == 0 && LastArrangeSlot == slot)
        {
            return;
        }

        IsArranging = true;
        Clear(Invalidation.Arrange);

        try
        {
            if (Visibility == Visibility.Collapsed)
            {
                Bounds = default;
                LastArrangeSlot = slot;
                return;
            }

            var available = Margin.Deflate(slot);
            var width = ResolveArrangeAxis(
                Width,
                HorizontalAlignment == HorizontalAlignment.Stretch,
                slot.Width,
                available.Width,
                DesiredSize.Width,
                MinWidth,
                MaxWidth);
            var height = ResolveArrangeAxis(
                Height,
                VerticalAlignment == VerticalAlignment.Stretch,
                slot.Height,
                available.Height,
                DesiredSize.Height,
                MinHeight,
                MaxHeight);
            var x = Align(available.X, available.Width, width, HorizontalAlignment);
            var y = Align(available.Y, available.Height, height, VerticalAlignment);
            var bounds = new Rect(x, y, width, height);

            Bounds = bounds;
            LastArrangeSlot = slot;
            ArrangeCore(Padding.Deflate(bounds));
        }
        catch
        {
            Invalidate(Invalidation.Arrange);
            throw;
        }
        finally
        {
            IsArranging = false;
        }
    }

    /// <summary>Requests a phase and every dependent later phase.</summary>
    /// <param name="value">The earliest dirty phase.</param>
    internal void Invalidate(Invalidation value)
    {
        var expanded = Expand(value);
        var added = expanded & ~Pending;

        if (added == Invalidation.None)
        {
            return;
        }

        Pending |= expanded;
        Parent?.Invalidate(value);
    }

    /// <summary>Releases this control and every child it owns.</summary>
    /// <exception cref="InvalidOperationException">The attached control is disposed off-dispatcher.</exception>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        VerifyAccess();
        if (Parent is { } parent)
        {
            _ = parent.Children.Remove(this);
        }

        DisposeChildren();
        Dispatcher = null;
        Pending = Invalidation.None;
        IsDisposed = true;
        PropertyChanged = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes children owned by a derived container.</summary>
    internal virtual void DisposeChildren() =>
        Debug.Assert(!IsDisposed, "Children release occurs before disposal.");

    /// <summary>Visits direct owned children without allocating an intermediate list.</summary>
    /// <param name="visitor">The non-null synchronous visitor.</param>
    internal virtual void VisitChildren(Action<Control> visitor) =>
        ArgumentNullException.ThrowIfNull(visitor);

    /// <summary>Assigns the parent after collection validation.</summary>
    /// <param name="value">The new parent or null.</param>
    internal void SetParent(Container? value) => Parent = value;

    /// <summary>Throws when mutation is not valid for this owner.</summary>
    internal void VerifyMutable()
    {
        ThrowIfDisposed();
        VerifyAccess();
    }

    /// <summary>Validates that the complete subtree may receive a dispatcher.</summary>
    internal void ValidateAttachment()
    {
        ThrowIfDisposed();

        if (Dispatcher is not null)
        {
            throw new ArgumentException("The control is already attached to a dispatcher.");
        }

        VisitChildren(static child => child.ValidateAttachment());
    }

    /// <summary>Measures content inside margin, border-size, and padding constraints.</summary>
    /// <param name="constraint">The content-box constraint.</param>
    /// <returns>The non-negative intrinsic content size.</returns>
    protected virtual Size MeasureCore(Constraint constraint)
    {
        Debug.Assert(!IsDisposed, "A disposed control cannot measure content.");
        return default;
    }

    /// <summary>Arranges content inside the committed padded border box.</summary>
    /// <param name="bounds">The non-negative content-box rectangle.</param>
    protected virtual void ArrangeCore(Rect bounds) =>
        Debug.Assert(!IsDisposed, "A disposed control cannot arrange content.");

    private static Invalidation Expand(Invalidation value) => value switch
    {
        Invalidation.None => Invalidation.None,
        Invalidation.Render => Invalidation.Render,
        Invalidation.Arrange => Invalidation.Arrange | Invalidation.Render,
        Invalidation.Measure => Invalidation.All,
        Invalidation.All => Invalidation.All,
        _ => value & Invalidation.All,
    };

    private static int Align(
        int origin,
        int available,
        int desired,
        HorizontalAlignment alignment) => alignment switch
        {
            HorizontalAlignment.Left or HorizontalAlignment.Stretch => origin,
            HorizontalAlignment.Center => SaturatingAdd(origin, (available - desired) / 2),
            HorizontalAlignment.Right => SaturatingAdd(origin, available - desired),
            _ => throw new UnreachableException(),
        };

    private static int Align(
        int origin,
        int available,
        int desired,
        VerticalAlignment alignment) => alignment switch
        {
            VerticalAlignment.Top or VerticalAlignment.Stretch => origin,
            VerticalAlignment.Center => SaturatingAdd(origin, (available - desired) / 2),
            VerticalAlignment.Bottom => SaturatingAdd(origin, available - desired),
            _ => throw new UnreachableException(),
        };

    private static int ResolveArrangeAxis(
        Length length,
        bool stretch,
        int slot,
        int available,
        int desired,
        int minimum,
        int maximum)
    {
        var requested = length.Kind switch
        {
            Kind.Auto when stretch => available,
            Kind.Auto => desired,
            Kind.Cells => (int) length.Value,
            Kind.Percent => ResolvePercent(slot, length.Value),
            Kind.Star => available,
            _ => throw new UnreachableException(),
        };

        return Math.Min(available, Math.Clamp(requested, minimum, maximum));
    }

    private static int ResolveMeasureAxis(
        Length length,
        int? slot,
        int margin,
        int padding,
        int intrinsic,
        int minimum,
        int maximum)
    {
        var requested = length.Kind switch
        {
            Kind.Auto => SaturatingAdd(intrinsic, padding),
            Kind.Cells => (int) length.Value,
            Kind.Percent => slot.HasValue
                ? ResolvePercent(slot.Value, length.Value)
                : SaturatingAdd(intrinsic, padding),
            Kind.Star => slot.HasValue
                ? Math.Max(0, slot.Value - margin)
                : SaturatingAdd(intrinsic, padding),
            _ => throw new UnreachableException(),
        };
        var clamped = Math.Clamp(requested, minimum, maximum);

        return slot.HasValue
            ? Math.Min(Math.Max(0, slot.Value - margin), clamped)
            : clamped;
    }

    private static int? ResolveContentAxis(
        Length length,
        int? slot,
        int margin,
        int padding)
    {
        int? border = length.Kind switch
        {
            Kind.Auto => slot.HasValue ? Math.Max(0, slot.Value - margin) : null,
            Kind.Cells => (int) length.Value,
            Kind.Percent => slot.HasValue ? ResolvePercent(slot.Value, length.Value) : null,
            Kind.Star => slot.HasValue ? Math.Max(0, slot.Value - margin) : null,
            _ => throw new UnreachableException(),
        };

        if (!border.HasValue)
        {
            return null;
        }

        var available = slot.HasValue ? Math.Max(0, slot.Value - margin) : int.MaxValue;
        return Math.Max(0, Math.Min(border.Value, available) - padding);
    }

    private static int ResolvePercent(int value, double percent)
    {
        var result = Math.Round(value * percent / 100, MidpointRounding.AwayFromZero);
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static int SaturatingAdd(int value, int extent)
    {
        var result = (long) value + extent;
        return result > int.MaxValue ? int.MaxValue : (int) result;
    }

    private static void Validate<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The enum value is unknown.");
        }
    }

    private bool Set<T>(
        ref T field,
        T value,
        Invalidation invalidation,
        [CallerMemberName] string? propertyName = null)
    {
        ThrowIfDisposed();
        VerifyAccess();

        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Invalidate(invalidation);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void InvalidateDescendants(Invalidation value) =>
        VisitChildren(child =>
        {
            child.Invalidate(value);
            child.InvalidateDescendants(value);
        });

    private Constraint CreateContentConstraint(Constraint constraint) => new(
        ResolveContentAxis(Width, constraint.Width, Margin.Horizontal, Padding.Horizontal),
        ResolveContentAxis(Height, constraint.Height, Margin.Vertical, Padding.Vertical));

    private Size ResolveDesiredSize(Constraint constraint, Size content) => new(
        ResolveMeasureAxis(
            Width,
            constraint.Width,
            Margin.Horizontal,
            Padding.Horizontal,
            content.Width,
            MinWidth,
            MaxWidth),
        ResolveMeasureAxis(
            Height,
            constraint.Height,
            Margin.Vertical,
            Padding.Vertical,
            content.Height,
            MinHeight,
            MaxHeight));

    private void SetDispatcher(Dispatcher? value)
    {
        Dispatcher = value;
        VisitChildren(child => child.SetDispatcher(value));
    }

    private void VerifyAccess() => Dispatcher?.VerifyAccess();

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);
}
