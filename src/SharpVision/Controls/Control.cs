// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.ComponentModel;
using System.Runtime.ExceptionServices;

using SharpVision.Terminal.Input;

/// <summary>
/// Defines a traditional mutable UI element with dispatcher affinity and box layout.
/// </summary>
/// <remarks>
/// Detached controls may be assembled on any thread. Once attached, every
/// mutation and disposal must run on <see cref="Dispatcher"/>.
/// </remarks>
public abstract partial class Control: INotifyPropertyChanged, IDisposable
{
    /// <summary>Initializes an empty control with one central visual-ownership registry.</summary>
    protected Control() => OwnedControls = new OwnedControlRegistry(this);

    /// <summary>Raised after one public property has committed a changed value.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the owning parent, or null for a detached/root control.</summary>
    public Control? Parent { get; private set; }

    /// <summary>Gets the exact slot owning this control, or null for an ownership root.</summary>
    internal OwnedControlSlot? OwningSlot { get; private set; }

    /// <summary>Gets the owning dispatcher while attached.</summary>
    public Dispatcher? Dispatcher { get; private set; }

    /// <summary>Gets the immutable Unicode cell policy inherited from the root.</summary>
    protected internal Policy CellPolicy { get; private set; } = Policy.Default;

    /// <summary>Gets or sets the requested border-box width.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Length Width
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Measure);
    }

    /// <summary>Gets or sets the requested border-box height.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Length Height
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Measure);
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

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
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

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
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

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
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

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = int.MaxValue;

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
            _ = SetProperty(ref field, value, ChangeImpact.Arrange);
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
            _ = SetProperty(ref field, value, ChangeImpact.Arrange);
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
            var impact = value == Visibility.Collapsed || field == Visibility.Collapsed
                ? ChangeImpact.Measure
                : ChangeImpact.Render;

            if (SetProperty(ref field, value, impact))
            {
                InvalidateDescendants(InvalidationFor(impact));

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
            if (SetProperty(ref field, value, ChangeImpact.Render))
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
        set => _ = SetProperty(ref field, value, ChangeImpact.None);
    } = true;

    /// <summary>Gets or sets whether the control may receive keyboard focus.</summary>
    /// <remarks>
    /// Setting this property to false releases focus before the property-change
    /// notification. During an active focus callback, both cleanup and notification
    /// complete before the enclosing focus request returns. Pointer capture is unaffected.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool CanFocus
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate(Invalidation.Render);

            if (CanFocusNotificationPending)
            {
                return;
            }

            CanFocusNotificationPending = true;

            try
            {
                if (!value && FocusOwner?.Ineligible(this) == false)
                {
                    return;
                }

                PublishDeferredCanFocusChange();
            }
            catch
            {
                CanFocusNotificationPending = false;
                throw;
            }
        }
    }

    /// <summary>Gets or sets the deterministic tab-order key.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int TabIndex
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.None);
    }

    /// <summary>Gets or sets the direct style resource, or null to inherit.</summary>
    /// <exception cref="ArgumentException">
    /// The style targets a type this control does not derive from or reports an unknown change impact.
    /// </exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public IControlStyle? Style
    {
        get => InstanceStyle;
        set
        {
            VerifyMutable();

            if (value is not null && !value.TargetType.IsAssignableFrom(GetType()))
            {
                throw new ArgumentException(
                    $"A style targeting {value.TargetType.Name} cannot be applied to {GetType().Name}.",
                    nameof(value));
            }

            if (ReferenceEquals(InstanceStyle, value))
            {
                return;
            }

            var replacementImpact = value?.AggregateImpact ?? ChangeImpact.None;

            if (!Enum.IsDefined(replacementImpact))
            {
                throw new ArgumentException(
                    "The style reports an unknown change impact.",
                    nameof(value));
            }

            var previous = InstanceStyle;
            var impact = MaximumImpact(
                previous?.AggregateImpact ?? ChangeImpact.None,
                replacementImpact);
            var invalidation = InvalidationFor(impact);
            UnsubscribeInstanceStyle(previous);
            InstanceStyle = value;
            SubscribeInstanceStyle(value);
            InvalidateResolvedStyleCache();
            Invalidate(invalidation);
            CascadeStyleScopeInvalidation(invalidation);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Style)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    /// <summary>Gets whether this control currently owns keyboard focus.</summary>
    public bool IsFocused { get; private set; }

    /// <summary>Gets whether the pointer currently hovers this control; only interactive (focusable) controls are marked hovered.</summary>
    public bool IsHovered { get; private set; }

    /// <summary>Gets whether an active pointer press began on this control.</summary>
    public bool IsPressed { get; private set; }

    /// <summary>Gets whether this control is an interactive hover target.</summary>
    /// <remarks>
    /// Hover feedback is reserved for interactive controls, so the default follows
    /// <see cref="CanFocus"/>. Hover over a non-interactive descendant resolves up to
    /// the nearest owner, which is how a composite interactive control claims one
    /// semantic hover state for its visible content.
    /// </remarks>
    internal virtual bool OwnsHover => CanFocus;

    /// <summary>Gets the desired border-box size from the last successful measure.</summary>
    public Size DesiredSize { get; internal set; }

    /// <summary>Gets the natural content size from the last measure, before outer-constraint clamping.</summary>
    /// <remarks>Equals <see cref="MeasureOverride"/>'s result. Scrollable containers compare it against the arranged viewport.</remarks>
    internal Size ContentExtent { get; private set; }

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

    private bool IsDisposing { get; set; }

    private bool OwnedDisposalRequested { get; set; }

    private bool CanFocusNotificationPending { get; set; }

    private bool HasSelectedState { get; set; }

    private List<IHandler>? Handlers { get; set; }

    /// <summary>Gets this control's central direct-ownership registry.</summary>
    internal OwnedControlRegistry OwnedControls { get; }

    /// <summary>Gets the inherited focus manager while one owns this subtree.</summary>
    internal FocusManager? FocusOwner { get; private set; }

    /// <summary>Gets the inherited capture manager while one owns this subtree.</summary>
    internal CaptureManager? CaptureOwner { get; private set; }

    /// <summary>Gets whether this control clips owned descendants to its bounds.</summary>
    /// <remarks>
    /// The framework reads this value while rendering children. Derived controls
    /// may return false only when their documented visual overflow requires the
    /// ancestor clip instead of this control's bounds.
    /// </remarks>
    protected virtual bool ClipsChildren => true;

    /// <summary>Gets the complete terminal style for the resolved appearance.</summary>
    protected internal TerminalStyle ResolvedStyle => GetResolvedStyle(GetVisualState());

    /// <summary>Gets the inherited normal-state terminal style for passive visual overflow.</summary>
    protected internal TerminalStyle NormalStyle => GetResolvedStyle(State.Normal);

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
    public virtual Control? HitTest(Point point)
    {
        var contains = Bounds.Contains(point);
        return CanHitTestSelf(point, requireContainment: false)
            ? HitTestPopup(point) ??
                (!ClipsChildren || contains ? OwnedControls.HitTestNormal(point) : null) ??
                (contains ? this : null)
            : null;
    }

    /// <summary>Attaches a root and its descendants to one dispatcher atomically.</summary>
    /// <param name="dispatcher">The non-null owning dispatcher.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatcher"/> is null.</exception>
    /// <exception cref="ArgumentException">Any descendant is already attached.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher or this control is owned.</exception>
    /// <exception cref="ObjectDisposedException">Any descendant is disposed.</exception>
    internal void Attach(Dispatcher dispatcher)
        => Attach(dispatcher, Policy.Default);

    /// <summary>Attaches a root and descendants with one immutable cell policy.</summary>
    /// <param name="dispatcher">The non-null owning dispatcher.</param>
    /// <param name="cellPolicy">The non-null inherited Unicode cell policy.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException">Any descendant is already attached.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher or this control is owned.</exception>
    /// <exception cref="ObjectDisposedException">Any descendant is disposed.</exception>
    internal void Attach(Dispatcher dispatcher, Policy cellPolicy)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(cellPolicy);
        VerifyLifecycleRoot();
        dispatcher.VerifyAccess();
        ValidateAttachment();
        CommitAndPublishContext(
            dispatcher,
            cellPolicy,
            FocusOwner,
            CaptureOwner,
            ThemeContext,
            configure: null);
    }

    /// <summary>Stages application-root context and publishes lifecycle only after managers are configured.</summary>
    /// <param name="dispatcher">The non-null owning dispatcher.</param>
    /// <param name="cellPolicy">The non-null inherited Unicode cell policy.</param>
    /// <param name="themeContext">The non-null initial theme context.</param>
    /// <param name="configure">Framework setup that installs focus and capture managers before publication.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException">Any descendant is already attached.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher or this control is owned.</exception>
    /// <exception cref="ObjectDisposedException">Any descendant is disposed.</exception>
    internal void Attach(
        Dispatcher dispatcher,
        Policy cellPolicy,
        ThemeContext themeContext,
        System.Action configure)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(cellPolicy);
        ArgumentNullException.ThrowIfNull(themeContext);
        ArgumentNullException.ThrowIfNull(configure);
        VerifyLifecycleRoot();
        dispatcher.VerifyAccess();
        ValidateAttachment();
        CommitAndPublishContext(
            dispatcher,
            cellPolicy,
            FocusOwner,
            CaptureOwner,
            themeContext,
            configure);
    }

    /// <summary>Detaches this ownership root and its subtree from its dispatcher.</summary>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher or this control is owned.</exception>
    internal void Detach()
    {
        VerifyLifecycleRoot();
        OwnedControlRegistry.VerifyMutationAllowed(this);
        var dispatcher = Dispatcher;

        if (dispatcher is null)
        {
            return;
        }

        dispatcher.VerifyAccess();
        var entered = OwnedControlRegistry.EnterPublication(this);
        var failure = (ExceptionDispatchInfo?) null;

        try
        {
            CaptureFailure(() => NotifyUnavailable(ReleaseReason.Detached), ref failure);
            var themeChanged = new List<Control>();
            var attached = new List<Control>();
            var detached = new List<Control>();
            CommitSubtreeContext(
                null,
                Policy.Default,
                null,
                null,
                null,
                themeChanged,
                attached,
                detached);
            PublishContextChanges(themeChanged, attached, detached, ref failure);
        }
        finally
        {
            OwnedControlRegistry.ExitPublication(entered);
        }

        failure?.Throw();
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

            var contentConstraint = OnMeasuringContent(CreateContentConstraint(constraint));
            var content = MeasureOverride(contentConstraint);
            ContentExtent = content;
            var desired = OnMeasuredDesired(ResolveDesiredSize(constraint, content));

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
                : ShrinkWrapsWidth
                    ? Math.Min(available.Width, Math.Clamp(DesiredSize.Width, MinWidth, MaxWidth))
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
                : ShrinkWrapsHeight
                    ? Math.Min(available.Height, Math.Clamp(DesiredSize.Height, MinHeight, MaxHeight))
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
            var content = Padding.Deflate(BorderThickness.Deflate(bounds));
            ArrangeOverride(ResolveContentSlot(content));
            ArrangeOverlays(content);
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

    /// <summary>Measures one direct owned child through the framework layout transaction.</summary>
    /// <param name="child">The non-null direct child owned by this control.</param>
    /// <param name="constraint">The non-negative content constraint supplied to the child.</param>
    /// <returns>The child's committed desired border-box size.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="child"/> is not directly owned by this control.</exception>
    /// <exception cref="InvalidOperationException">
    /// The attached child is accessed off-dispatcher or measure is reentered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The child is disposed.</exception>
    protected Size MeasureChild(Control child, Constraint constraint)
    {
        ArgumentNullException.ThrowIfNull(child);
        EnsureDirectOwnedChild(child);
        child.Measure(constraint);
        return child.DesiredSize;
    }

    /// <summary>Arranges one direct owned child through the framework layout transaction.</summary>
    /// <param name="child">The non-null direct child owned by this control.</param>
    /// <param name="slot">The final non-negative outer slot assigned to the child.</param>
    /// <param name="resolvedAxes">Axes whose border-box sizes were already resolved by this parent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="resolvedAxes"/> contains an unknown flag.</exception>
    /// <exception cref="ArgumentException"><paramref name="child"/> is not directly owned by this control.</exception>
    /// <exception cref="InvalidOperationException">
    /// The attached child is accessed off-dispatcher or arrange is reentered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The child is disposed.</exception>
    protected void ArrangeChild(
        Control child,
        Rect slot,
        ResolvedAxes resolvedAxes = ResolvedAxes.None)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (!Enum.IsDefined(resolvedAxes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolvedAxes),
                resolvedAxes,
                "The resolved axes contain an unknown flag.");
        }

        EnsureDirectOwnedChild(child);
        child.Arrange(
            slot,
            widthResolved: (resolvedAxes & ResolvedAxes.Width) != 0,
            heightResolved: (resolvedAxes & ResolvedAxes.Height) != 0);
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
            OnRender(visual);
            RenderChildren(ClipsChildren ? clipped : canvas);

            if (Parent is null)
            {
                RenderOwnedPopupDescendants(canvas);
            }
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

    /// <summary>Requests the earliest UI phase affected by derived control state.</summary>
    /// <param name="impact">The validated earliest affected phase.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="impact"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected void Invalidate(ChangeImpact impact)
    {
        ValidateImpact(impact);
        VerifyMutable();
        Invalidate(InvalidationFor(impact));
    }

    /// <summary>Clears resolved appearance caches and requests the phase required by active styles.</summary>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected void InvalidateVisualState()
    {
        VerifyMutable();
        InvalidateResolvedStyleCache();
        Invalidate(VisualStateInvalidation());
    }

    /// <summary>Requests keyboard focus from the manager inherited by this control.</summary>
    /// <returns>True when focus is acquired or already owned; false when detached or ineligible.</returns>
    /// <exception cref="InvalidOperationException">
    /// The attached control is accessed off-dispatcher or focus is reentered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected bool RequestFocus()
    {
        VerifyMutable();
        return FocusOwner?.Focus(this) ?? false;
    }

    /// <summary>Requests exclusive pointer capture from the manager inherited by this control.</summary>
    /// <returns>True when capture is acquired or already owned; false when detached or ineligible.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected bool CapturePointer()
    {
        VerifyMutable();
        return CaptureOwner?.Capture(this) ?? false;
    }

    /// <summary>Gets whether this control is the current exclusive pointer-capture target.</summary>
    protected bool HasPointerCapture => ReferenceEquals(CaptureOwner?.Captured, this);

    /// <summary>Releases pointer capture only when this control currently owns it.</summary>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected void ReleasePointerCapture()
    {
        VerifyMutable();
        CaptureOwner?.Release(this);
    }

    /// <summary>Releases this control and every child it owns.</summary>
    /// <exception cref="InvalidOperationException">
    /// The attached control is disposed off-dispatcher or disposal reenters structural publication.
    /// </exception>
    public void Dispose()
    {
        if (IsDisposed || IsDisposing)
        {
            GC.SuppressFinalize(this);
            return;
        }

        if (!OwnedDisposalRequested)
        {
            OwnedControlRegistry.VerifyMutationAllowed(this);
        }

        try
        {
            DisposeWithPublication();
        }
        finally
        {
            if (IsDisposed)
            {
                GC.SuppressFinalize(this);
            }
        }
    }

    /// <summary>Disposes a child while its owner already holds structural publication.</summary>
    internal void DisposeOwned()
    {
        if (IsDisposed || IsDisposing)
        {
            return;
        }

        Debug.Assert(OwningSlot is not null, "Registry disposal targets one currently owned child.");
        OwnedDisposalRequested = true;

        try
        {
            Dispose();
        }
        finally
        {
            OwnedDisposalRequested = false;
        }
    }

    private void DisposeWithPublication()
    {
        var entered = OwnedControlRegistry.EnterPublication(this, [this]);

        try
        {
            DisposeCore();
        }
        finally
        {
            OwnedControlRegistry.ExitPublication(entered);
        }
    }

    private void DisposeCore()
    {
        VerifyAccess();
        IsDisposing = true;
        var failure = (ExceptionDispatchInfo?) null;
        CaptureFailure(OnDisposing, ref failure);

        try
        {
            CaptureFailure(
                () => NotifyUnavailable(ReleaseReason.Disposed),
                ref failure);

            if (OwningSlot is { } slot)
            {
                CaptureFailure(
                    () => slot.RemoveForDisposalWithinPublication(this),
                    ref failure);
            }

            CaptureFailure(OwnedControls.DisposeAll, ref failure);
            CaptureFailure(ClearHandlers, ref failure);
            CaptureFailure(
                () => UnsubscribeInstanceStyle(InstanceStyle),
                ref failure);
        }
        finally
        {
            Dispatcher = null;
            Pending = Invalidation.None;
            IsDisposed = true;
            IsDisposing = false;
            PropertyChanged = null;
        }

        failure?.Throw();
    }

    /// <summary>Visits direct owned children without allocating an intermediate list.</summary>
    /// <param name="visitor">The non-null synchronous visitor.</param>
    internal void VisitChildren(Action<Control> visitor) => OwnedControls.Visit(visitor);

    /// <summary>Gets the total number of direct controls across every ownership slot.</summary>
    internal int OwnedControlCount => OwnedControls.Count;

    /// <summary>Gets one direct control in slot-registration and item order.</summary>
    /// <param name="index">The valid zero-based global position.</param>
    /// <returns>The owned control at the requested position.</returns>
    internal Control OwnedControlAt(int index) => OwnedControls.At(index);

    /// <summary>Gets the number of direct controls eligible for default focus navigation.</summary>
    internal virtual int NavigationCount => OwnedControls.NavigationCount;

    /// <summary>Gets one direct control in default focus-navigation order.</summary>
    /// <param name="index">The valid zero-based navigation position.</param>
    /// <returns>The navigation-eligible child at the requested position.</returns>
    internal virtual Control NavigationAt(int index) => OwnedControls.NavigationAt(index);

    /// <summary>Returns the topmost open popup descendant containing one screen-cell point.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <returns>An open popup target, or null when this subtree has none.</returns>
    internal Control? HitTestPopup(Point point) =>
        CanHitTestSelf(point, requireContainment: false) ? HitTestPopupCore(point) : null;

    /// <summary>Searches elevated descendants after owner eligibility has been validated.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <returns>An open popup target, or null when this subtree has none.</returns>
    internal virtual Control? HitTestPopupCore(Point point) => OwnedControls.HitTestPopup(point);

    /// <summary>Renders open popup descendants after ordinary sibling content.</summary>
    /// <param name="canvas">The non-null root-relative canvas used by the current frame.</param>
    internal virtual void RenderPopupLayer(TerminalCanvas canvas) => RenderOwnedPopupDescendants(canvas);

    /// <summary>Renders elevated descendants without redispatching through this control's popup hook.</summary>
    /// <param name="canvas">The root-relative frame canvas.</param>
    internal virtual void RenderOwnedPopupDescendants(TerminalCanvas canvas) => OwnedControls.RenderPopup(canvas);

    /// <summary>Gets the minimum visual layer required by this control independent of its owning slot.</summary>
    /// <remarks>Ordinary controls use their slot layer; popup surfaces promote themselves until every owner has a dedicated popup slot.</remarks>
    internal virtual OwnedControlLayer IntrinsicLayer => OwnedControlLayer.Normal;

    /// <summary>Gets whether a specialized ordinary-content loop may render this control inline.</summary>
    internal bool RendersInNormalLayer => IntrinsicLayer == OwnedControlLayer.Normal;

    /// <summary>Resolves slot metadata and intrinsic promotion into one effective visual layer.</summary>
    /// <param name="slotLayer">The defined layer declared by the owning slot.</param>
    /// <returns>The effective normal or popup layer.</returns>
    internal OwnedControlLayer ResolveOwnedLayer(OwnedControlLayer slotLayer) =>
        slotLayer == OwnedControlLayer.Popup || IntrinsicLayer == OwnedControlLayer.Popup
            ? OwnedControlLayer.Popup
            : OwnedControlLayer.Normal;

    /// <summary>Finds one elevated target within this branch using its effective owned layer.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <param name="slotLayer">The defined layer declared by the owning slot.</param>
    /// <returns>The topmost elevated target, or null.</returns>
    internal Control? HitTestPopupBranch(Point point, OwnedControlLayer slotLayer) =>
        HitTestPopup(point) ??
        (ResolveOwnedLayer(slotLayer) == OwnedControlLayer.Popup ? HitTest(point) : null);

    /// <summary>Renders one branch during the elevated pass using its effective owned layer.</summary>
    /// <param name="canvas">The root-relative frame canvas.</param>
    /// <param name="slotLayer">The defined layer declared by the owning slot.</param>
    internal void RenderPopupBranch(TerminalCanvas canvas, OwnedControlLayer slotLayer)
    {
        if (ResolveOwnedLayer(slotLayer) == OwnedControlLayer.Popup)
        {
            Render(canvas);
            RenderOwnedPopupDescendants(canvas);
        }
        else
        {
            RenderPopupLayer(canvas);
        }
    }

    /// <summary>Registers one distinct ordered visual ownership slot.</summary>
    /// <param name="options">The validated structural and traversal metadata.</param>
    /// <param name="capacity">The non-negative maximum control count.</param>
    /// <returns>The newly registered empty slot.</returns>
    internal OwnedControlSlot RegisterOwnedSlot(OwnedControlOptions options, int capacity) =>
        OwnedControls.Register(options, capacity);

    /// <summary>Commits an ownership edge without invoking user callbacks.</summary>
    /// <param name="parent">The committed owner, or null.</param>
    /// <param name="slot">The exact committed slot, or null.</param>
    internal void CommitOwnership(Control? parent, OwnedControlSlot? slot)
    {
        Debug.Assert((parent is null) == (slot is null), "Parent and owning-slot state change together.");
        Debug.Assert(slot is null || ReferenceEquals(slot.Registry.Owner, parent), "The slot belongs to the committed parent.");
        Parent = parent;
        OwningSlot = slot;
        InvalidateSubtreeResolvedStyleCache();
    }

    /// <summary>Publishes one already committed parent transition.</summary>
    /// <param name="previous">The previous owner, or null.</param>
    /// <param name="current">The committed owner, or null.</param>
    internal void PublishParentChanged(Control? previous, Control? current) =>
        OnParentChanged(previous, current);

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

        var snapshot = ArrayPool<IHandler>.Shared.Rent(handlers.Count);
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
            ArrayPool<IHandler>.Shared.Return(snapshot);
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

        InvalidateResolvedStyleCache();
        Invalidate(VisualStateInvalidation());
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFocused)));
        OnFocusChanged(value);
    }

    /// <summary>Publishes a focus-eligibility change after deferred manager cleanup commits.</summary>
    internal void PublishDeferredCanFocusChange()
    {
        Debug.Assert(CanFocusNotificationPending, "Only a deferred eligibility change is published.");
        CanFocusNotificationPending = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanFocus)));
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

        InvalidateResolvedStyleCache();
        Invalidate(VisualStateInvalidation());
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
        InvalidateResolvedStyleCache();
        Invalidate(VisualStateInvalidation());
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
        Invalidate(VisualStateInvalidation());
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
    protected virtual Size MeasureOverride(Constraint constraint)
    {
        Debug.Assert(!IsDisposed, "A disposed control cannot measure content.");
        return default;
    }

    /// <summary>Adjusts the content constraint before content measurement. Default returns it unchanged.</summary>
    /// <param name="content">The border-and-padding-deflated content constraint.</param>
    /// <returns>The constraint passed to <see cref="MeasureOverride"/>.</returns>
    internal virtual Constraint OnMeasuringContent(Constraint content) => content;

    /// <summary>Adjusts the resolved desired size after content measurement. Default returns it unchanged.</summary>
    /// <param name="desired">The border-box desired size.</param>
    /// <returns>The committed desired size.</returns>
    internal virtual Size OnMeasuredDesired(Size desired) => desired;

    /// <summary>Adjusts the border-and-padding-deflated content box before arrangement. Default returns it unchanged.</summary>
    /// <param name="padded">The border-and-padding-deflated content-box rectangle.</param>
    /// <returns>The rectangle passed to <see cref="ArrangeOverride"/>.</returns>
    internal virtual Rect ResolveContentSlot(Rect padded) => padded;

    /// <summary>Arranges overlay chrome inside the border-and-padding-deflated content box. Default is a no-op.</summary>
    /// <param name="padded">The border-and-padding-deflated content-box rectangle.</param>
    internal virtual void ArrangeOverlays(Rect padded) { }

    /// <summary>Gets whether this control sizes its width to content, overriding stretch. Default false.</summary>
    internal virtual bool ShrinkWrapsWidth => false;

    /// <summary>Gets whether this control sizes its height to content, overriding stretch. Default false.</summary>
    internal virtual bool ShrinkWrapsHeight => false;

    /// <summary>Arranges content inside the committed border-and-padding-deflated content box.</summary>
    /// <param name="bounds">The non-negative content-box rectangle.</param>
    protected virtual void ArrangeOverride(Rect bounds) =>
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

    /// <summary>Responds after this control commits attachment to a dispatcher.</summary>
    /// <remarks>The callback observes a non-null <see cref="Dispatcher"/>.</remarks>
    protected virtual void OnAttached() =>
        Debug.Assert(Dispatcher is not null, "Attachment state commits before its callback.");

    /// <summary>Responds after this control commits detachment from its dispatcher.</summary>
    /// <remarks>The callback observes a null <see cref="Dispatcher"/>.</remarks>
    protected virtual void OnDetached() =>
        Debug.Assert(Dispatcher is null, "Detachment state commits before its callback.");

    /// <summary>Releases derived resources before this control's owned state is disposed.</summary>
    /// <remarks>
    /// The hook runs at most once. If it throws, base cleanup still completes
    /// before the original exception is rethrown.
    /// </remarks>
    protected virtual void OnDisposing() =>
        Debug.Assert(!IsDisposed, "The disposing hook runs before disposal commits.");

    /// <summary>Responds after implicit pointer-capture cancellation clears all pointer state.</summary>
    /// <param name="reason">The defined reason capture was cancelled.</param>
    protected virtual void OnPointerCaptureCancelled(ReleaseReason reason) =>
        Debug.Assert(Enum.IsDefined(reason), "Capture cancellation reasons are validated internally.");

    /// <summary>Responds after this control's direct ownership changes.</summary>
    /// <param name="previous">The previous owner, or null.</param>
    /// <param name="current">The committed owner, or null.</param>
    protected virtual void OnParentChanged(Control? previous, Control? current)
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
    protected virtual void OnRender(TerminalCanvas canvas)
    {
        _ = canvas.Bounds;
        Debug.Assert(!IsDisposed, "A disposed control cannot render content.");
        RenderChrome(canvas);
    }

    /// <summary>Draws the shared border, shadow, and body-fill chrome for the current visual state.</summary>
    /// <param name="canvas">The frame-owned canvas clipped to <see cref="VisualBounds"/>.</param>
    /// <remarks>
    /// Derived controls that fully override <see cref="OnRender"/> can call this to draw the
    /// standard chrome consistently with the built-in controls before rendering custom content.
    /// </remarks>
    protected void RenderChrome(TerminalCanvas canvas) =>
        ControlChrome.Render(this, canvas, GetVisualState());

    /// <summary>Gets the own-content drawing bounds, including deliberate visual overflow.</summary>
    /// <remarks>
    /// The default is <see cref="Bounds"/>. Overrides affect own drawing only;
    /// descendant clipping and pointer hit testing continue to use the arranged box.
    /// </remarks>
    protected virtual Rect VisualBounds =>
        ControlChrome.ExpandVisualBounds(Bounds, HasShadow, ShadowOffset);

    /// <summary>Renders owned descendants after this control's content.</summary>
    /// <param name="canvas">The canvas clipped to this control.</param>
    internal virtual void RenderChildren(TerminalCanvas canvas)
    {
        Debug.Assert(!IsDisposed, "A disposed control cannot render children.");
        OwnedControls.RenderNormal(canvas);
    }

    /// <summary>Renders owned child content into the (already clipped) canvas.</summary>
    /// <param name="canvas">The child canvas.</param>
    /// <remarks>The default delegates to <see cref="RenderChildren"/> so leaf controls are unaffected.</remarks>
    internal virtual void RenderContent(TerminalCanvas canvas) => RenderChildren(canvas);

    /// <summary>Gets whether this control is interaction-eligible, optionally requiring point containment.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <param name="requireContainment">Whether the arranged bounds must contain the point.</param>
    /// <returns>True when this control may participate in hit testing.</returns>
    internal bool CanHitTestSelf(Point point, bool requireContainment = true) =>
        !IsDisposed && IsHitTestVisible && EffectiveIsVisible && EffectiveIsEnabled &&
        (!requireContainment || Bounds.Contains(point));

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

        if (IsSelectedState)
        {
            result |= State.Selected;
        }

        if (IsCheckedState)
        {
            result |= State.Checked;
        }

        if (IsIndeterminateState)
        {
            result |= State.Indeterminate;
        }

        return result;
    }

    /// <summary>Gets whether the control currently holds a checked value.</summary>
    /// <remarks>
    /// Overridden by checkable controls (checkbox, radio, menu item) to drive
    /// <see cref="State.Checked"/>.
    /// This is the supported seam for participating in checked styling without overriding
    /// <see cref="GetVisualState"/>.
    /// </remarks>
    protected virtual bool IsCheckedState => false;

    /// <summary>Gets whether the control is the selected member of an owning collection.</summary>
    /// <remarks>
    /// Defaults to inherited collection selection propagated by an owning list; a control with its
    /// own selection concept overrides this to drive <see cref="State.Selected"/>.
    /// </remarks>
    protected virtual bool IsSelectedState => HasSelectedState;

    /// <summary>Gets whether the control holds a mixed or indeterminate value.</summary>
    /// <remarks>
    /// Overridden by tri-state controls to drive <see cref="State.Indeterminate"/>.
    /// </remarks>
    protected virtual bool IsIndeterminateState => false;

    /// <summary>Gets the invalidation a visual-state change requires for this control.</summary>
    /// <remarks>
    /// A change is render-only unless an applicable style contains an arrange- or measure-impact
    /// property, in which case the corresponding layout work also reruns.
    /// </remarks>
    private Invalidation VisualStateInvalidation()
    {
        var impact = MaximumImpact(
            ChangeImpact.Render,
            InstanceStyle?.AggregateImpact ?? ChangeImpact.None);

        if (ThemeContext is { } context)
        {
            foreach (var style in context.GetStyleChain(GetType()))
            {
                impact = MaximumImpact(impact, style.AggregateImpact);
            }
        }

        for (var current = Parent; current is not null; current = current.Parent)
        {
            if (current is not IStyleScope)
            {
                continue;
            }

            if (ThemeContext is { } scopeContext)
            {
                foreach (var style in scopeContext.GetStyleChain(current.GetType()))
                {
                    impact = MaximumImpact(impact, style.AggregateImpact);
                }
            }

            impact = MaximumImpact(
                impact,
                current.InstanceStyle?.AggregateImpact ?? ChangeImpact.None);
        }

        return InvalidationFor(impact);
    }

    /// <summary>Gets the committed content rectangle after border and padding deflation.</summary>
    protected Rect ContentBounds => Padding.Deflate(BorderThickness.Deflate(Bounds));

    /// <summary>Returns the earlier and therefore stronger of two validated change impacts.</summary>
    /// <param name="left">The first validated impact.</param>
    /// <param name="right">The second validated impact.</param>
    /// <returns>The impact with the greatest ordered value.</returns>
    internal static ChangeImpact MaximumImpact(ChangeImpact left, ChangeImpact right) =>
        (int) left >= (int) right ? left : right;

    /// <summary>Maps one validated public change impact to the complete internal dirty-phase closure.</summary>
    /// <param name="impact">The validated earliest affected UI phase.</param>
    /// <returns>The internal dirty phases requested by the change.</returns>
    internal static Invalidation InvalidationFor(ChangeImpact impact) => impact switch
    {
        ChangeImpact.None => Invalidation.None,
        ChangeImpact.Render => Invalidation.Render,
        ChangeImpact.Arrange => Invalidation.Arrange | Invalidation.Render,
        ChangeImpact.Measure => Invalidation.All,
        _ => throw new UnreachableException(),
    };

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
        int inset,
        int intrinsic,
        int minimum,
        int maximum)
    {
        var requested = length.Kind switch
        {
            Kind.Auto => SaturatingAdd(intrinsic, inset),
            Kind.Cells => (int) length.Value,
            Kind.Percent => slot.HasValue
                ? ResolvePercent(slot.Value, length.Value)
                : SaturatingAdd(intrinsic, inset),
            Kind.Star => slot.HasValue
                ? Math.Max(0, slot.Value - margin)
                : SaturatingAdd(intrinsic, inset),
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
        int inset)
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
        return Math.Max(0, Math.Min(border.Value, available) - inset);
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
    /// <param name="impact">The validated earliest affected phase.</param>
    /// <param name="propertyName">The non-empty property name supplied by the compiler.</param>
    /// <returns>Whether a changed value was committed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="impact"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        ChangeImpact impact,
        [CallerMemberName] string? propertyName = null)
    {
        ValidateImpact(impact);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        VerifyMutable();

        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Invalidate(InvalidationFor(impact));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    /// <summary>Raises one derived committed-property notification after atomic field mutation.</summary>
    /// <param name="propertyName">The non-empty public property name.</param>
    /// <param name="impact">The validated earliest phase affected by the committed transaction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="impact"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected void NotifyPropertyChanged(string propertyName, ChangeImpact impact)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        ValidateImpact(impact);
        VerifyMutable();
        Invalidate(InvalidationFor(impact));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void EnsureDirectOwnedChild(Control child)
    {
        Debug.Assert(child is not null, "Direct-child validation requires an instance.");

        if (!ReferenceEquals(child.Parent, this))
        {
            throw new ArgumentException(
                "The control must be a direct child of this owner.",
                nameof(child));
        }
    }

    private static void ValidateImpact(ChangeImpact impact)
    {
        if (!Enum.IsDefined(impact))
        {
            throw new ArgumentOutOfRangeException(
                nameof(impact),
                impact,
                "The change impact is unknown.");
        }
    }

    private void InvalidateDescendants(Invalidation value) =>
        VisitChildren(child =>
        {
            child.Invalidate(value);
            child.InvalidateDescendants(value);
        });

    private void OnInstanceStyleChanged(object? sender, ThemeChangedEventArgs eventArgs)
    {
        var dispatcher = Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Post(() => ApplyInstanceStyleChanged(sender, eventArgs)); return;
        }
        ApplyInstanceStyleChanged(sender, eventArgs);
    }

    private void ApplyInstanceStyleChanged(object? sender, ThemeChangedEventArgs eventArgs)
    {
        if (IsDisposed || !ReferenceEquals(sender, InstanceStyle))
        {
            return;
        }

        InvalidateResolvedStyleCache();

        var invalidation = InvalidationFor(eventArgs.Impact);
        Invalidate(invalidation);
        CascadeStyleScopeInvalidation(invalidation);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
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
        var failure = (ExceptionDispatchInfo?) null;
        CaptureFailure(
            () => focus?.Unavailable(this),
            ref failure);
        CaptureFailure(
            () => capture?.Unavailable(this, reason),
            ref failure);
        CaptureFailure(
            () => OnUnavailable(reason),
            ref failure);

        if (reason == ReleaseReason.Disposed)
        {
            if (focus is not null && ReferenceEquals(focus.Root, this))
            {
                CaptureFailure(focus.RootDisposed, ref failure);
            }

            if (capture is not null && ReferenceEquals(capture.Root, this))
            {
                CaptureFailure(capture.RootDisposed, ref failure);
            }
        }

        failure?.Throw();
    }

    private static void CaptureFailure(
        System.Action action,
        ref ExceptionDispatchInfo? failure)
    {
        Debug.Assert(action is not null, "Cleanup capture requires one action.");

        try
        {
            action();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }

    /// <summary>Invokes the derived capture-cancellation hook after manager state is clear.</summary>
    /// <param name="reason">The defined implicit cancellation reason.</param>
    internal void NotifyPointerCaptureCancelled(ReleaseReason reason)
    {
        Debug.Assert(Enum.IsDefined(reason), "Capture cancellation reasons are validated internally.");
        OnPointerCaptureCancelled(reason);
    }

    private Constraint CreateContentConstraint(Constraint constraint)
    {
        var horizontalInset = SaturatingAdd(Padding.Horizontal, BorderThickness.Horizontal);
        var verticalInset = SaturatingAdd(Padding.Vertical, BorderThickness.Vertical);
        return new Constraint(
            ResolveContentAxis(Width, constraint.Width, Margin.Horizontal, horizontalInset),
            ResolveContentAxis(Height, constraint.Height, Margin.Vertical, verticalInset));
    }

    private Size ResolveDesiredSize(Constraint constraint, Size content)
    {
        var horizontalInset = SaturatingAdd(Padding.Horizontal, BorderThickness.Horizontal);
        var verticalInset = SaturatingAdd(Padding.Vertical, BorderThickness.Vertical);
        return new Size(
            ResolveMeasureAxis(
                Width,
                constraint.Width,
                Margin.Horizontal,
                horizontalInset,
                content.Width,
                MinWidth,
                MaxWidth),
            ResolveMeasureAxis(
                Height,
                constraint.Height,
                Margin.Vertical,
                verticalInset,
                content.Height,
                MinHeight,
                MaxHeight));
    }

    /// <summary>Commits inherited context across this complete subtree without invoking user callbacks.</summary>
    /// <param name="dispatcher">The inherited dispatcher, or null.</param>
    /// <param name="cellPolicy">The non-null inherited Unicode cell policy.</param>
    /// <param name="focusOwner">The inherited focus manager, or null.</param>
    /// <param name="captureOwner">The inherited pointer manager, or null.</param>
    /// <param name="themeContext">The inherited theme context, or null.</param>
    /// <param name="themeChanged">Collects controls whose theme identity changed.</param>
    /// <param name="attached">Collects controls that became attached.</param>
    /// <param name="detached">Collects controls that became detached.</param>
    internal void CommitSubtreeContext(
        Dispatcher? dispatcher,
        Policy cellPolicy,
        FocusManager? focusOwner,
        CaptureManager? captureOwner,
        ThemeContext? themeContext,
        List<Control> themeChanged,
        List<Control> attached,
        List<Control> detached)
    {
        ArgumentNullException.ThrowIfNull(cellPolicy);
        ArgumentNullException.ThrowIfNull(themeChanged);
        ArgumentNullException.ThrowIfNull(attached);
        ArgumentNullException.ThrowIfNull(detached);
        var previous = Dispatcher;

        if (!ReferenceEquals(previous, dispatcher))
        {
            UnsubscribeInstanceStyle(InstanceStyle);
            Dispatcher = dispatcher;
            SubscribeInstanceStyle(InstanceStyle);
        }

        CellPolicy = cellPolicy;
        FocusOwner = focusOwner;
        CaptureOwner = captureOwner;

        if (CommitThemeContext(themeContext))
        {
            themeChanged.Add(this);
        }

        if (previous is null && dispatcher is not null)
        {
            attached.Add(this);
        }
        else if (previous is not null && dispatcher is null)
        {
            detached.Add(this);
        }

        VisitChildren(child => child.CommitSubtreeContext(
            dispatcher,
            cellPolicy,
            focusOwner,
            captureOwner,
            themeContext,
            themeChanged,
            attached,
            detached));
    }

    /// <summary>Publishes this control's committed theme-context change.</summary>
    internal void PublishThemeContextChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    /// <summary>Publishes this control's already committed attachment.</summary>
    internal void PublishAttached() => OnAttached();

    /// <summary>Publishes this control's already committed detachment.</summary>
    internal void PublishDetached() => OnDetached();

    private void CommitAndPublishContext(
        Dispatcher? dispatcher,
        Policy cellPolicy,
        FocusManager? focusOwner,
        CaptureManager? captureOwner,
        ThemeContext? themeContext,
        System.Action? configure)
    {
        OwnedControlRegistry.VerifyMutationAllowed(this);
        var entered = OwnedControlRegistry.EnterPublication(this);
        var themeChanged = new List<Control>();
        var attached = new List<Control>();
        var detached = new List<Control>();
        var failure = (ExceptionDispatchInfo?) null;

        try
        {
            CommitSubtreeContext(
                dispatcher,
                cellPolicy,
                focusOwner,
                captureOwner,
                themeContext,
                themeChanged,
                attached,
                detached);
            configure?.Invoke();
            PublishContextChanges(themeChanged, attached, detached, ref failure);
        }
        finally
        {
            OwnedControlRegistry.ExitPublication(entered);
        }

        failure?.Throw();
    }

    private static void PublishContextChanges(
        List<Control> themeChanged,
        List<Control> attached,
        List<Control> detached,
        ref ExceptionDispatchInfo? failure)
    {
        foreach (var control in themeChanged)
        {
            CaptureFailure(control.PublishThemeContextChanged, ref failure);
        }

        foreach (var control in detached)
        {
            CaptureFailure(control.PublishDetached, ref failure);
        }

        foreach (var control in attached)
        {
            CaptureFailure(control.PublishAttached, ref failure);
        }
    }

    private void SubscribeInstanceStyle(IControlStyle? style)
    {
        if (Dispatcher is not null && style is not null)
        {
            style.Changed += OnInstanceStyleChanged;
        }
    }

    private void UnsubscribeInstanceStyle(IControlStyle? style) => style?.Changed -= OnInstanceStyleChanged;

    private void VerifyAccess() => Dispatcher?.VerifyAccess();

    private void VerifyLifecycleRoot()
    {
        if (Parent is not null || OwningSlot is not null)
        {
            throw new InvalidOperationException(
                "Only an unowned control root can be attached or detached directly.");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);
}
