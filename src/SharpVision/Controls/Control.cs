// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.ComponentModel;
using System.Runtime.ExceptionServices;

using DataBinding;

using SharpVision.Terminal.Input;

/// <summary>
/// Defines a traditional mutable UI element with dispatcher affinity and box layout.
/// </summary>
/// <remarks>
/// Detached controls may be assembled on any thread. Once attached, every
/// mutation and disposal must run on <see cref="Dispatcher"/>.
/// </remarks>
[PublicAPI]
[DebuggerDisplay("{DebuggerSummary,nq}")]
public abstract partial class Control: INotifyPropertyChanged, IDisposable
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerSummary => $"{GetType().Name} [{Bounds.Width}×{Bounds.Height}]" +
        (IsFocused ? " Focused" : "") +
        (Visibility != Visibility.Visible ? $" {Visibility}" : "") +
        (IsDisposed ? " Disposed" : "");

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
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets the requested border-box height.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Length Height
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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
            EnumValidation.ValidateDefined(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Arrange);
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
            EnumValidation.ValidateDefined(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Arrange);
        }
    } = VerticalAlignment.Stretch;

    /// <summary>Gets or sets local layout/render/input participation.</summary>
    /// <remarks>
    /// A change to hidden or collapsed state commits and invalidates first, completes focus and
    /// pointer-capture cleanup, and then raises <see cref="PropertyChanged"/>. Cleanup and property
    /// callbacks both run when either fails, and the earliest failure is rethrown afterward.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Visibility Visibility
    {
        get;
        set
        {
            EnumValidation.ValidateDefined(value);
            var impact = value == Visibility.Collapsed || field == Visibility.Collapsed
                ? InvalidationImpact.Measure
                : InvalidationImpact.Render;
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            var invalidation = InvalidationFor(impact);
            Invalidate(invalidation);
            InvalidateDescendants(invalidation);
            ExceptionDispatchInfo? failure = null;

            if (value != Visibility.Visible)
            {
                ExceptionAggregation.Capture(
                    () => NotifyUnavailable(ReleaseReason.Hidden),
                    ref failure);
            }

            ExceptionAggregation.Capture(
                () => PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(Visibility))),
                ref failure);
            ExceptionAggregation.Capture(
                () => VisibilityChanged?.Invoke(this, EventArgs.Empty),
                ref failure);
            failure?.Throw();
        }
    } = Visibility.Visible;

    /// <summary>Gets or sets whether local behavior accepts input.</summary>
    /// <remarks>
    /// Disabling commits and invalidates first, completes focus and pointer-capture cleanup, and
    /// then raises <see cref="PropertyChanged"/>. Cleanup and property callbacks both run when
    /// either fails, and the earliest failure is rethrown afterward.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool IsEnabled
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
            InvalidateDescendants(Invalidation.Render);
            ExceptionDispatchInfo? failure = null;

            if (!value)
            {
                ExceptionAggregation.Capture(
                    () => NotifyUnavailable(ReleaseReason.Disabled),
                    ref failure);
            }

            ExceptionAggregation.Capture(
                () => PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(IsEnabled))),
                ref failure);
            ExceptionAggregation.Capture(
                () => IsEnabledChanged?.Invoke(this, EventArgs.Empty),
                ref failure);
            failure?.Throw();
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
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    } = true;

    /// <summary>Gets or sets whether this control is configured to accept keyboard focus.</summary>
    /// <remarks>
    /// Setting this property to false releases focus before the property-change
    /// notification. During an active focus callback, both cleanup and notification
    /// complete before the enclosing focus request returns. Pointer capture is unaffected.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool Focusable
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

            if (FocusableNotificationPending)
            {
                return;
            }

            FocusableNotificationPending = true;

            try
            {
                if (!value && FocusOwner?.Ineligible(this) == false)
                {
                    return;
                }

                PublishDeferredFocusableChange();
            }
            catch
            {
                FocusableNotificationPending = false;
                throw;
            }
        }
    }

    /// <summary>Gets whether the control can currently receive keyboard focus.</summary>
    /// <remarks>This effective value includes <see cref="Focusable"/>, visibility, enabled state, and disposal.</remarks>
    public bool CanFocus => Focusable && EffectiveIsVisible && EffectiveIsEnabled && !IsDisposed;

    /// <summary>Gets or sets the deterministic tab-order key.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int TabIndex
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    }

    /// <summary>Gets or sets whether this focusable control participates in Tab traversal.</summary>
    /// <remarks>
    /// Explicit and pointer-originated focus use <see cref="CanFocus"/> only. Changing this value
    /// never releases an already focused control; it affects only future traversal.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool TabStop
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    } = true;

    /// <summary>Gets whether this control currently participates in Tab traversal.</summary>
    public virtual bool IsTabStop => CanFocus && TabStop;

    /// <summary>Gets or sets how Tab traversal treats this control's subtree.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TabNavigation TabNavigation
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The tab navigation mode is unknown.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets an optional debugging or accessibility identifier.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string? Name
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    }

    /// <summary>Gets or sets whether ampersands in this control's caption declare a mnemonic.</summary>
    /// <remarks>
    /// One unescaped ampersand marks the following Unicode scalar and is omitted from rendering;
    /// two ampersands render one literal ampersand. Derived controls choose which caption participates.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool UseMnemonic
    {
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                InvalidateDescendants(Invalidation.Measure);
            }
        }
    } = true;

    /// <summary>Gets the optional caption searched for an ampersand-marked access key.</summary>
    /// <remarks>
    /// Derived controls override this for their user-visible action, header, label, or title text.
    /// The returned string remains owned by the control and is borrowed only during synchronous dispatch.
    /// </remarks>
    protected virtual string? AccessKeyText => null;

    /// <summary>Gets or sets arbitrary user data associated with this control.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public object? Tag
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    }

    private OwnedControlSlot? _contextMenuSlot;

    /// <summary>Gets or sets an optional context menu shown on secondary pointer press.</summary>
    /// <remarks>
    /// A menu's presentation control may belong to only one owner at a time; assigning a menu
    /// already presented by another control is rejected.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The assigned menu's presentation already belongs to another control.
    /// </exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public IContextMenu? ContextMenu
    {
        get;
        set
        {
            VerifyMutable();

            if (ReferenceEquals(field, value))
            {
                return;
            }

            _contextMenuSlot ??= RegisterOwnedSlot(
                new OwnedControlOptions(
                    OwnedControlRole.FrameworkPart,
                    OwnedControlLayer.Popup,
                    participatesInHitTesting: true,
                    participatesInNavigation: true,
                    partKey: "context-menu",
                    InvalidationImpact.None),
                capacity: 1);

            _contextMenuSlot.ReplaceAll(value is null ? [] : [value.Presentation]);
            field = value;
        }
    }

    private readonly ControlInteractionState _interactionState = new();

    /// <summary>Gets whether this control currently owns keyboard focus.</summary>
    public bool IsFocused => _interactionState.IsFocused;

    /// <summary>Raised after this control loses direct keyboard focus.</summary>
    public event EventHandler? LostFocus;

    /// <summary>Raised after this control gains direct keyboard focus.</summary>
    public event EventHandler? GotFocus;

    /// <summary>Raised after keyboard focus enters this control's subtree.</summary>
    public event EventHandler? FocusEntered;

    /// <summary>Raised after keyboard focus leaves this control's subtree.</summary>
    public event EventHandler? FocusLeft;

    /// <summary>Gets whether this control or a descendant owns keyboard focus.</summary>
    public bool ContainsFocus
    {
        get
        {
            for (var focused = FocusOwner?.Focused; focused is not null; focused = focused.Parent)
            {
                if (ReferenceEquals(focused, this))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Gets whether the pointer is over this control or one of its descendants.</summary>
    public bool IsPointerOver => _interactionState.IsPointerOver;

    /// <summary>Gets whether the pointer directly targets this control.</summary>
    public bool IsPointerDirectlyOver => _interactionState.IsPointerDirectlyOver;

    /// <summary>Raised after the physical pointer enters this control's subtree.</summary>
    public event EventHandler? PointerEntered;

    /// <summary>Raised after the physical pointer exits this control's subtree.</summary>
    public event EventHandler? PointerExited;

    /// <summary>Gets whether an active pointer press began on this control.</summary>
    public bool IsPressed => _interactionState.IsPressed;

    /// <summary>Raised after a primary pointer press arrives through the routed event system.</summary>
    public event EventHandler<PointerEventArgs>? PointerPressed;

    /// <summary>Raised after a pointer release arrives through the routed event system.</summary>
    public event EventHandler<PointerEventArgs>? PointerReleased;

    /// <summary>Raised after a pointer move arrives through the routed event system.</summary>
    public event EventHandler<PointerEventArgs>? PointerMoved;

    /// <summary>Raised after a key press or repeat arrives through the routed event system.</summary>
    public event EventHandler<KeyEventArgs>? KeyDown;

    /// <summary>Raised after a key release arrives through the routed event system.</summary>
    public event EventHandler<KeyEventArgs>? KeyUp;

    /// <summary>Raised after this control's direct ownership changes.</summary>
    public event EventHandler? ParentChanged;

    /// <summary>Raised after the committed border box changes during arrange.</summary>
    public event EventHandler? BoundsChanged;

    /// <summary>Raised after the <see cref="IsEnabled"/> property changes.</summary>
    public event EventHandler? IsEnabledChanged;

    /// <summary>Raised after the <see cref="Visibility"/> property changes.</summary>
    public event EventHandler? VisibilityChanged;

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

    /// <summary>Gets whether disposal is currently unwinding this control, before <see cref="IsDisposed"/> flips true.</summary>
    internal bool IsDisposing { get; private set; }

    private bool OwnedDisposalRequested { get; set; }

    private bool FocusableNotificationPending { get; set; }

    private List<IHandler>? Handlers { get; set; }

    /// <summary>Gets this control's central direct-ownership registry.</summary>
    internal OwnedControlRegistry OwnedControls { get; }

    /// <summary>Gets the inherited focus manager while one owns this subtree.</summary>
    internal FocusManager? FocusOwner { get; private set; }

    /// <summary>Gets the inherited capture manager while one owns this subtree.</summary>
    internal PointerManager? CaptureOwner { get; private set; }

    /// <summary>Gets the inherited modality manager while one owns this subtree.</summary>
    internal ModalityManager? ModalityOwner { get; private set; }

    /// <summary>Gets whether this control clips owned descendants to its bounds.</summary>
    /// <remarks>
    /// The framework reads this value while rendering children. Derived controls
    /// may return false only when their documented visual overflow requires the
    /// ancestor clip instead of this control's bounds.
    /// </remarks>
    protected virtual bool ClipsChildren => true;

    /// <summary>Gets the complete terminal style for the resolved appearance.</summary>
    protected internal TerminalStyle ResolvedStyle => GetResolvedStyle(GetAppearanceState());

    /// <summary>Gets the inherited normal-state terminal style for passive visual overflow.</summary>
    protected internal TerminalStyle NormalStyle => GetResolvedStyle(VisualState.Normal);

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
    internal virtual Control? HitTest(Point point)
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
            ModalityOwner,
            InheritedTheme,
            configure: null);
    }

    /// <summary>Attaches a root and descendants with cell and terminal capability context.</summary>
    /// <param name="dispatcher">The non-null owning dispatcher.</param>
    /// <param name="cellPolicy">The non-null inherited Unicode cell policy.</param>
    /// <param name="capabilities">The non-null inherited terminal capability profile.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException">Any descendant is already attached.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher or this control is owned.</exception>
    /// <exception cref="ObjectDisposedException">Any descendant is disposed.</exception>
    internal void Attach(
        Dispatcher dispatcher,
        Policy cellPolicy,
        TerminalCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(cellPolicy);
        ArgumentNullException.ThrowIfNull(capabilities);
        VerifyLifecycleRoot();
        dispatcher.VerifyAccess();
        ValidateAttachment();
        SetCapabilities(capabilities);
        CommitAndPublishContext(
            dispatcher,
            cellPolicy,
            FocusOwner,
            CaptureOwner,
            ModalityOwner,
            InheritedTheme,
            configure: null);
    }

    /// <summary>Stages application-root context and publishes lifecycle only after managers are configured.</summary>
    /// <param name="dispatcher">The non-null owning dispatcher.</param>
    /// <param name="cellPolicy">The non-null inherited Unicode cell policy.</param>
    /// <param name="theme">The non-null initial immutable theme.</param>
    /// <param name="configure">Framework setup that installs focus and capture managers before publication.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException">Any descendant is already attached.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher or this control is owned.</exception>
    /// <exception cref="ObjectDisposedException">Any descendant is disposed.</exception>
    internal void Attach(
        Dispatcher dispatcher,
        Policy cellPolicy,
        Theme theme,
        System.Action configure)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(cellPolicy);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(configure);
        VerifyLifecycleRoot();
        dispatcher.VerifyAccess();
        ValidateAttachment();
        CommitAndPublishContext(
            dispatcher,
            cellPolicy,
            FocusOwner,
            CaptureOwner,
            ModalityOwner,
            theme,
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
        ExceptionDispatchInfo? failure = null;

        try
        {
            ExceptionAggregation.Capture(() => NotifyUnavailable(ReleaseReason.Detached), ref failure);
            var previousAppearance = AppearanceSnapshot.CaptureSubtree(this);
            var plan = ContextTransitionPlan.Create(
                this,
                null,
                Policy.Default,
                null,
                null,
                null,
                null,
                previousAppearance,
                AppearanceSnapshot.ResolveParentAmbient(Parent),
                propagateContext: true);
            plan.Commit();
            var appearanceChanges = AppearanceChange.CreateChanges(
                plan.ThemeTransitions,
                previousAppearance,
                plan.CurrentAppearance);
            PublishContextChanges(appearanceChanges, plan.Attached, plan.Detached, ref failure);
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
            var previousBounds = Bounds;

            Bounds = bounds;
            LastArrangeSlot = slot;
            LastWidthResolved = widthResolved;
            LastHeightResolved = heightResolved;
            var content = Padding.Deflate(BorderInset.Deflate(bounds));
            ArrangeOverride(ResolveContentSlot(content));
            ArrangeOverlays(content);

            if (ContextMenu?.Presentation is { } contextMenuPresentation)
            {
                _ = MeasureChild(contextMenuPresentation, new Constraint(null, null));
                ArrangeChild(contextMenuPresentation, RootBounds(bounds), ResolvedAxes.Both);
            }

            if (bounds != previousBounds)
            {
                BoundsChanged?.Invoke(this, EventArgs.Empty);
            }
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
        RenderCore(canvas, canvas.Bounds);
    }

    /// <summary>Renders one normal-layer branch through a hard canvas and inherited soft content clip.</summary>
    /// <param name="canvas">The nearest frame, viewport, or explicit hard clip.</param>
    /// <param name="contentClip">The accumulated ordinary-layout aperture.</param>
    internal void Render(TerminalCanvas canvas, Rect contentClip)
    {
        VerifyMutable();
        _ = canvas.Bounds;
        RenderCore(canvas, contentClip);
    }

    private void RenderCore(TerminalCanvas canvas, Rect contentClip)
    {

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

            // Each branch expands only its own soft aperture for deliberate
            // visual overflow. Siblings never borrow that expansion, while a
            // finite frame, viewport, or explicit clip remains authoritative.
            var visualBounds = VisualBounds;
            var visual = canvas.Clip(
                ControlChrome.ResolveVisualClip(contentClip, Bounds, visualBounds, canvas.Bounds));
            var appearanceState = GetAppearanceState();
            var chrome = GetChromeRenderOptions();
            ControlChrome.RenderUnderlay(this, visual, appearanceState, chrome);
            OnRenderContent(visual);
            var descendantBounds = DescendantRenderBounds;
            var descendantClip = ClipsChildren
                ? contentClip.Intersect(descendantBounds)
                : contentClip;
            var descendantCanvas = ClipsDescendantVisualOverflow
                ? canvas.Clip(descendantBounds)
                : canvas;
            RenderChildren(descendantCanvas, descendantClip);
            OnRenderAdornment(visual);
            ControlChrome.RenderBorder(this, visual, appearanceState, chrome);
            RenderOverlay(visual);

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

        // A parent may remeasure a child while resolving its final arrangement,
        // as Grid does for finite tracks. The parent will arrange that child in
        // the same transaction, so propagating the child's resulting arrange
        // request would schedule an identical ancestor layout forever.
        if (value == Invalidation.Arrange && Parent is { IsArranging: true })
        {
            return;
        }

        Parent?.Invalidate(value);
    }

    /// <summary>Requests the earliest UI phase affected by derived control state.</summary>
    /// <param name="impact">The validated earliest affected phase.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="impact"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected void Invalidate(InvalidationImpact impact)
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
        InvalidateVisualStateCore();
    }

    /// <summary>Requests keyboard focus from the manager inherited by this control.</summary>
    /// <returns>True when focus is acquired or already owned; false when detached or ineligible.</returns>
    /// <exception cref="InvalidOperationException">
    /// The attached control is accessed off-dispatcher or focus is reentered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool Focus()
    {
        VerifyMutable();
        return FocusOwner?.Focus(this) ?? false;
    }

    /// <summary>Handles one matched application access key.</summary>
    /// <param name="key">The invariant-matched Unicode scalar declared by this control's caption.</param>
    /// <returns>True when the control accepted the access-key action.</returns>
    /// <remarks>
    /// The default focuses this control, its first eligible descendant, or the next tab stop for a
    /// label-like leaf. Action controls override this method and reuse their ordinary keyboard path.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected virtual bool OnAccessKey(Rune key)
    {
        _ = key;
        return FocusAccessKeyTarget();
    }

    /// <summary>Focuses the semantic target associated with this caption.</summary>
    /// <returns>True when this control, a descendant, or the next tab stop accepts focus.</returns>
    protected bool FocusAccessKeyTarget()
    {
        VerifyMutable();

        return FocusOwner is { } focus &&
               (CanFocus
                   ? focus.Focus(this, FocusReason.Keyboard, cancellable: true)
                   : OwnedControlCount > 0
                       ? focus.FocusFirst(this)
                       : focus.MoveNext(this));
    }

    /// <summary>Requests keyboard focus for derived controls that need a named protected seam.</summary>
    protected bool RequestFocus() => Focus();

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
    public bool HasPointerCapture => ReferenceEquals(CaptureOwner?.Captured, this);

    /// <summary>Raised after this control loses exclusive pointer capture.</summary>
    public event EventHandler<PointerCaptureLostEventArgs>? LostPointerCapture;

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
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(DisposeBindings, ref failure);
        ExceptionAggregation.Capture(OnDisposing, ref failure);

        try
        {
            ExceptionAggregation.Capture(
                () => NotifyUnavailable(ReleaseReason.Disposed),
                ref failure);

            if (OwningSlot is { } slot)
            {
                ExceptionAggregation.Capture(
                    () => slot.RemoveForDisposalWithinPublication(this),
                    ref failure);
            }

            ExceptionAggregation.Capture(OwnedControls.DisposeAll, ref failure);
            ExceptionAggregation.Capture(ClearHandlers, ref failure);
        }
        finally
        {
            Dispatcher = null;
            Pending = Invalidation.None;
            IsDisposed = true;
            IsDisposing = false;
            PropertyChanged = null;
            GotFocus = null;
            LostFocus = null;
            FocusEntered = null;
            FocusLeft = null;
            PointerEntered = null;
            PointerExited = null;
            LostPointerCapture = null;
            PointerPressed = null;
            PointerReleased = null;
            PointerMoved = null;
            KeyDown = null;
            KeyUp = null;
            ParentChanged = null;
            BoundsChanged = null;
            IsEnabledChanged = null;
            VisibilityChanged = null;
        }

        ExceptionAggregation.Capture(OnDisposed, ref failure);
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

    /// <summary>Returns whether this control or any owned descendant currently holds keyboard focus.</summary>
    /// <param name="control">The root of the subtree to search.</param>
    /// <returns><see langword="true"/> when <paramref name="control"/> or a descendant is focused.</returns>
    internal static bool ContainsFocused(Control control)
    {
        if (control.IsFocused)
        {
            return true;
        }

        for (var index = 0; index < control.OwnedControlCount; index++)
        {
            if (ContainsFocused(control.OwnedControlAt(index)))
            {
                return true;
            }
        }

        return false;
    }

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

    /// <summary>Finds one previously registered stable owned part by its non-empty key.</summary>
    /// <param name="partKey">The non-empty stable part key.</param>
    /// <returns>The registered slot, or null when the key is not registered.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="partKey"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="partKey"/> is empty or whitespace.</exception>
    internal OwnedControlSlot? FindOwnedSlot(string partKey) => OwnedControls.FindPart(partKey);

    /// <summary>Commits an ownership edge without invoking user callbacks.</summary>
    /// <param name="parent">The committed owner, or null.</param>
    /// <param name="slot">The exact committed slot, or null.</param>
    internal void CommitOwnership(Control? parent, OwnedControlSlot? slot)
    {
        Debug.Assert((parent is null) == (slot is null), "Parent and owning-slot state change together.");
        Debug.Assert(slot is null || ReferenceEquals(slot.Registry.Owner, parent),
            "The slot belongs to the committed parent.");
        Parent = parent;
        OwningSlot = slot;
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
            eventArgs is PointerEventArgs { Pointer: { Action: PointerAction.Press, Cells: { } cells } pointer } &&
            pointer.Buttons.HasFlag(Buttons.Secondary) &&
            ContextMenu is { } contextMenu)
        {
            contextMenu.Show(cells.Y, cells.X);
            eventArgs.Handled = true;
        }

        if (!eventArgs.Handled &&
            eventArgs is KeyEventArgs
            {
                Stroke:
                {
                    Code: Code.Tab,
                    Action: KeyAction.Press,
                    Modifiers: var modifiers
                }
            } &&
            (modifiers & ~Modifiers.Shift) == 0)
        {
            eventArgs.Handled = true;
            eventArgs.RequestPostRouteCommand(
                (modifiers & Modifiers.Shift) != 0 ? PostRouteCommand.TabPrevious : PostRouteCommand.TabNext);
        }
    }

    /// <summary>Returns whether this available control declares the supplied access key.</summary>
    /// <param name="key">The decoded Alt-modified character scalar.</param>
    /// <returns>True when the current caption's first mnemonic matches invariantly.</returns>
    internal bool MatchesAccessKey(Rune key)
    {
        return UseMnemonic &&
               !IsDisposed &&
               Dispatcher is not null &&
               EffectiveIsVisible &&
               EffectiveIsEnabled &&
               AccessKeyText is { } text &&
               SharpVision.Input.AccessKeyText.TryGetKey(text, out var declared) &&
               Rune.ToUpperInvariant(declared) == Rune.ToUpperInvariant(key);
    }

    /// <summary>Invokes the protected access-key seam after manager eligibility validation.</summary>
    /// <param name="key">The matched Unicode scalar.</param>
    /// <returns>True when the control accepts the semantic action.</returns>
    internal bool InvokeAccessKey(Rune key)
    {
        VerifyMutable();
        return MatchesAccessKey(key) && OnAccessKey(key);
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
    internal void SetCaptureOwner(PointerManager? value)
    {
        CaptureOwner = value;
        VisitChildren(child => child.SetCaptureOwner(value));
    }

    /// <summary>Assigns inherited modality-manager ownership recursively.</summary>
    internal void SetModalityOwner(ModalityManager? value)
    {
        ModalityOwner = value;
        VisitChildren(child => child.SetModalityOwner(value));
    }

    /// <summary>Updates focus visual state on the owning dispatcher.</summary>
    internal void SetFocused(bool value)
    {
        VerifyMutable();

        if (!_interactionState.SetFocused(value))
        {
            return;
        }

        InvalidateVisualStateCore();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFocused)));
        OnFocusChanged(value);
    }

    /// <summary>Repairs the manager-owned focus fact without publishing user callbacks.</summary>
    /// <param name="value">Whether this control must own direct focus after repair.</param>
    /// <remarks>
    /// FocusManager uses this only after an ordinary focus transaction callback failed.
    /// The interaction fact commits even when the control is being detached or disposed.
    /// </remarks>
    internal void CommitFocusFact(bool value)
    {
        if (!_interactionState.SetFocused(value) || IsDisposed || Dispatcher is null)
        {
            return;
        }

        InvalidateVisualStateCore();
    }

    /// <summary>Publishes one already-committed direct focus loss.</summary>
    internal void PublishLostFocus() => LostFocus?.Invoke(this, EventArgs.Empty);

    /// <summary>Publishes one already-committed direct focus gain.</summary>
    internal void PublishGotFocus() => GotFocus?.Invoke(this, EventArgs.Empty);

    /// <summary>Publishes one already-committed focus-within entry.</summary>
    internal void PublishFocusEntered() => FocusEntered?.Invoke(this, EventArgs.Empty);

    /// <summary>Publishes one already-committed focus-within exit.</summary>
    internal void PublishFocusLeft() => FocusLeft?.Invoke(this, EventArgs.Empty);

    /// <summary>Publishes a focus-eligibility change after deferred manager cleanup commits.</summary>
    internal void PublishDeferredFocusableChange()
    {
        Debug.Assert(FocusableNotificationPending, "Only a deferred eligibility change is published.");
        FocusableNotificationPending = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Focusable)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanFocus)));
    }

    /// <summary>Updates hover visual state on the owning dispatcher.</summary>
    internal void SetPointerOver(bool value, bool directlyOver)
    {
        VerifyMutable();

        if (!_interactionState.SetPointerOver(value, directlyOver, out var wasOver))
        {
            return;
        }

        InvalidateVisualStateCore();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPointerOver)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPointerDirectlyOver)));

        if (value && !wasOver)
        {
            PointerEntered?.Invoke(this, EventArgs.Empty);
        }
        else if (!value && wasOver)
        {
            PointerExited?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Updates pressed visual state on the owning dispatcher.</summary>
    internal void SetPressed(bool value)
    {
        VerifyMutable();

        if (!_interactionState.SetPressed(value))
        {
            return;
        }

        InvalidateVisualStateCore();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPressed)));
        OnPressedChanged(value);
    }

    /// <summary>Propagates semantic selected visual state through one realized item subtree.</summary>
    /// <param name="value">Whether the subtree is selected.</param>
    internal void SetSelectedState(bool value)
    {
        VerifyMutable();

        if (_interactionState.SetSelected(value))
        {
            InvalidateVisualStateCore();
        }

        VisitChildren(child => child.SetSelectedState(value));
    }

    /// <summary>Propagates collection-current visual state through one realized item subtree.</summary>
    internal void SetCurrentState(bool value)
    {
        VerifyMutable();

        if (!_interactionState.SetCurrent(value))
        {
            return;
        }

        InvalidateVisualState();
    }

    /// <summary>Validates that the complete subtree may receive a dispatcher.</summary>
    internal virtual void ValidateAttachment()
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
    internal virtual void ArrangeOverlays(Rect padded)
    {
    }

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
    protected virtual void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        switch (eventArgs)
        {
            case PointerEventArgs pointer:
                switch (pointer.Pointer.Action)
                {
                    case PointerAction.Press:
                        PointerPressed?.Invoke(this, pointer);
                        break;
                    case PointerAction.Release:
                        PointerReleased?.Invoke(this, pointer);
                        break;
                    case PointerAction.Move:
                        PointerMoved?.Invoke(this, pointer);
                        break;
                    case PointerAction.Wheel:
                    case PointerAction.Leave:
                    default:
                        break;
                }

                break;
            case KeyEventArgs key:
                switch (key.Stroke.Action)
                {
                    case KeyAction.Press:
                    case KeyAction.Repeat:
                        KeyDown?.Invoke(this, key);
                        break;
                    case KeyAction.Release:
                        KeyUp?.Invoke(this, key);
                        break;
                    default:
                        break;
                }

                break;
            default:
                break;
        }
    }

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

    /// <summary>Releases derived completion state after disposal and ownership removal have committed.</summary>
    /// <remarks>
    /// The hook runs at most once with <see cref="IsDisposed"/> true, a null parent, and no dispatcher.
    /// Failures join the ordered disposal aggregation without undoing committed cleanup.
    /// </remarks>
    protected virtual void OnDisposed()
    {
        Debug.Assert(IsDisposed, "The disposed hook runs after disposal commits.");
        Debug.Assert(Parent is null, "A disposed control has no retained parent.");
        Debug.Assert(Dispatcher is null, "A disposed control has no dispatcher.");
    }

    /// <summary>Responds after this control loses pointer capture or its associated press transaction.</summary>
    /// <param name="reason">The defined reason exclusive pointer ownership ended.</param>
    protected virtual void OnLostPointerCapture(PointerCaptureLossReason reason) =>
        Debug.Assert(Enum.IsDefined(reason), "Capture-loss reasons are validated internally.");

    /// <summary>Responds after this control's direct ownership changes.</summary>
    /// <param name="previous">The previous owner, or null.</param>
    /// <param name="current">The committed owner, or null.</param>
    protected virtual void OnParentChanged(Control? previous, Control? current)
    {
        _ = previous;
        _ = current;
        Debug.Assert(!IsDisposed, "A disposed control cannot change parent.");
        ParentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Releases derived transient state when this control becomes unavailable.</summary>
    /// <param name="reason">The precise unavailability reason.</param>
    protected virtual void OnUnavailable(ReleaseReason reason) =>
        Debug.Assert(Enum.IsDefined(reason), "Unavailable reasons are validated internally.");

    /// <summary>Configures the framework-owned chrome surrounding this control's content.</summary>
    /// <returns>The narrow set of chrome adjustments required by a specialized frame.</returns>
    protected virtual ChromeRenderOptions GetChromeRenderOptions() => default;

    /// <summary>Draws this control's own content into its clipped visual bounds.</summary>
    /// <param name="canvas">The frame-owned canvas clipped to <see cref="VisualBounds"/>.</param>
    protected virtual void OnRenderContent(TerminalCanvas canvas)
    {
        _ = canvas.Bounds;
        Debug.Assert(!IsDisposed, "A disposed control cannot render content.");
    }

    /// <summary>Draws this control's own content after normal-layer descendants render, and before
    /// the framework border and any internal overlay chrome.</summary>
    /// <remarks>
    /// This is the seam for content that must paint over a control's own subtree - gridlines above
    /// cells, a focus ring around an active cell, a splitter grip, a drag adorner - the counterpart
    /// to <see cref="OnRenderContent"/>, which always runs beneath descendants. The framework border
    /// and any internal <c>RenderOverlay</c> chrome still paint after this, so an adornment cannot
    /// cover specialized frame chrome a control family owns.
    /// </remarks>
    /// <param name="canvas">The frame-owned canvas clipped to <see cref="VisualBounds"/>.</param>
    protected virtual void OnRenderAdornment(TerminalCanvas canvas)
    {
        _ = canvas.Bounds;
        Debug.Assert(!IsDisposed, "A disposed control cannot render an adornment.");
    }

    /// <summary>Gets the own-content drawing bounds, including deliberate visual overflow.</summary>
    /// <remarks>
    /// The default is <see cref="Bounds"/>. Overrides affect own drawing only;
    /// descendant clipping and pointer hit testing continue to use the arranged box.
    /// </remarks>
    protected virtual Rect VisualBounds
    {
        get
        {
            var shadow = ActualShadow;
            return ControlChrome.ExpandVisualBounds(Bounds, shadow.IsVisible, shadow.Mode, shadow.Offset);
        }
    }

    /// <summary>Gets whether this control forms a hard clip for descendant visual overflow.</summary>
    /// <remarks>Own visual overflow remains eligible for propagation through the control's parent.</remarks>
    internal virtual bool ClipsDescendantVisualOverflow => false;

    /// <summary>Gets the soft layout aperture applied to normal-layer descendants.</summary>
    /// <remarks>The default is the arranged border box. Specialized translated faces may override it.</remarks>
    internal virtual Rect DescendantRenderBounds => Bounds;

    /// <summary>Renders owned descendants after this control's content.</summary>
    /// <param name="canvas">The nearest hard descendant clip.</param>
    /// <param name="contentClip">The inherited soft content clip.</param>
    internal virtual void RenderChildren(TerminalCanvas canvas, Rect contentClip)
    {
        Debug.Assert(!IsDisposed, "A disposed control cannot render children.");
        OwnedControls.RenderNormal(canvas, contentClip);
    }

    /// <summary>Renders owned child content through the current branch clips.</summary>
    /// <param name="canvas">The nearest hard descendant clip.</param>
    /// <param name="contentClip">The inherited soft content clip.</param>
    /// <remarks>The default delegates to <see cref="RenderChildren"/> so leaf controls are unaffected.</remarks>
    internal virtual void RenderContent(TerminalCanvas canvas, Rect contentClip) =>
        RenderChildren(canvas, contentClip);

    /// <summary>Draws specialized frame chrome after normal-layer descendants.</summary>
    /// <param name="canvas">The canvas clipped to this control's resolved visual bounds.</param>
    internal virtual void RenderOverlay(TerminalCanvas canvas)
    {
        _ = canvas.Bounds;
        Debug.Assert(!IsDisposed, "A disposed control cannot render overlay chrome.");
    }

    /// <summary>Gets whether this control is interaction-eligible, optionally requiring point containment.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <param name="requireContainment">Whether the arranged bounds must contain the point.</param>
    /// <returns>True when this control may participate in hit testing.</returns>
    internal bool CanHitTestSelf(Point point, bool requireContainment = true) =>
        !IsDisposed && IsHitTestVisible && EffectiveIsVisible && EffectiveIsEnabled &&
        (!requireContainment || Bounds.Contains(point));

    /// <summary>Gets the local state used by the direct appearance model.</summary>
    /// <remarks>Built-in composites may scope ancestry state to the semantic part that owns its appearance.</remarks>
    internal virtual VisualState GetAppearanceState()
    {
        var result = VisualState.Normal;

        if (IsPointerOver)
        {
            result |= VisualState.PointerOver;
        }

        if (ContainsFocus)
        {
            result |= VisualState.FocusWithin;
        }

        if (IsFocused)
        {
            result |= VisualState.Focused;
        }

        if (IsPressed)
        {
            result |= VisualState.Pressed;
        }

        if (!EffectiveIsEnabled)
        {
            result |= VisualState.Disabled;
        }

        if (IsSelectedState)
        {
            result |= VisualState.Selected;
        }

        if (IsCurrentState)
        {
            result |= VisualState.Current;
        }

        if (IsCheckedState)
        {
            result |= VisualState.Checked;
        }

        if (IsIndeterminateState)
        {
            result |= VisualState.Indeterminate;
        }

        return result;
    }

    /// <summary>Gets whether the control currently holds a checked value.</summary>
    /// <remarks>
    /// Overridden by checkable controls (checkbox, radio, menu item) to drive
    /// <see cref="VisualState.Checked"/>.
    /// This is the supported seam for participating in checked styling without overriding
    /// <see cref="GetAppearanceState"/>.
    /// </remarks>
    protected virtual bool IsCheckedState => false;

    /// <summary>Gets whether the control is the selected member of an owning collection.</summary>
    /// <remarks>
    /// Defaults to inherited collection selection propagated by an owning list; a control with its
    /// own selection concept overrides this to drive <see cref="VisualState.Selected"/>.
    /// </remarks>
    protected virtual bool IsSelectedState => _interactionState.IsSelected;

    /// <summary>Gets whether this control is the current member of an owning navigator.</summary>
    protected virtual bool IsCurrentState => _interactionState.IsCurrent;

    /// <summary>Gets whether the control holds a mixed or indeterminate value.</summary>
    /// <remarks>
    /// Overridden by tri-state controls to drive <see cref="VisualState.Indeterminate"/>.
    /// </remarks>
    protected virtual bool IsIndeterminateState => false;

    /// <summary>Gets the invalidation a visual-state change requires for this control.</summary>
    /// <remarks>
    /// A change is render-only unless an applicable style contains an arrange- or measure-impact
    /// property, in which case the corresponding layout work also reruns.
    /// </remarks>
    private Invalidation VisualStateInvalidation()
    {
        if (AppearanceProfile.StateCanChangeChromeGeometry)
        {
            return Invalidation.Measure;
        }

        foreach (var appearance in _appearanceSets.Values)
        {
            if (appearance.Border?.Sides.HasValue == true)
            {
                return Invalidation.Measure;
            }
        }

        return Invalidation.Render;
    }

    /// <summary>Gets the committed content area after border and padding deflation.</summary>
    public Rect ContentBounds => Padding.Deflate(BorderInset.Deflate(Bounds));

    private protected virtual Thickness BorderInset => new(
        (ActualBorder.Sides & BorderSide.Left) != 0 ? 1 : 0,
        (ActualBorder.Sides & BorderSide.Top) != 0 ? 1 : 0,
        (ActualBorder.Sides & BorderSide.Right) != 0 ? 1 : 0,
        (ActualBorder.Sides & BorderSide.Bottom) != 0 ? 1 : 0);

    /// <summary>Gets the committed bounds relative to the parent's content area.</summary>
    public Rect LocalBounds
    {
        get
        {
            if (Parent is not { } parent)
            {
                return Bounds;
            }

            var origin = parent.ContentBounds;
            return new Rect(Bounds.X - origin.X, Bounds.Y - origin.Y, Bounds.Width, Bounds.Height);
        }
    }

    /// <summary>Returns the earlier and therefore stronger of two validated change impacts.</summary>
    /// <param name="left">The first validated impact.</param>
    /// <param name="right">The second validated impact.</param>
    /// <returns>The impact with the greatest ordered value.</returns>
    internal static InvalidationImpact MaximumImpact(InvalidationImpact left, InvalidationImpact right) =>
        (int) left >= (int) right ? left : right;

    /// <summary>Maps one validated public change impact to the complete internal dirty-phase closure.</summary>
    /// <param name="impact">The validated earliest affected UI phase.</param>
    /// <returns>The internal dirty phases requested by the change.</returns>
    internal static Invalidation InvalidationFor(InvalidationImpact impact) => impact switch
    {
        InvalidationImpact.None => Invalidation.None,
        InvalidationImpact.Render => Invalidation.Render,
        InvalidationImpact.Arrange => Invalidation.Arrange | Invalidation.Render,
        InvalidationImpact.Measure => Invalidation.All,
        _ => throw new UnreachableException()
    };

    private static Invalidation Expand(Invalidation value) => value switch
    {
        Invalidation.None => Invalidation.None,
        Invalidation.Render => Invalidation.Render,
        Invalidation.Arrange => Invalidation.Arrange | Invalidation.Render,
        Invalidation.Measure => Invalidation.All,
        Invalidation.All => Invalidation.All,
        _ => value & Invalidation.All
    };

    private static int Align(
        int origin,
        int available,
        int desired,
        HorizontalAlignment alignment) => alignment switch
        {
            HorizontalAlignment.Left or HorizontalAlignment.Stretch => origin,
            HorizontalAlignment.Center => LayoutMath.SaturatingAdd(origin, (available - desired) / 2),
            HorizontalAlignment.Right => LayoutMath.SaturatingAdd(origin, available - desired),
            _ => throw new UnreachableException()
        };

    private static int Align(
        int origin,
        int available,
        int desired,
        VerticalAlignment alignment) => alignment switch
        {
            VerticalAlignment.Top or VerticalAlignment.Stretch => origin,
            VerticalAlignment.Center => LayoutMath.SaturatingAdd(origin, (available - desired) / 2),
            VerticalAlignment.Bottom => LayoutMath.SaturatingAdd(origin, available - desired),
            _ => throw new UnreachableException()
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
            LengthKind.Auto when stretch => available,
            LengthKind.Auto => desired,
            LengthKind.Cells => (int) length.Value,
            LengthKind.Percent => ResolvePercent(slot, length.Value),
            LengthKind.Star => available,
            _ => throw new UnreachableException()
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
            LengthKind.Auto => LayoutMath.SaturatingAdd(intrinsic, inset),
            LengthKind.Cells => (int) length.Value,
            LengthKind.Percent => slot.HasValue
                ? ResolvePercent(slot.Value, length.Value)
                : LayoutMath.SaturatingAdd(intrinsic, inset),
            LengthKind.Star => slot.HasValue
                ? Math.Max(0, slot.Value - margin)
                : LayoutMath.SaturatingAdd(intrinsic, inset),
            _ => throw new UnreachableException()
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
            LengthKind.Auto => slot.HasValue ? Math.Max(0, slot.Value - margin) : null,
            LengthKind.Cells => (int) length.Value,
            LengthKind.Percent => slot.HasValue ? ResolvePercent(slot.Value, length.Value) : null,
            LengthKind.Star => slot.HasValue ? Math.Max(0, slot.Value - margin) : null,
            _ => throw new UnreachableException()
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


    /// <summary>Walks the parent chain and returns the first ancestor of the given type, or null.</summary>
    protected T? FindAncestor<T>() where T : Control
    {
        for (var current = Parent; current is not null; current = current.Parent)
        {
            if (current is T result)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>Sets a nullable glyph field with validation and notification.</summary>
    protected void SetOptionalGlyph(ref Rune? storage, Rune value, string propertyName)
    {
        _ = new ControlGlyph(value, value);
        VerifyMutable();

        if (storage == value)
        {
            return;
        }

        storage = value;
        NotifyPropertyChanged(propertyName, InvalidationImpact.Render);
    }

    /// <summary>Resets a nullable glyph field to null with notification.</summary>
    /// <returns>True when the field was non-null and has been cleared.</returns>
    protected bool ResetOptionalGlyph(ref Rune? storage, string propertyName)
    {
        if (!storage.HasValue)
        {
            return false;
        }

        storage = null;
        NotifyPropertyChanged(propertyName, InvalidationImpact.Render);
        return true;
    }

    /// <summary>Walks the parent chain to the root and returns its bounds, falling back to the last measure constraint.</summary>
    protected Rect RootBounds(Rect fallback)
    {
        var root = this;

        while (root.Parent is { } parent)
        {
            root = parent;
        }

        if (!ReferenceEquals(root, this) && root.Bounds.Width != 0 && root.Bounds.Height != 0)
        {
            return root.Bounds;
        }

        var viewport = LastMeasureConstraint;
        return new Rect(
            fallback.X,
            fallback.Y,
            viewport?.Width ?? fallback.Width,
            viewport?.Height ?? fallback.Height);
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
        InvalidationImpact impact,
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

    /// <summary>Commits one nullable local immutable control style and publishes its resolved effects.</summary>
    /// <typeparam name="TStyle">The complete immutable style value.</typeparam>
    /// <param name="field">The nullable local style storage.</param>
    /// <param name="value">The proposed nullable local style.</param>
    /// <param name="resolve">Resolves the complete style from explicit local and inherited values.</param>
    /// <param name="compareStyle">Compares non-appearance structural and visual members of two styles.</param>
    /// <param name="appearance">Selects the complete appearance profile owned by a style.</param>
    /// <param name="propertyName">The public nullable local style property name.</param>
    /// <param name="actualPropertyName">The public complete resolved style property name.</param>
    /// <returns>Whether a distinct local style value was committed.</returns>
    /// <exception cref="ArgumentNullException">A delegate or property name is null.</exception>
    /// <exception cref="ArgumentException">A property name is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The style comparer returns an unknown impact.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected bool SetControlStyle<TStyle>(
        ref TStyle? field,
        TStyle? value,
        Func<TStyle?, Theme?, TStyle> resolve,
        Func<TStyle, TStyle, InvalidationImpact> compareStyle,
        Func<TStyle, ThemeProfile> appearance,
        string propertyName,
        string actualPropertyName)
        where TStyle : struct, IEquatable<TStyle>
    {
        ArgumentNullException.ThrowIfNull(resolve);
        ArgumentNullException.ThrowIfNull(compareStyle);
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        ArgumentException.ThrowIfNullOrEmpty(actualPropertyName);
        VerifyMutable();

        if (EqualityComparer<TStyle?>.Default.Equals(field, value))
        {
            return false;
        }

        var parentAmbientFace = AppearanceSnapshot.ResolveParentAmbient(Parent);
        var appearanceState = GetAppearanceState();
        var previousStyle = resolve(field, Theme);
        var currentStyle = resolve(value, Theme);
        var previousProfile = appearance(previousStyle);
        var currentProfile = appearance(currentStyle);
        var styleImpact = compareStyle(previousStyle, currentStyle);
        ValidateImpact(styleImpact);
        var appearanceImpact = AppearanceResolver.GetImpact(
            this,
            Theme,
            previousProfile,
            Theme,
            currentProfile,
            parentAmbientFace,
            parentAmbientFace);
        var impact = MaximumImpact(styleImpact, appearanceImpact);
        var invalidation = InvalidationFor(impact);
        var resolvedStyleChanged = !previousStyle.Equals(currentStyle);
        var previousAppearance = AppearanceResolver.ResolveSnapshot(
            this,
            appearanceState,
            Theme,
            previousProfile,
            parentAmbientFace);
        var currentAppearance = AppearanceResolver.ResolveSnapshot(
            this,
            appearanceState,
            Theme,
            currentProfile,
            parentAmbientFace);

        field = value;
        InvalidateSubtreeResolvedStyleCache();
        Invalidate(invalidation);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        if (!resolvedStyleChanged)
        {
            return true;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(actualPropertyName));
        PublishAppearanceChanges(previousAppearance, currentAppearance);
        return true;
    }

    /// <summary>Calculates one complete style's exact impact across a prospective Theme replacement.</summary>
    /// <typeparam name="TStyle">The complete immutable style value.</typeparam>
    /// <param name="previousTheme">The currently inherited Theme, or null.</param>
    /// <param name="currentTheme">The prospective inherited Theme, or null.</param>
    /// <param name="localStyle">The explicit local style, or null for Theme ownership.</param>
    /// <param name="resolve">Resolves the complete style from explicit local and inherited values.</param>
    /// <param name="compareStyle">Compares non-appearance structural and visual members of two styles.</param>
    /// <param name="appearance">Selects the complete appearance profile owned by a style.</param>
    /// <param name="previousParentAmbientFace">The explicit parent ambient face before Theme replacement.</param>
    /// <param name="currentParentAmbientFace">The explicit parent ambient face after Theme replacement.</param>
    /// <returns>The strongest exact invalidation impact.</returns>
    /// <exception cref="ArgumentNullException">A delegate or resolved appearance profile is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The style comparer returns an unknown impact.</exception>
    protected InvalidationImpact GetControlStyleThemeImpact<TStyle>(
        TStyle? localStyle,
        Theme? previousTheme,
        Theme? currentTheme,
        Func<TStyle?, Theme?, TStyle> resolve,
        Func<TStyle, TStyle, InvalidationImpact> compareStyle,
        Func<TStyle, ThemeProfile> appearance,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace)
        where TStyle : struct, IEquatable<TStyle>
    {
        ArgumentNullException.ThrowIfNull(resolve);
        ArgumentNullException.ThrowIfNull(compareStyle);
        ArgumentNullException.ThrowIfNull(appearance);
        var previousStyle = resolve(localStyle, previousTheme);
        var currentStyle = resolve(localStyle, currentTheme);
        var styleImpact = compareStyle(previousStyle, currentStyle);
        ValidateImpact(styleImpact);
        var appearanceImpact = AppearanceResolver.GetImpact(
            this,
            previousTheme,
            appearance(previousStyle),
            currentTheme,
            appearance(currentStyle),
            previousParentAmbientFace,
            currentParentAmbientFace);
        return MaximumImpact(styleImpact, appearanceImpact);
    }

    /// <summary>Commits one derived semantic state and invalidates the active visual-state cascade.</summary>
    /// <typeparam name="T">The semantic state value type.</typeparam>
    /// <param name="field">The current backing field.</param>
    /// <param name="value">The validated replacement value.</param>
    /// <param name="propertyName">The non-empty property name supplied by the compiler.</param>
    /// <returns>Whether a changed value was committed.</returns>
    /// <remarks>
    /// Use this seam when a CLR property changes <see cref="GetAppearanceState"/>. The helper clears
    /// resolved values before calculating the strongest impact declared by the newly active state.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected bool SetVisualStateProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        VerifyMutable();

        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        InvalidateVisualStateCore();
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
    protected void NotifyPropertyChanged(string propertyName, InvalidationImpact impact)
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

    private static void ValidateImpact(InvalidationImpact impact)
    {
        if (!Enum.IsDefined(impact))
        {
            throw new ArgumentOutOfRangeException(
                nameof(impact),
                impact,
                "The change impact is unknown.");
        }
    }

    internal void InvalidateDescendants(Invalidation value) =>
        VisitChildren(child =>
        {
            child.Invalidate(value);
            child.InvalidateDescendants(value);
        });

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
        var modality = ModalityOwner;
        ExceptionDispatchInfo? failure = null;
        modality?.BeginUnavailable(this);

        try
        {
            ExceptionAggregation.Capture(
                () => focus?.Unavailable(this),
                ref failure);
            ExceptionAggregation.Capture(
                () => capture?.Unavailable(this, reason),
                ref failure);
            ExceptionAggregation.Capture(
                () => modality?.Unavailable(
                    this,
                    restoreFocus: !ReferenceEquals(modality.Root, this)),
                ref failure);
            ExceptionAggregation.Capture(
                () => OnUnavailable(reason),
                ref failure);

            if (reason == ReleaseReason.Disposed)
            {
                if (focus is not null && ReferenceEquals(focus.Root, this))
                {
                    ExceptionAggregation.Capture(focus.RootDisposed, ref failure);
                }

                if (capture is not null && ReferenceEquals(capture.Root, this))
                {
                    ExceptionAggregation.Capture(capture.RootDisposed, ref failure);
                }

                if (modality is not null && ReferenceEquals(modality.Root, this))
                {
                    ExceptionAggregation.Capture(() => modality.RootDisposed(this), ref failure);
                }
            }
        }
        finally
        {
            modality?.EndUnavailable(this);
        }

        failure?.Throw();
    }

    /// <summary>Invokes the derived pointer-capture-loss hook after manager state is clear.</summary>
    /// <param name="reason">The defined capture-loss reason.</param>
    internal void NotifyLostPointerCapture(PointerCaptureLossReason reason)
    {
        Debug.Assert(Enum.IsDefined(reason), "Capture-loss reasons are validated internally.");
        OnLostPointerCapture(reason);
    }

    /// <summary>Publishes one already-committed direct pointer-capture loss.</summary>
    /// <param name="reason">The defined capture-loss reason.</param>
    internal void PublishLostPointerCapture(PointerCaptureLossReason reason) =>
        LostPointerCapture?.Invoke(this, new PointerCaptureLostEventArgs(reason));

    private Constraint CreateContentConstraint(Constraint constraint)
    {
        var horizontalInset = LayoutMath.SaturatingAdd(Padding.Horizontal, BorderInset.Horizontal);
        var verticalInset = LayoutMath.SaturatingAdd(Padding.Vertical, BorderInset.Vertical);
        return new Constraint(
            ResolveContentAxis(Width, constraint.Width, Margin.Horizontal, horizontalInset),
            ResolveContentAxis(Height, constraint.Height, Margin.Vertical, verticalInset));
    }

    private Size ResolveDesiredSize(Constraint constraint, Size content)
    {
        var horizontalInset = LayoutMath.SaturatingAdd(Padding.Horizontal, BorderInset.Horizontal);
        var verticalInset = LayoutMath.SaturatingAdd(Padding.Vertical, BorderInset.Vertical);
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

    /// <summary>Commits one fully prevalidated inherited-context entry without invoking virtual hooks.</summary>
    /// <param name="transition">The prospective entry owned by this control.</param>
    internal void CommitContext(in ControlContextTransition transition)
    {
        Debug.Assert(ReferenceEquals(transition.Control, this), "Context entries commit through their owner.");

        if (transition.CommitsContext)
        {
            Dispatcher = transition.Dispatcher;
            CellPolicy = transition.CellPolicy;
            FocusOwner = transition.FocusOwner;
            CaptureOwner = transition.CaptureOwner;
            ModalityOwner = transition.ModalityOwner;
            InheritedTheme = transition.Theme;
        }

        InvalidateResolvedStyleCache();

        if (transition.ThemeTransition is { } themeTransition)
        {
            Invalidate(InvalidationFor(themeTransition.Impact));
        }
    }

    /// <summary>Publishes one exact already-committed Theme or appearance change.</summary>
    /// <param name="change">The staged Theme metadata and exact appearance snapshots.</param>
    internal void PublishAppearanceChanged(in AppearanceChange change)
    {
        Debug.Assert(ReferenceEquals(change.Control, this), "Appearance changes publish through their owning control.");

        if (change.ThemeTransition is { } transition)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Theme)));

            if (transition.ResolvedStylePropertyName is { } actualStylePropertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(actualStylePropertyName));
            }
        }

        PublishAppearanceChanges(change.PreviousAppearance, change.CurrentAppearance);
    }

    /// <summary>Publishes this control's already committed attachment.</summary>
    internal void PublishAttached() => OnAttached();

    /// <summary>Publishes this control's already committed detachment.</summary>
    internal void PublishDetached() => OnDetached();

    private void CommitAndPublishContext(
        Dispatcher? dispatcher,
        Policy cellPolicy,
        FocusManager? focusOwner,
        PointerManager? captureOwner,
        ModalityManager? modalityOwner,
        Theme? theme,
        System.Action? configure)
    {
        OwnedControlRegistry.VerifyMutationAllowed(this);
        var entered = OwnedControlRegistry.EnterPublication(this);
        var previousAppearance = AppearanceSnapshot.CaptureSubtree(this);
        ExceptionDispatchInfo? failure = null;

        try
        {
            var plan = ContextTransitionPlan.Create(
                this,
                dispatcher,
                cellPolicy,
                focusOwner,
                captureOwner,
                modalityOwner,
                theme,
                previousAppearance,
                AppearanceSnapshot.ResolveParentAmbient(Parent),
                propagateContext: true);
            plan.Commit();
            configure?.Invoke();
            var appearanceChanges = AppearanceChange.CreateChanges(
                plan.ThemeTransitions,
                previousAppearance,
                plan.CurrentAppearance);
            PublishContextChanges(appearanceChanges, plan.Attached, plan.Detached, ref failure);
        }
        finally
        {
            OwnedControlRegistry.ExitPublication(entered);
        }

        failure?.Throw();
    }

    private static void PublishContextChanges(
        List<AppearanceChange> appearanceChanges,
        List<Control> attached,
        List<Control> detached,
        ref ExceptionDispatchInfo? failure)
    {
        PublishAppearanceChanges(appearanceChanges, ref failure);

        foreach (var control in detached)
        {
            ExceptionAggregation.Capture(control.PublishDetached, ref failure);
        }

        foreach (var control in attached)
        {
            ExceptionAggregation.Capture(control.PublishAttached, ref failure);
        }
    }

    private static void PublishAppearanceChanges(
        List<AppearanceChange> appearanceChanges,
        ref ExceptionDispatchInfo? failure)
    {
        foreach (var change in appearanceChanges)
        {
            ExceptionAggregation.Capture(
                () => change.Control.PublishAppearanceChanged(change),
                ref failure);
        }
    }

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

    #region Bindings

    private ControlBindingRegistry? _bindingRegistry;

    /// <summary>Gets or creates the private registry that owns this control's bindings.</summary>
    internal ControlBindingRegistry GetBindingRegistry() =>
        _bindingRegistry ??= new ControlBindingRegistry();

    /// <summary>Releases every binding before derived disposal begins.</summary>
    private void DisposeBindings() => _bindingRegistry?.DisposeAll();

    #endregion

    #region Capabilities

    /// <summary>Gets the immutable terminal capability profile inherited from the application.</summary>
    protected TerminalCapabilities Capabilities { get; private set; } = TerminalCapabilities.Conservative;

    /// <summary>Gets the inherited capability profile for framework-owned descendants.</summary>
    internal TerminalCapabilities CapabilityContext => Capabilities;

    /// <summary>Publishes one immutable capability profile across this complete subtree.</summary>
    /// <param name="value">The non-null profile to inherit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached tree is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">This control or an owned descendant is disposed.</exception>
    internal void SetCapabilities(TerminalCapabilities value)
    {
        ArgumentNullException.ThrowIfNull(value);
        VerifyMutable();
        var changed = new List<(Control Control, TerminalCapabilities Previous)>();
        CommitCapabilities(value, changed);
        ExceptionDispatchInfo? failure = null;

        foreach (var entry in changed)
        {
            ExceptionAggregation.Capture(
                () => entry.Control.OnCapabilitiesChanged(entry.Previous, value),
                ref failure);
        }

        failure?.Throw();
    }

    /// <summary>Responds after one inherited terminal capability profile commits.</summary>
    /// <param name="previous">The non-null previous immutable profile.</param>
    /// <param name="current">The non-null committed immutable profile.</param>
    protected virtual void OnCapabilitiesChanged(
        TerminalCapabilities previous,
        TerminalCapabilities current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
    }

    private void CommitCapabilities(
        TerminalCapabilities value,
        List<(Control Control, TerminalCapabilities Previous)> changed)
    {
        ThrowIfDisposed();
        var previous = Capabilities;

        if (!ReferenceEquals(previous, value))
        {
            Capabilities = value;
            changed.Add((this, previous));
        }

        VisitChildren(child => child.CommitCapabilities(value, changed));
    }

    #endregion

    #region Style properties

    private Face? LocalFaceValue { get; set; }
    private Border? LocalBorderValue { get; set; }
    private Shadow? LocalShadowValue { get; set; }

    /// <summary>Gets or sets outer non-collapsing cell edges.</summary>
    public Thickness Margin
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets inner cell edges around content.</summary>
    public Thickness Padding
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets the complete local face, or the semantic normal face when unset.</summary>
    public Face Face
    {
        get => LocalFaceValue ?? GetRoleNormalAppearance().Face;
        set => SetFace(value);
    }

    /// <summary>Gets the fully composed current face with concrete terminal values.</summary>
    public Face ActualFace => GetActualFace(GetAppearanceState());

    /// <summary>Clears the complete local face and returns ownership to the active semantic profile.</summary>
    public void ResetFace()
    {
        VerifyMutable();
        if (LocalFaceValue is null)
        {
            return;
        }

        LocalFaceValue = null;
        InvalidateSubtreeResolvedStyleCache();
        Invalidate(Invalidation.Render);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Face)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActualFace)));
    }

    /// <summary>Gets or sets derived-control border authoring, or the semantic normal border when unset.</summary>
    protected virtual Border Border
    {
        get => LocalBorderValue ?? GetRoleNormalAppearance().Border;
        set => SetBorder(value);
    }

    /// <summary>Gets the fully composed current border with concrete terminal values.</summary>
    public Border ActualBorder => GetActualBorder(GetAppearanceState());

    /// <summary>Clears the complete local border and returns ownership to the active semantic profile.</summary>
    protected virtual void ResetBorder()
    {
        VerifyMutable();
        if (LocalBorderValue is null)
        {
            return;
        }

        var previous = LocalBorderValue.Value.Sides;
        LocalBorderValue = null;
        InvalidateResolvedStyleCache();
        Invalidate(previous == Border.Sides ? Invalidation.Render : Invalidation.Measure);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Border)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActualBorder)));
    }

    /// <summary>Gets or sets derived-control shadow authoring, or the semantic normal shadow when unset.</summary>
    protected Shadow Shadow
    {
        get => LocalShadowValue ?? GetRoleNormalAppearance().Shadow;
        set => SetShadow(value);
    }

    /// <summary>Gets the fully composed current shadow with concrete terminal values.</summary>
    public Shadow ActualShadow => GetActualShadow(GetAppearanceState());

    /// <summary>Clears the complete local shadow and returns ownership to the active semantic profile.</summary>
    protected void ResetShadow()
    {
        VerifyMutable();
        if (LocalShadowValue is null)
        {
            return;
        }

        LocalShadowValue = null;
        InvalidateResolvedStyleCache();
        Invalidate(Invalidation.Render);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Shadow)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActualShadow)));
    }

    #endregion

    #region Theme values

    private readonly Dictionary<VisualState, AppearanceSet> _appearanceSets = [];

    // A real control reaches only a handful of distinct VisualState combinations, so this grows
    // from a small inline capacity rather than allocating the full 512-slot combinatorial space
    // (see #114). Linear scan is cheap at this size and avoids Dictionary's per-entry overhead.
    private ResolvedAppearanceCacheSlot[]? _resolvedAppearanceCache;
    private int _resolvedAppearanceCacheCount;

    /// <summary>Gets the immutable theme inherited from the owning application.</summary>
    public Theme? Theme => InheritedTheme;

    /// <summary>Gets the library-defined semantic appearance role for this control type.</summary>
    protected virtual ThemeRole ThemeRole => ThemeRole.Control;

    /// <summary>Gets the complete appearance profile that owns normal and visual-state presentation.</summary>
    protected virtual ThemeProfile AppearanceProfile =>
        (Theme ?? Themes.Dark).GetProfile(ThemeRole);

    /// <summary>Resolves the complete appearance profile for one explicit prospective Theme.</summary>
    /// <param name="theme">The inherited Theme to resolve, or null for the library fallback.</param>
    /// <returns>The non-null complete appearance profile.</returns>
    /// <remarks>
    /// The current Theme resolves through <see cref="AppearanceProfile"/> so derived property overrides remain
    /// authoritative. Styled controls override this hook as well to resolve a different prospective Theme without
    /// temporarily mutating inherited state.
    /// </remarks>
    protected virtual ThemeProfile GetAppearanceProfile(Theme? theme) =>
        ReferenceEquals(theme, Theme)
            ? AppearanceProfile
            : (theme ?? Themes.Dark).GetProfile(ThemeRole);

    /// <summary>Calculates exact invalidation for a prospective inherited Theme replacement.</summary>
    /// <param name="previous">The currently inherited Theme, or null.</param>
    /// <param name="current">The prospective inherited Theme, or null.</param>
    /// <param name="previousParentAmbientFace">The explicit parent ambient face before replacement.</param>
    /// <param name="currentParentAmbientFace">The explicit parent ambient face after replacement.</param>
    /// <returns>The strongest affected UI phase.</returns>
    protected virtual InvalidationImpact GetThemeChangeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace) =>
        AppearanceResolver.GetImpact(
            this,
            previous,
            (previous ?? Themes.Dark).GetProfile(ThemeRole),
            current,
            (current ?? Themes.Dark).GetProfile(ThemeRole),
            previousParentAmbientFace,
            currentParentAmbientFace);

    /// <summary>Calculates the exact invalidation impact of one resolved appearance change.</summary>
    /// <param name="previous">The resolved appearance before the change.</param>
    /// <param name="current">The resolved appearance after the change.</param>
    /// <returns>The earliest UI phase affected by the change.</returns>
    /// <remarks>Specialized controls may refine layout impact when intrinsic chrome has control-specific geometry.</remarks>
    internal virtual InvalidationImpact GetAppearanceChangeImpact(
        ResolvedAppearance previous,
        ResolvedAppearance current) => AppearanceResolver.GetImpact(previous, current);

    /// <summary>Gets the resolved-style property to publish for one committed Theme change.</summary>
    /// <param name="previous">The previously inherited Theme, or null.</param>
    /// <param name="current">The currently inherited Theme, or null.</param>
    /// <returns>
    /// The non-empty resolved-style property name when Theme ownership changed that value; otherwise null.
    /// </returns>
    /// <remarks>
    /// A styled control compares its complete old and new Theme-owned styles here. A local style returns null
    /// because Theme identity does not change the complete resolved style value. The hook runs before inherited
    /// Theme state commits, so implementations resolve only from the supplied parameters and local style state.
    /// </remarks>
    protected virtual string? GetThemeResolvedStylePropertyName(Theme? previous, Theme? current) => null;

    /// <summary>Gets or sets whether this control stops ambient text appearance inheritance.</summary>
    public bool AppearanceBoundary
    {
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                InvalidateSubtreeResolvedStyleCache();
            }
        }
    }

    /// <summary>Sets or removes one partial local visual-state appearance contribution.</summary>
    /// <param name="state">One non-normal visual state.</param>
    /// <param name="appearance">The partial contribution, or null to remove it.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is normal, combined, or unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected void SetAppearance(VisualState state, AppearanceSet? appearance)
    {
        if (state == VisualState.Normal || !Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "A single non-normal visual state is required.");
        }

        VerifyMutable();
        _ = _appearanceSets.TryGetValue(state, out var previous);
        if (appearance is { } value)
        {
            if (previous == value)
            {
                return;
            }

            _appearanceSets[state] = value;
        }
        else if (!_appearanceSets.Remove(state))
        {
            return;
        }

        InvalidateResolvedStyleCache();
        var sidesChanged = previous.Border?.Sides.HasValue == true ||
                           appearance?.Border?.Sides.HasValue == true;
        Invalidate(sidesChanged ? Invalidation.Measure : Invalidation.Render);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppearanceSets)));
    }

    internal Theme? InheritedTheme { get; private set; }

    /// <summary>Preflights one prospective Theme identity using explicit cache-neutral ambient context.</summary>
    /// <param name="theme">The prospective inherited Theme, or null.</param>
    /// <param name="previousParentAmbientFace">The parent ambient face before the transaction.</param>
    /// <param name="currentParentAmbientFace">The parent ambient face after the transaction.</param>
    /// <returns>The prospective transition metadata, or null when Theme identity is unchanged.</returns>
    internal ThemeTransition? PrepareThemeTransition(
        Theme? theme,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace)
    {
        VerifyMutable();

        if (ReferenceEquals(InheritedTheme, theme))
        {
            return null;
        }

        var previousTheme = InheritedTheme;
        var impact = GetThemeChangeImpact(
            previousTheme,
            theme,
            previousParentAmbientFace,
            currentParentAmbientFace);
        ValidateImpact(impact);
        var resolvedStylePropertyName = GetThemeResolvedStylePropertyName(previousTheme, theme);

        if (resolvedStylePropertyName is not null)
        {
            ArgumentException.ThrowIfNullOrEmpty(resolvedStylePropertyName);
        }

        return new ThemeTransition(
            this,
            previousTheme,
            theme,
            resolvedStylePropertyName,
            impact);
    }

    internal void SetTheme(Theme? theme)
    {
        VerifyMutable();

        if (ReferenceEquals(InheritedTheme, theme))
        {
            return;
        }

        var previousAppearance = AppearanceSnapshot.CaptureSubtree(this);
        var plan = ContextTransitionPlan.Create(
            this,
            Dispatcher,
            CellPolicy,
            FocusOwner,
            CaptureOwner,
            ModalityOwner,
            theme,
            previousAppearance,
            AppearanceSnapshot.ResolveParentAmbient(Parent),
            propagateContext: false);
        plan.Commit();
        var appearanceChanges = AppearanceChange.CreateChanges(
            plan.ThemeTransitions,
            previousAppearance,
            plan.CurrentAppearance);
        ExceptionDispatchInfo? failure = null;
        PublishAppearanceChanges(appearanceChanges, ref failure);
        failure?.Throw();
    }

    internal void PropagateTheme(Theme? theme)
    {
        OwnedControlRegistry.VerifyMutationAllowed(this);
        var entered = OwnedControlRegistry.EnterPublication(this);
        var previousAppearance = AppearanceSnapshot.CaptureSubtree(this);
        ExceptionDispatchInfo? failure = null;
        try
        {
            var plan = ContextTransitionPlan.Create(
                this,
                Dispatcher,
                CellPolicy,
                FocusOwner,
                CaptureOwner,
                ModalityOwner,
                theme,
                previousAppearance,
                AppearanceSnapshot.ResolveParentAmbient(Parent),
                propagateContext: true);
            plan.Commit();
            var appearanceChanges = AppearanceChange.CreateChanges(
                plan.ThemeTransitions,
                previousAppearance,
                plan.CurrentAppearance);
            PublishContextChanges(appearanceChanges, plan.Attached, plan.Detached, ref failure);
        }
        finally
        {
            OwnedControlRegistry.ExitPublication(entered);
        }

        failure?.Throw();
    }

    /// <summary>Gets local state allowed to contribute ambient text appearance to descendants.</summary>
    internal virtual VisualState AmbientAppearanceState => VisualState.Normal;

    /// <summary>Gets whether local visual-state changes can affect inherited descendant face values.</summary>
    internal virtual bool StateAffectsAmbientAppearance => false;

    /// <inheritdoc/>
    protected internal TerminalStyle GetResolvedStyle(VisualState state) => GetResolvedAppearance(state).Style;

    internal ResolvedAppearance GetResolvedAppearance(VisualState state)
    {
        const int stateCount = 1 << 9;
        var index = (int) state;
        if ((uint) index >= stateCount)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The visual state contains unknown flags.");
        }

        var cache = _resolvedAppearanceCache;

        for (var slot = 0; slot < _resolvedAppearanceCacheCount; slot++)
        {
            if (cache![slot].State == state)
            {
                return cache[slot].Appearance;
            }
        }

        UncachedAppearanceResolutionCount++;
        var resolved = AppearanceResolver.Resolve(this, state);

        if (cache is null)
        {
            cache = new ResolvedAppearanceCacheSlot[4];
            _resolvedAppearanceCache = cache;
        }
        else if (_resolvedAppearanceCacheCount == cache.Length)
        {
            Array.Resize(ref cache, cache.Length * 2);
            _resolvedAppearanceCache = cache;
        }

        cache[_resolvedAppearanceCacheCount++] = new ResolvedAppearanceCacheSlot(state, resolved);
        return resolved;
    }

    private readonly record struct ResolvedAppearanceCacheSlot(VisualState State, ResolvedAppearance Appearance);

    internal Face GetActualFace(VisualState state) => GetResolvedAppearance(state).Face;

    internal Border GetActualBorder(VisualState state) => GetResolvedAppearance(state).Border;

    internal Shadow GetActualShadow(VisualState state) => GetResolvedAppearance(state).Shadow;

    internal int UncachedAppearanceResolutionCount { get; private set; }

    internal Face? LocalFace => LocalFaceValue;

    internal Border? LocalBorder => LocalBorderValue;

    internal Shadow? LocalShadow => LocalShadowValue;

    internal IReadOnlyDictionary<VisualState, AppearanceSet> AppearanceSets => _appearanceSets;

    internal ThemeProfile ResolvedAppearanceProfile => AppearanceProfile;

    /// <summary>Resolves one prospective profile through the derived control hook.</summary>
    /// <param name="theme">The explicit prospective inherited Theme, or null.</param>
    /// <returns>The non-null complete appearance profile.</returns>
    internal ThemeProfile ResolveAppearanceProfile(Theme? theme) => GetAppearanceProfile(theme);

    internal static Color ResolveThemeColor(Color color) => color;

    /// <summary>Resolves a possibly-literal color value against an optional theme.</summary>
    /// <param name="value">The literal or theme-referenced color value.</param>
    /// <param name="theme">The active theme, or null when no theme resolves the value.</param>
    /// <returns>The literal color, or the theme-resolved color, or <see cref="Color.Default"/>.</returns>
    internal static Color ResolveColor(ColorValue value, Theme? theme) => value.IsLiteral
        ? value.Literal
        : theme?.ResolveColor(value.ThemeColor) ?? Color.Default;

    internal Rune ResolveControlGlyph(ControlGlyph glyph) =>
        CellGlyphResolver.Resolve(glyph.Value, glyph.Fallback, CellPolicy.AmbiguousWidth);

    internal BorderGlyphStyle ResolveBorderGlyphs(BorderGlyphStyle glyphs)
    {
        var fallback = ControlGlyphs.Chrome;
        return new BorderGlyphStyle(
            CellGlyphResolver.Resolve(glyphs.TopLeft, fallback.TopLeft.Fallback, CellPolicy.AmbiguousWidth),
            CellGlyphResolver.Resolve(glyphs.Top, fallback.Top.Fallback, CellPolicy.AmbiguousWidth),
            CellGlyphResolver.Resolve(glyphs.TopRight, fallback.TopRight.Fallback, CellPolicy.AmbiguousWidth),
            CellGlyphResolver.Resolve(glyphs.Right, fallback.Right.Fallback, CellPolicy.AmbiguousWidth),
            CellGlyphResolver.Resolve(glyphs.BottomRight, fallback.BottomRight.Fallback, CellPolicy.AmbiguousWidth),
            CellGlyphResolver.Resolve(glyphs.Bottom, fallback.Bottom.Fallback, CellPolicy.AmbiguousWidth),
            CellGlyphResolver.Resolve(glyphs.BottomLeft, fallback.BottomLeft.Fallback, CellPolicy.AmbiguousWidth),
            CellGlyphResolver.Resolve(glyphs.Left, fallback.Left.Fallback, CellPolicy.AmbiguousWidth));
    }

    /// <summary>Gets the number of direct resolved-appearance cache clears for instrumentation.</summary>
    internal int ResolvedAppearanceCacheInvalidationCount { get; private set; }

    /// <summary>Clears this control's resolved-appearance cache exactly once.</summary>
    internal void InvalidateResolvedStyleCache()
    {
        ResolvedAppearanceCacheInvalidationCount++;
        _resolvedAppearanceCache = null;
        _resolvedAppearanceCacheCount = 0;
    }

    private void InvalidateVisualStateCore()
    {
        if (StateAffectsAmbientAppearance)
        {
            InvalidateSubtreeResolvedStyleCache();
        }
        else
        {
            InvalidateResolvedStyleCache();
        }

        Invalidate(VisualStateInvalidation());
    }

    internal void InvalidateSubtreeResolvedStyleCache()
    {
        var stack = new Stack<Control>();
        stack.Push(this);

        while (stack.TryPop(out var control))
        {
            control.InvalidateResolvedStyleCache();

            for (var index = control.OwnedControlCount - 1; index >= 0; index--)
            {
                stack.Push(control.OwnedControlAt(index));
            }
        }
    }

    private ThemeAppearance GetRoleNormalAppearance() =>
        AppearanceProfile.Normal;

    private void PublishAppearanceChanges(ResolvedAppearance previous, ResolvedAppearance current)
    {
        if (previous.Face != current.Face)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActualFace)));
        }

        if (previous.Border != current.Border)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActualBorder)));
        }

        if (previous.Shadow != current.Shadow)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActualShadow)));
        }
    }

    private void SetFace(Face value)
    {
        VerifyMutable();
        if (LocalFaceValue == value)
        {
            return;
        }

        LocalFaceValue = value;
        InvalidateSubtreeResolvedStyleCache();
        Invalidate(Invalidation.Render);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Face)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActualFace)));
    }

    private void SetBorder(Border value)
    {
        VerifyMutable();
        if (LocalBorderValue == value)
        {
            return;
        }

        var previousSides = Border.Sides;
        LocalBorderValue = value;
        InvalidateResolvedStyleCache();
        Invalidate(previousSides == value.Sides ? Invalidation.Render : Invalidation.Measure);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Border)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActualBorder)));
    }

    private void SetShadow(Shadow value)
    {
        VerifyMutable();
        if (LocalShadowValue == value)
        {
            return;
        }

        LocalShadowValue = value;
        InvalidateResolvedStyleCache();
        Invalidate(Invalidation.Render);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Shadow)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActualShadow)));
    }

    #endregion
}
