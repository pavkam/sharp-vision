using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Styling;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Unicode;
using SharpVision.Threading;

using KeyAction = SharpVision.Terminal.Input.Action;

using TerminalCanvas = SharpVision.Terminal.Rendering.Canvas;
using TerminalStyle = SharpVision.Terminal.Rendering.Style;

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

    /// <summary>Gets the immutable Unicode cell policy inherited from the root.</summary>
    internal Policy CellPolicy { get; private set; } = Policy.Default;

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
    } = HorizontalAlignment.Left;

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

                if (value != Visibility.Visible)
                {
                    NotifyUnavailable(ReleaseReason.Hidden);
                }
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

                if (!value)
                {
                    NotifyUnavailable(ReleaseReason.Disabled);
                }
            }
        }
    } = true;

    /// <summary>Gets whether this control and every ancestor are enabled.</summary>
    public bool EffectiveIsEnabled => IsEnabled && (Parent?.EffectiveIsEnabled ?? true);

    /// <summary>Gets whether this control and every ancestor are visible.</summary>
    public bool EffectiveIsVisible => Visibility == Visibility.Visible &&
        (Parent?.EffectiveIsVisible ?? true);

    /// <summary>Gets or sets whether pointer hit testing may target this control.</summary>
    /// <remarks>
    /// This property affects pointer targeting only. It does not suppress
    /// rendering, visibility, enabled state, or programmatic focus.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool IsHitTestVisible
    {
        get;
        set => _ = Set(ref field, value, Invalidation.None);
    } = true;

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

    /// <summary>Gets or sets the direct style resource, or null to inherit.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Style? Style
    {
        get;
        set
        {
            VerifyMutable();

            if (ReferenceEquals(field, value))
            {
                return;
            }

            UnsubscribeStyle(field);
            field = value;
            SubscribeStyle(field);
            Invalidate(Invalidation.Measure);
            InvalidateStyleDescendants(Impact.Measure);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Style)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Appearance)));
        }
    }

    /// <summary>Gets the resolved inherited appearance for current behavior state.</summary>
    public Appearance Appearance => Resolver.Resolve(EffectiveStyle, GetVisualState());

    /// <summary>Gets whether this control currently owns keyboard focus.</summary>
    public bool IsFocused { get; private set; }

    /// <summary>Gets whether pointer targeting currently hovers this control.</summary>
    public bool IsHovered { get; private set; }

    /// <summary>Gets whether an active pointer press began on this control.</summary>
    public bool IsPressed { get; private set; }

    /// <summary>Gets whether this control owns hover resolved from its hit-tested descendants.</summary>
    /// <remarks>
    /// The default preserves direct leaf hover. Composite interactive controls
    /// override this to expose one semantic hover state for their visible content.
    /// </remarks>
    internal virtual bool OwnsHover => false;

    /// <summary>Gets the desired border-box size from the last successful measure.</summary>
    public Size DesiredSize { get; internal set; }

    /// <summary>Gets the committed border-box rectangle from the last successful arrange.</summary>
    public Rect Bounds { get; internal set; }

    /// <summary>Gets whether this control has released its owned resources.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Gets dirty phases for the next root transaction.</summary>
    internal Invalidation Pending { get; private set; } = Invalidation.All;

    /// <summary>Gets the last outer constraint committed by the measure transaction, or null before initial measurement.</summary>
    /// <remarks>Derived overlay-owned controls use this viewport record when their own resolved box is intentionally smaller than the host.</remarks>
    internal Constraint? LastMeasureConstraint { get; private set; }

    private Rect? LastArrangeSlot { get; set; }

    private bool LastWidthResolved { get; set; }

    private bool LastHeightResolved { get; set; }

    private bool IsMeasuring { get; set; }

    private bool IsArranging { get; set; }

    private bool IsRendering { get; set; }

    private bool HasSelectedState { get; set; }

    private List<IHandler>? Handlers { get; set; }

    /// <summary>Gets the inherited focus manager while one owns this subtree.</summary>
    internal FocusManager? FocusOwner { get; private set; }

    /// <summary>Gets the inherited capture manager while one owns this subtree.</summary>
    internal CaptureManager? CaptureOwner { get; private set; }

    /// <summary>Gets whether this control clips owned descendants to its bounds.</summary>
    internal virtual bool ClipsChildren => true;

    /// <summary>Gets the complete terminal style for the resolved appearance.</summary>
    internal TerminalStyle ResolvedStyle => Resolver.ToTerminal(Appearance);

    /// <summary>Gets the inherited normal-state terminal style for passive visual overflow.</summary>
    internal TerminalStyle NormalStyle => Resolver.ToTerminal(Resolver.Resolve(EffectiveStyle, State.Normal));

    /// <summary>Adds one typed routed-event handler to this control.</summary>
    /// <typeparam name="TArgs">The exact event-argument type.</typeparam>
    /// <param name="routedEvent">The non-null typed event identifier.</param>
    /// <param name="handler">The non-null synchronous handler.</param>
    /// <param name="handledEventsToo">Whether to invoke after handled state is set.</param>
    /// <returns>An idempotent registration that removes the handler on disposal.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The same event and delegate are registered.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public IDisposable AddHandler<TArgs>(
        Event<TArgs> routedEvent,
        EventHandler<TArgs> handler,
        bool handledEventsToo = false) where TArgs : RoutedEventArgs
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        ArgumentNullException.ThrowIfNull(handler);
        VerifyMutable();

        if (Handlers is not null && Handlers.Exists(item => item.Matches(routedEvent, handler)))
        {
            throw new ArgumentException(
                "The same handler is already registered for this event.",
                nameof(handler));
        }

        var registration = new Registration<TArgs>(
            this,
            routedEvent,
            handler,
            handledEventsToo,
            Sequence.Next());
        (Handlers ??= []).Add(registration);
        return registration;
    }

    /// <summary>Returns the highest eligible control containing a screen-cell point.</summary>
    /// <param name="point">The screen-cell point.</param>
    /// <returns>This control when eligible and contained; otherwise null.</returns>
    public virtual Control? HitTest(Point point) =>
        !IsDisposed && IsHitTestVisible && EffectiveIsVisible && EffectiveIsEnabled &&
        Bounds.Contains(point)
            ? this
            : null;

    /// <summary>Attaches a root and its descendants to one dispatcher atomically.</summary>
    /// <param name="dispatcher">The non-null owning dispatcher.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatcher"/> is null.</exception>
    /// <exception cref="ArgumentException">Any descendant is already attached.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">Any descendant is disposed.</exception>
    internal void Attach(Dispatcher dispatcher)
        => Attach(dispatcher, Policy.Default);

    /// <summary>Attaches a root and descendants with one immutable cell policy.</summary>
    /// <param name="dispatcher">The non-null owning dispatcher.</param>
    /// <param name="cellPolicy">The non-null inherited Unicode cell policy.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException">Any descendant is already attached.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">Any descendant is disposed.</exception>
    internal void Attach(Dispatcher dispatcher, Policy cellPolicy)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(cellPolicy);
        dispatcher.VerifyAccess();
        ValidateAttachment();
        SetCellPolicy(cellPolicy);
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
        SetCellPolicy(Policy.Default);
    }

    /// <summary>Assigns one immutable Unicode cell policy recursively.</summary>
    /// <param name="value">The non-null inherited cell policy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal void SetCellPolicy(Policy value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (ReferenceEquals(CellPolicy, value))
        {
            return;
        }

        CellPolicy = value;
        VisitChildren(child => child.SetCellPolicy(value));
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
    internal void Arrange(Rect slot) => Arrange(slot, widthResolved: false, heightResolved: false);

    /// <summary>Arranges with optional parent-resolved border-box axes.</summary>
    /// <param name="slot">The final non-negative outer rectangle including margin.</param>
    /// <param name="widthResolved">Whether the parent already resolved the border-box width.</param>
    /// <param name="heightResolved">Whether the parent already resolved the border-box height.</param>
    /// <exception cref="InvalidOperationException">
    /// The attached control is accessed off-dispatcher or arrange is reentered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    internal void Arrange(Rect slot, bool widthResolved, bool heightResolved)
    {
        VerifyMutable();

        if (IsArranging)
        {
            throw new InvalidOperationException("Arrange cannot be reentered.");
        }

        if ((Pending & Invalidation.Arrange) == 0 &&
            LastArrangeSlot == slot &&
            LastWidthResolved == widthResolved &&
            LastHeightResolved == heightResolved)
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
                LastWidthResolved = widthResolved;
                LastHeightResolved = heightResolved;
                return;
            }

            var available = Margin.Deflate(slot);
            var width = widthResolved
                ? available.Width
                : ResolveArrangeAxis(
                    Width,
                    HorizontalAlignment == HorizontalAlignment.Stretch,
                    slot.Width,
                    available.Width,
                    DesiredSize.Width,
                    MinWidth,
                    MaxWidth);
            var height = heightResolved
                ? available.Height
                : ResolveArrangeAxis(
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
            LastWidthResolved = widthResolved;
            LastHeightResolved = heightResolved;
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

    /// <summary>Renders this control and owned descendants into a clipped semantic canvas.</summary>
    /// <param name="canvas">The frame-owned parent canvas.</param>
    /// <exception cref="InvalidOperationException">
    /// The attached control is accessed off-dispatcher or render is reentered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The control or canvas is disposed.</exception>
    internal void Render(TerminalCanvas canvas)
    {
        VerifyMutable();
        _ = canvas.Bounds;

        if (IsRendering)
        {
            throw new InvalidOperationException("Render cannot be reentered.");
        }

        IsRendering = true;
        Clear(Invalidation.Render);

        try
        {
            if (!EffectiveIsVisible)
            {
                return;
            }

            // Every control receives a canvas clipped by every ancestor. The
            // coordinate system remains absolute, so no transform can drift.
            // Panels may deliberately retain the ancestor clip for children,
            // while their own drawing always remains inside their bounds.
            var visual = canvas.Clip(VisualBounds);
            var clipped = canvas.Clip(Bounds);
            RenderCore(visual);
            RenderChildren(ClipsChildren ? clipped : canvas);
        }
        catch
        {
            Invalidate(Invalidation.Render);
            throw;
        }
        finally
        {
            IsRendering = false;
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
        NotifyUnavailable(ReleaseReason.Disposed);
        if (Parent is { } parent)
        {
            _ = parent.Children.Remove(this);
        }

        DisposeChildren();
        ClearHandlers();
        UnsubscribeStyle(Style);
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

    /// <summary>Returns the topmost open popup descendant containing one screen-cell point.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <returns>An open popup target, or null when this subtree has none.</returns>
    internal virtual Control? HitTestPopup(Point point)
    {
        _ = point;
        return null;
    }

    /// <summary>Renders open popup descendants after ordinary sibling content.</summary>
    /// <param name="canvas">The non-null root-relative canvas used by the current frame.</param>
    internal virtual void RenderPopupLayer(TerminalCanvas canvas) => _ = canvas.Bounds;

    /// <summary>Assigns the parent after collection validation.</summary>
    /// <param name="value">The new parent or null.</param>
    internal void SetParent(Container? value)
    {
        var previous = Parent;
        Parent = value;
        OnParentChanged(previous, value);
    }

    /// <summary>Throws when mutation is not valid for this owner.</summary>
    internal void VerifyMutable()
    {
        ThrowIfDisposed();
        VerifyAccess();
    }

    /// <summary>Invokes handlers that existed when the active route began.</summary>
    internal void InvokeHandlers(IEvent routedEvent, RoutedEventArgs eventArgs, long sequence)
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        ArgumentNullException.ThrowIfNull(eventArgs);
        var handlers = Handlers;

        if (handlers is null || handlers.Count == 0)
        {
            return;
        }

        if (eventArgs is PointerEventArgs pointer)
        {
            pointer.SetLocal(this);
        }

        var snapshot = System.Buffers.ArrayPool<IHandler>.Shared.Rent(handlers.Count);
        handlers.CopyTo(snapshot);
        var count = handlers.Count;

        try
        {
            for (var index = 0; index < count; index++)
            {
                snapshot[index].Invoke(this, routedEvent, eventArgs, sequence);
            }
        }
        finally
        {
            Array.Clear(snapshot, 0, count);
            System.Buffers.ArrayPool<IHandler>.Shared.Return(snapshot);
        }
    }

    /// <summary>Runs this route member's default behavior after an unhandled bubble.</summary>
    internal void InvokeDefault(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        OnEvent(eventArgs);

        if (!eventArgs.Handled &&
            eventArgs is KeyEventArgs
            {
                Stroke:
                {
                    Code: Code.Tab,
                    Action: KeyAction.Press,
                    Modifiers: var modifiers,
                },
            } &&
            (modifiers & ~Modifiers.Shift) == 0 &&
            FocusOwner?.MoveNext((modifiers & Modifiers.Shift) != 0) == true)
        {
            eventArgs.Handled = true;
        }
    }

    /// <summary>Removes one live registration after dispatcher validation.</summary>
    internal void RemoveHandler(IHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        VerifyMutable();

        if (Handlers?.Remove(handler) == true)
        {
            handler.Detach();
        }
    }

    /// <summary>Assigns inherited focus-manager ownership recursively.</summary>
    internal void SetFocusOwner(FocusManager? value)
    {
        FocusOwner = value;
        VisitChildren(child => child.SetFocusOwner(value));
    }

    /// <summary>Assigns inherited capture-manager ownership recursively.</summary>
    internal void SetCaptureOwner(CaptureManager? value)
    {
        CaptureOwner = value;
        VisitChildren(child => child.SetCaptureOwner(value));
    }

    /// <summary>Updates focus visual state on the owning dispatcher.</summary>
    internal void SetFocused(bool value)
    {
        VerifyMutable();

        if (IsFocused == value)
        {
            return;
        }

        IsFocused = value;
        Invalidate(Invalidation.Render);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFocused)));
        OnFocusChanged(value);
    }

    /// <summary>Updates hover visual state on the owning dispatcher.</summary>
    internal void SetHovered(bool value)
    {
        VerifyMutable();

        if (IsHovered == value)
        {
            return;
        }

        IsHovered = value;
        Invalidate(Invalidation.Render);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHovered)));
    }

    /// <summary>Updates pressed visual state on the owning dispatcher.</summary>
    internal void SetPressed(bool value)
    {
        VerifyMutable();

        if (IsPressed == value)
        {
            return;
        }

        IsPressed = value;
        Invalidate(Invalidation.Render);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPressed)));
        OnPressedChanged(value);
    }

    /// <summary>Propagates semantic selected visual state through one realized item subtree.</summary>
    /// <param name="value">Whether the subtree is selected.</param>
    internal void SetSelectedState(bool value)
    {
        VerifyMutable();

        if (HasSelectedState == value)
        {
            return;
        }

        HasSelectedState = value;
        Invalidate(Invalidation.Render);
        VisitChildren(child => child.SetSelectedState(value));
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

    /// <summary>Runs target-specific default behavior for one unhandled routed event.</summary>
    /// <param name="eventArgs">The non-null event state and typed payload.</param>
    protected virtual void OnEvent(RoutedEventArgs eventArgs) =>
        ArgumentNullException.ThrowIfNull(eventArgs);

    /// <summary>Responds after this control's keyboard-focus state changes.</summary>
    /// <param name="focused">The newly committed focus state.</param>
    protected virtual void OnFocusChanged(bool focused) =>
        Debug.Assert(!IsDisposed, "A disposed control cannot change focus state.");

    /// <summary>Responds after this control's pressed visual state commits.</summary>
    /// <param name="pressed">The newly committed pressed state.</param>
    protected virtual void OnPressedChanged(bool pressed) =>
        Debug.Assert(!IsDisposed, "A disposed control cannot change pressed state.");

    /// <summary>Responds after this control's direct ownership changes.</summary>
    /// <param name="previous">The previous owner, or null.</param>
    /// <param name="current">The committed owner, or null.</param>
    protected virtual void OnParentChanged(Container? previous, Container? current)
    {
        _ = previous;
        _ = current;
        Debug.Assert(!IsDisposed, "A disposed control cannot change parent.");
    }

    /// <summary>Releases derived transient state when this control becomes unavailable.</summary>
    /// <param name="reason">The precise unavailability reason.</param>
    protected virtual void OnUnavailable(ReleaseReason reason) =>
        Debug.Assert(Enum.IsDefined(reason), "Unavailable reasons are validated internally.");

    /// <summary>Draws this control's own content into its clipped visual bounds.</summary>
    /// <param name="canvas">The frame-owned canvas clipped to <see cref="VisualBounds"/>.</param>
    protected virtual void RenderCore(TerminalCanvas canvas)
    {
        _ = canvas.Bounds;
        Debug.Assert(!IsDisposed, "A disposed control cannot render content.");
    }

    /// <summary>Gets the own-content drawing bounds, including deliberate visual overflow.</summary>
    /// <remarks>
    /// The default is <see cref="Bounds"/>. Overrides affect own drawing only;
    /// descendant clipping and pointer hit testing continue to use the arranged box.
    /// </remarks>
    protected virtual Rect VisualBounds => Bounds;

    /// <summary>Renders owned descendants after this control's content.</summary>
    /// <param name="canvas">The canvas clipped to this control.</param>
    internal virtual void RenderChildren(TerminalCanvas canvas)
    {
        _ = canvas.Bounds;
        Debug.Assert(!IsDisposed, "A disposed control cannot render children.");
    }

    /// <summary>Gets behavior-derived flags for appearance resolution.</summary>
    /// <returns>The current defined visual-state flags.</returns>
    protected virtual State GetVisualState()
    {
        var result = State.Normal;

        if (IsHovered)
        {
            result |= State.Hovered;
        }

        if (IsFocused)
        {
            result |= State.Focused;
        }

        if (IsPressed)
        {
            result |= State.Pressed;
        }

        if (!EffectiveIsEnabled)
        {
            result |= State.Disabled;
        }

        if (HasSelectedState)
        {
            result |= State.Checked;
        }

        return result;
    }

    /// <summary>Gets the committed content rectangle after padding deflation.</summary>
    protected Rect ContentBounds => Padding.Deflate(Bounds);

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

    /// <summary>Commits one derived or base property and requests its earliest phase.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="field">The current backing field.</param>
    /// <param name="value">The validated replacement value.</param>
    /// <param name="invalidation">The earliest affected phase.</param>
    /// <param name="propertyName">The property name supplied by the compiler.</param>
    /// <returns>Whether a changed value was committed.</returns>
    private protected bool Set<T>(
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

    /// <summary>Raises one derived committed-property notification after atomic field mutation.</summary>
    /// <param name="propertyName">The non-empty public property name.</param>
    /// <param name="invalidation">The earliest phase affected by the committed transaction.</param>
    /// <exception cref="ArgumentException"><paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    private protected void NotifyChanged(string propertyName, Invalidation invalidation)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        VerifyMutable();
        Invalidate(invalidation);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void InvalidateDescendants(Invalidation value) =>
        VisitChildren(child =>
        {
            child.Invalidate(value);
            child.InvalidateDescendants(value);
        });

    private void InvalidateStyleDescendants(Impact impact) =>
        VisitChildren(child =>
        {
            if (child.Style is not null)
            {
                return;
            }

            child.Invalidate(impact == Impact.Measure ? Invalidation.Measure : Invalidation.Render);
            child.PropertyChanged?.Invoke(
                child,
                new PropertyChangedEventArgs(nameof(Appearance)));
            child.InvalidateStyleDescendants(impact);
        });

    private void OnStyleChanged(object? sender, ChangedEventArgs eventArgs)
    {
        var dispatcher = Dispatcher;

        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Post(() => ApplyStyleChanged(sender, eventArgs));
            return;
        }

        ApplyStyleChanged(sender, eventArgs);
    }

    private void ApplyStyleChanged(object? sender, ChangedEventArgs eventArgs)
    {
        if (IsDisposed || !ReferenceEquals(sender, Style))
        {
            return;
        }

        var invalidation = eventArgs.Impact == Impact.Measure
            ? Invalidation.Measure
            : Invalidation.Render;
        Invalidate(invalidation);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Appearance)));
        InvalidateStyleDescendants(eventArgs.Impact);
    }

    private void ClearHandlers()
    {
        if (Handlers is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            handler.Detach();
        }

        handlers.Clear();
        Handlers = null;
    }

    internal void NotifyUnavailable(ReleaseReason reason)
    {
        var focus = FocusOwner;
        var capture = CaptureOwner;
        focus?.Unavailable(this);
        capture?.Unavailable(this, reason);
        OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            if (focus is not null && ReferenceEquals(focus.Root, this))
            {
                focus.RootDisposed();
            }

            if (capture is not null && ReferenceEquals(capture.Root, this))
            {
                capture.RootDisposed();
            }
        }
    }

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
        UnsubscribeStyle(Style);
        Dispatcher = value;
        SubscribeStyle(Style);
        VisitChildren(child => child.SetDispatcher(value));
    }

    private void SubscribeStyle(Style? style)
    {
        if (Dispatcher is not null && style is not null)
        {
            style.Changed += OnStyleChanged;
        }
    }

    private void UnsubscribeStyle(Style? style) => style?.Changed -= OnStyleChanged;

    private Style? EffectiveStyle => Style ?? Parent?.EffectiveStyle;

    private void VerifyAccess() => Dispatcher?.VerifyAccess();

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);
}
