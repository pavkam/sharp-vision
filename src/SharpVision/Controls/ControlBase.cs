// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.ComponentModel;
using System.Runtime.ExceptionServices;

using DataBinding;

using SharpVision.Menus;
using SharpVision.Runtime;
using SharpVision.Terminal.Input;

using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;
using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>
/// Defines a traditional mutable UI element with dispatcher affinity and box layout.
/// </summary>
/// <remarks>
/// Detached controls may be assembled on any thread. Once attached, every
/// mutation and disposal must run on <see cref="Dispatcher"/>.
/// </remarks>
[PublicAPI]
[DebuggerDisplay("{DebuggerSummary,nq}")]
public abstract partial class ControlBase: INotifyPropertyChanged, IDisposable
{
    private StyleSlotBase? _primaryStyle;
    private Dictionary<string, StyleSlotBase>? _styleSlots;
    private Dictionary<string, long>? _synchronizedPropertyVersions;
    private long _stylePublicationVersion;
    private ThemeStructuralDependency _themeStructuralDependencies;
    private bool? _effectiveIsVisible;
    private bool? _effectiveIsEnabled;
    private long _visibilityVersion;
    private long _isEnabledVersion;

    /// <summary>Gets how many times this control has actually recomputed <see cref="EffectiveIsVisible"/>
    /// or <see cref="EffectiveIsEnabled"/> from ancestor state rather than returning a cached value.
    /// Test-only diagnostic proving the memoized read stays amortized O(1) per node - a repeated
    /// <see cref="HitTest"/> descent over an unchanged subtree must never re-derive a node's own
    /// effective state more than once.</summary>
    internal int EffectiveStateComputationCount { get; private set; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerSummary => $"{GetType().Name} [{Bounds.Width}×{Bounds.Height}]" +
        (IsFocused ? " Focused" : "") +
        (Visibility != Visibility.Visible ? $" {Visibility}" : "") +
        (IsDisposed ? " Disposed" : "");

    /// <summary>Initializes an empty control with one central visual-ownership registry.</summary>
    protected ControlBase()
    {
        OwnedControls = new OwnedControlRegistry(this);
        _ = AddHandler(Events.Key, OnTextSelectionKeyRouted, handledEventsToo: true);
        _ = AddHandler(Events.Pointer, OnTextSelectionPointerRouted, handledEventsToo: true);
        _ = AddHandler(Events.TerminalFocusChanged, OnTextSelectionTerminalFocusRouted, handledEventsToo: true);
    }

    /// <summary>Raised after one public property has committed a changed value.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the owning parent, or null for a detached/root control.</summary>
    public ControlBase? Parent { get; private set; }

    /// <summary>Gets the exact slot owning this control, or null for an ownership root.</summary>
    internal OwnedControlSlot? OwningSlot { get; private set; }

    /// <summary>Gets the owning dispatcher while attached.</summary>
    public Dispatcher? Dispatcher { get; private set; }

    /// <summary>Gets the lifecycle generation of the current dispatcher attachment.</summary>
    /// <remarks>
    /// Derived controls capture this value when queuing dispatcher work whose validity ends at
    /// detachment. It advances for every attach and detach commit, including a later reattachment
    /// to the same dispatcher instance.
    /// </remarks>
    private protected long AttachmentVersion { get; private set; }

    /// <summary>Gets the committed dispatcher-attachment generation used by bindings to reject
    /// queued source work captured under an earlier owner.</summary>
    internal long BindingAttachmentVersion => AttachmentVersion;

    /// <summary>Gets the attached dispatcher's clock, or the system clock while detached.</summary>
    private protected TimeProvider TimeProvider => Dispatcher?.TimeProvider ?? TimeProvider.System;

    /// <summary>Gets the immutable Unicode cell policy inherited from the root.</summary>
    protected internal UnicodePolicy CellPolicy { get; private set; } = UnicodePolicy.Default;

    /// <summary>Measures printable cells under the tree's ambient East Asian Ambiguous width
    /// policy.</summary>
    /// <param name="value">The borrowed UTF-16 text.</param>
    /// <returns>The printable cell count <see cref="CellPolicy"/> would render.</returns>
    /// <remarks>
    /// The bare <c>Terminal.Unicode.Width.Measure(value)</c> overload defaults to
    /// <see cref="Ambiguous.Narrow"/>, which silently disagrees with a control tree whose
    /// <see cref="CellPolicy"/> resolved to <see cref="Ambiguous.Wide"/> - measurement and
    /// rendering must consult the same policy.
    /// </remarks>
    protected internal int MeasureCells(ReadOnlySpan<char> value) =>
        Terminal.Unicode.Width.Measure(value, CellPolicy.AmbiguousWidth).Cells;

    /// <summary>Gets or sets the requested border-box width.</summary>
    /// <remarks>
    /// A surface that resolves a child directly to its own layout slot honors this value and
    /// <see cref="HorizontalAlignment"/> only while it is left at the default
    /// <see cref="LengthKind.Auto"/> - a <see cref="Layout.Grid"/> cell fills an
    /// <c>Auto</c>-width child with the complete union of its spanned tracks, but leaves this
    /// axis unresolved once a non-<c>Auto</c> <see cref="Width"/> is set, so the requested size
    /// and <see cref="HorizontalAlignment"/> place the child within the cell instead.
    /// <see cref="MinWidth"/>/<see cref="MaxWidth"/> are honored either way: on the filled path
    /// they cap the fill and hand any resulting slack to <see cref="HorizontalAlignment"/>; on
    /// the explicit-<see cref="Width"/> path they cap the requested size the same way they do
    /// everywhere else (see docs/controls/layout/grid.md). By contrast
    /// <see cref="Layout.Stack"/> resolves only its own stacking axis this way and
    /// leaves the cross axis to this property.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Length Width
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets the requested border-box height.</summary>
    /// <remarks>
    /// The same rules documented on <see cref="Width"/> apply to this axis and
    /// <see cref="VerticalAlignment"/>.
    /// </remarks>
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
    [NonNegativeValue]
    public int MinWidth
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentException.ThrowIfAboveMaximum(value, MaxWidth, nameof(value), "Minimum width cannot exceed maximum width.");

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets the non-negative minimum border-box height.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="ArgumentException">The value exceeds <see cref="MaxHeight"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int MinHeight
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentException.ThrowIfAboveMaximum(value, MaxHeight, nameof(value), "Minimum height cannot exceed maximum height.");

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets the maximum border-box width.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="ArgumentException">The value is below <see cref="MinWidth"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int MaxWidth
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentException.ThrowIfBelowMinimum(value, MinWidth, nameof(value), "Maximum width cannot be below minimum width.");

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = int.MaxValue;

    /// <summary>Gets or sets the maximum border-box height.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="ArgumentException">The value is below <see cref="MinHeight"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int MaxHeight
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentException.ThrowIfBelowMinimum(value, MinHeight, nameof(value), "Maximum height cannot be below minimum height.");

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
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);
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
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Arrange);
        }
    } = VerticalAlignment.Stretch;

    /// <summary>Gets or sets local layout/render/input participation.</summary>
    /// <remarks>
    /// A change to hidden or collapsed state commits and invalidates first, completes focus and
    /// pointer-capture cleanup, and then raises <see cref="PropertyChanged"/>. Cleanup and property
    /// callbacks both run when either fails, and the earliest failure is rethrown afterward.
    /// If cleanup commits a newer visibility transition, the superseded outer transition does not
    /// publish duplicate property, visibility, or derived-focus notifications.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Visibility Visibility
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);
            var impact = value == Visibility.Collapsed || field == Visibility.Collapsed
                ? InvalidationImpact.Measure
                : InvalidationImpact.Render;
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            var derivedSnapshot = SnapshotDerivedFocusState();
            field = value;
            var version = ++_visibilityVersion;
            InvalidateEffectiveState();
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

            if (IsCurrentVisibilityTransition(version, value))
            {
                ExceptionAggregation.Capture(
                    () => PropertyChanged?.Invoke(
                        this,
                        new PropertyChangedEventArgs(nameof(Visibility))),
                    ref failure);
            }

            if (IsCurrentVisibilityTransition(version, value))
            {
                ExceptionAggregation.Capture(
                    () => VisibilityChanged?.Invoke(this, EventArgs.Empty),
                    ref failure);
            }

            if (IsCurrentVisibilityTransition(version, value))
            {
                ExceptionAggregation.Capture(() => PublishDerivedFocusStateChanges(derivedSnapshot), ref failure);
            }

            failure?.Throw();
        }
    } = Visibility.Visible;

    /// <summary>Gets or sets whether local behavior accepts input.</summary>
    /// <remarks>
    /// Disabling commits and invalidates first, completes focus and pointer-capture cleanup, and
    /// then raises <see cref="PropertyChanged"/>. Cleanup and property callbacks both run when
    /// either fails, and the earliest failure is rethrown afterward.
    /// If cleanup commits a newer enabled transition, the superseded outer transition does not
    /// publish duplicate property, enabled, or derived-focus notifications.
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

            var derivedSnapshot = SnapshotDerivedFocusState();
            field = value;
            var version = ++_isEnabledVersion;
            InvalidateEffectiveState();

            // Disabled is in the appearance states' chrome-geometry state set (border.sides may
            // change per-state), so this must route through the same invalidation-impact decision
            // every other visual-state driver (SetFocused, SetPressed, ...) already makes - a
            // hard-coded Render here left a themed disabled border painted over content that was
            // never re-arranged out of the way.
            InvalidateVisualState();
            InvalidateDescendantsVisualState();
            ExceptionDispatchInfo? failure = null;

            if (!value)
            {
                ExceptionAggregation.Capture(
                    () => NotifyUnavailable(ReleaseReason.Disabled),
                    ref failure);
            }

            if (IsCurrentEnabledTransition(version, value))
            {
                ExceptionAggregation.Capture(
                    () => PropertyChanged?.Invoke(
                        this,
                        new PropertyChangedEventArgs(nameof(IsEnabled))),
                    ref failure);
            }

            if (IsCurrentEnabledTransition(version, value))
            {
                ExceptionAggregation.Capture(
                    () => EnabledChanged?.Invoke(this, EventArgs.Empty),
                    ref failure);
            }

            if (IsCurrentEnabledTransition(version, value))
            {
                ExceptionAggregation.Capture(() => PublishDerivedFocusStateChanges(derivedSnapshot), ref failure);
            }

            failure?.Throw();
        }
    } = true;

    private bool IsCurrentVisibilityTransition(long version, Visibility value) =>
        !IsDisposed && _visibilityVersion == version && Visibility == value;

    private bool IsCurrentEnabledTransition(long version, bool value) =>
        !IsDisposed && _isEnabledVersion == version && IsEnabled == value;

    /// <summary>Gets whether this control and every ancestor are enabled.</summary>
    public bool EffectiveIsEnabled
    {
        get
        {
            if (_effectiveIsEnabled is { } cached)
            {
                return cached;
            }

            EffectiveStateComputationCount++;
            return (_effectiveIsEnabled = IsEnabled && (Parent?.EffectiveIsEnabled ?? true)).Value;
        }
    }

    /// <summary>Gets whether this control and every ancestor are visible.</summary>
    public bool EffectiveIsVisible
    {
        get
        {
            if (_effectiveIsVisible is { } cached)
            {
                return cached;
            }

            EffectiveStateComputationCount++;
            return (_effectiveIsVisible = Visibility == Visibility.Visible && (Parent?.EffectiveIsVisible ?? true)).Value;
        }
    }

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
    public bool IsFocusable
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
    /// <remarks>This effective value includes <see cref="IsFocusable"/>, visibility, enabled state, and disposal.</remarks>
    public bool CanFocus => IsFocusable && EffectiveIsVisible && EffectiveIsEnabled && !IsDisposed;

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
    public bool IsTabStop
    {
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.None))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanTabStop)));
            }
        }
    } = true;

    /// <summary>Gets whether this control currently participates in Tab traversal.</summary>
    public virtual bool CanTabStop => CanFocus && IsTabStop;

    /// <summary>Gets or sets how Tab traversal treats this control's subtree.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TabNavigation TabNavigation
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The tab navigation mode is unknown.");

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
    public ContextMenu? ContextMenu
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
    public event EventHandler? EnabledChanged;

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

    /// <summary>Gets the wrapping generation of state invalidation requests affecting this subtree.</summary>
    /// <remarks>
    /// Selectable-text aggregators use this cheap conservative signal to decide when an exact
    /// semantic snapshot comparison is necessary. It advances even when phase bits were already
    /// pending, because a second mutation before layout still represents newer retained state.
    /// </remarks>
    internal ulong SelectableTextInvalidationVersion { get; private set; }

    /// <summary>Gets the last outer constraint committed by the measure transaction, or null before initial measurement.</summary>
    /// <remarks>Derived overlay-owned controls use this viewport record when their own resolved box is intentionally smaller than the host.</remarks>
    internal Constraint? LastMeasureConstraint { get; private set; }

    private Rect? LastArrangeSlot { get; set; }

    private bool LastWidthResolved { get; set; }

    private bool LastHeightResolved { get; set; }

    private bool IsMeasuring { get; set; }

    private protected bool IsArranging { get; set; }

    /// <summary>
    /// Children whose upward Arrange propagation was swallowed while this control was arranging
    /// them, recorded so <see cref="Arrange(Rect, bool, bool)"/> can re-run that propagation for
    /// any child the transaction did not actually arrange. Lazily allocated - the overwhelming
    /// majority of arranges never swallow anything - and cleared at the end of every arrange.
    /// </summary>
    private List<ControlBase>? SwallowedArrangeChildren { get; set; }

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
    [MustDisposeResource]
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
    internal virtual ControlBase? HitTest(Point point)
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
        => Attach(dispatcher, UnicodePolicy.Default);

    /// <summary>Attaches a root and descendants with one immutable cell policy.</summary>
    /// <param name="dispatcher">The non-null owning dispatcher.</param>
    /// <param name="cellPolicy">The non-null inherited Unicode cell policy.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException">Any descendant is already attached.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher or this control is owned.</exception>
    /// <exception cref="ObjectDisposedException">Any descendant is disposed.</exception>
    internal void Attach(Dispatcher dispatcher, UnicodePolicy cellPolicy)
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
        UnicodePolicy cellPolicy,
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
        UnicodePolicy cellPolicy,
        Theme theme,
        Action configure)
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
                UnicodePolicy.Default,
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
    internal void SetCellPolicy(UnicodePolicy value)
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
                ? Math.Min(available.Width, Math.Clamp(available.Width, MinWidth, MaxWidth))
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
                ? Math.Min(available.Height, Math.Clamp(available.Height, MinHeight, MaxHeight))
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

            // Self-heal for the swallow above: this control's own Pending is checked first so the
            // common case - nothing was recorded, or every recorded child was actually arranged
            // and its Arrange bit already cleared - costs one null/empty check. Anything left
            // still carrying Invalidation.Arrange means this transaction measured that child
            // without arranging it, so replay the propagation its own Invalidate call withheld:
            // re-adding this control's expanded bits and notifying Parent. Calling the child's own
            // Invalidate again would not help - its Pending already holds the bit, so it would see
            // added == None and stay silent - the withheld half was always the notification to
            // this control's ancestors, not the child's own bookkeeping.
            if (SwallowedArrangeChildren is { Count: > 0 } swallowedArrangeChildren)
            {
                foreach (var child in swallowedArrangeChildren)
                {
                    if ((child.Pending & Invalidation.Arrange) != 0)
                    {
                        Invalidate(Invalidation.Arrange);
                    }
                }

                swallowedArrangeChildren.Clear();
            }
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
    protected Size MeasureChild(ControlBase child, Constraint constraint)
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
        ControlBase child,
        Rect slot,
        ResolvedAxes resolvedAxes = ResolvedAxes.None)
    {
        ArgumentNullException.ThrowIfNull(child);

        ArgumentOutOfRangeException.ThrowIfNotDefined(resolvedAxes, nameof(resolvedAxes), "The resolved axes contain an unknown flag.");

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
        var wasRenderDirty = (Pending & Invalidation.Render) != 0;
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

            if (!wasRenderDirty && canvas.HasPreviousFrame && CanReuseCleanRender())
            {
                // canvas.HasPreviousFrame already guarantees no layout ran anywhere in the tree
                // since the frame it copies from (see Application.StartRender), so this control's
                // own Bounds - and therefore visual - are unchanged. Combined with an unset render
                // bit and CanReuseCleanRender's leaf/shadow-free/owns-no-popup-of-its-own
                // requirement, the previous frame's cells for this exact region are still correct.
                // Being overlapped or bordered by a FOREIGN popup - one this control
                // does not itself own - needs no separate exclusion here: see the correctness note
                // above CanReuseCleanRender for why.
                visual.CopyFromPrevious(visual.Bounds);

                // Cell copying alone cannot reproduce a paint effect that lives outside the frame's
                // cell arena - currently only Display.Image's semantic placement. OnReuseCleanRender
                // re-asserts exactly that out-of-band state for this exact traversal position,
                // reading this control's own CURRENT properties rather than anything cached: an
                // unset render bit already proves nothing that would change them fired since the
                // last real paint.
                OnReuseCleanRender(visual);
            }
            else
            {
                var appearanceState = GetAppearanceState();
                var chrome = GetChromeRenderOptions();
                this.RenderUnderlay(visual, appearanceState, chrome);
                OnRenderContent(visual);
                var descendantBounds = DescendantRenderBounds;
                var descendantClip = ClipsChildren
                    ? ControlChrome.ResolveClipBox(contentClip, Bounds, descendantBounds, canvas.Bounds)
                        .Intersect(descendantBounds)
                    : contentClip;
                var descendantCanvas = ClipsDescendantVisualOverflow
                    ? canvas.Clip(descendantBounds)
                    : canvas;
                RenderChildren(descendantCanvas, descendantClip);
                OnRenderAdornment(visual);
                this.RenderBorder(visual, appearanceState, chrome);
                RenderOverlay(visual);
            }

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

    // Narrow, maintainer-approved scope: reuse is safe only for a subtree whose own
    // paint is fully reproduced by copying cells - no children (nothing else to composite), no
    // owned popups of its own (OwnedControlCount covers both layers), and no visible shadow
    // (CopyFromPrevious never restores VisualBounds' shadow-expanded overflow region). A control
    // whose paint has an effect a cell copy alone cannot reproduce opts out via
    // RequiresCompleteRender - still available as a general escape hatch, though nothing sets it
    // today; Display.Image used to (see the placement paragraph below for why it no longer needs
    // to). A transparent underlay never authors its uncovered cells - those cells hold whatever the
    // parent painted underneath, so copying them resurrects the parent's OLD content over content
    // that may have since changed with no invalidation of this otherwise render-clean control;
    // requiring an opaque fill excludes exactly that case.
    //
    // A visible shadow is safe to reuse under the identical reasoning, extended one level: a
    // shadow cell is safe to copy exactly when its own paint is a full destination overwrite that
    // never depends on whatever was already there, so the copied bytes are provably identical to
    // what a fresh paint would produce regardless of what changed underneath since the copied
    // frame. Only BlockGlyph with an opaque resolved background qualifies - DrawRune with an
    // opaque background replaces grapheme, style, and background together (ControlChrome.cs).
    // FractionalBlock always reads the destination: DrawFractionalShadow hardcodes
    // BackgroundMode.Transparent regardless of the shadow's own configured background. Composite
    // never qualifies either, even opaque: ShadowMode.Composite's own contract is to "preserve
    // underlying graphemes and replace their semantic style" - Canvas.ApplyStyle only ever calls
    // TrySetOwnerStyle, which never touches the stored character - so a copied Composite shadow
    // cell would carry the OLD frame's grapheme forward even though the style is correct,
    // resurrecting stale content exactly like the transparent-underlay case the opaque body
    // fill guard above already exists to prevent.
    //
    // Image-bearing subtrees participate too, without any exclusion here:
    // Canvas.CopyFromPrevious restores cells only and never replays Canvas.DrawImage's semantic
    // placement, so a naive reuse would silently drop Display.Image's placement from the frame
    // the moment it went render-clean, even though its cells stayed correct - the renderer would
    // see the placement vanish from Frame.Placements and treat it as removed. OnReuseCleanRender
    // closes that gap: called at the exact traversal position OnRenderContent would otherwise run,
    // it re-asserts the placement by reading this control's own CURRENT Source/Stretch/bounds -
    // never a cached prior value - so it is provably identical to what a fresh paint would record
    // (an unset render bit already proves none of those changed since the last real paint). Because
    // it runs through the ordinary Canvas.DrawImage call at the ordinary traversal position, paint
    // order against every other placement or cell mutation this frame is exactly what it would have
    // been had this control painted fresh: no separate ordering reasoning is needed beyond what
    // already governs a normal render.
    //
    // Popup-overlapped subtrees need no exclusion here at all - traced the
    // actual coupling between popup state and the two flags this method depends on, rather than
    // assuming an exclusion was needed just because a popup's paint is "someone else's" pass:
    //
    // - Steady state (an open popup's SurfaceBounds identical to the previous frame): the popup
    //   layer is repainted unconditionally on every frame - RenderOwnedPopupDescendants runs from
    //   Root every time, never gated by any clean check - so whatever an overlapped control wrote
    //   at a contested cell, copied or freshly painted, is always overwritten by the popup's own
    //   current-frame paint before the frame is committed. Same "paint order, not paint source,
    //   decides the final byte" invariant already established and tested for shadow overlap above.
    // - Any frame where a popup's footprint could have changed (opened, closed, moved, or resized)
    //   is exactly a frame where SOME control's Bounds changed, which - per Popup.ArrangeOverride -
    //   only happens inside an Arrange pass, and Popup.SetOpen's every _isOpen transition pairs
    //   with NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.Measure) (Popup.cs). Control's
    //   own Invalidate(value) propagates that up through Parent to Root, so Root.Pending gains
    //   Measure/Arrange, PerformLayout runs, and Application.ProcessInvalidation calls StartRender
    //   with skipCleanSubtrees false for that entire frame - meaning canvas.HasPreviousFrame is
    //   false for every control app-wide that frame, and CanReuseCleanRender is never even
    //   consulted anywhere. A popup's footprint can only ever be stale relative to a frame nothing
    //   was allowed to copy from in the first place.
    //
    // Together: whenever CopyFromPrevious for a leaf actually executes, either no popup nearby
    // changed since the copied frame (safe by the second point), or one did and this branch was
    // never reached this frame (safe by construction). See RenderCleanSubtreeReuseTests for
    // adversarial-ordering coverage of both cases.
    private bool CanReuseCleanRender()
    {
        var appearance = GetResolvedAppearance(GetAppearanceState());

        return OwnedControlCount == 0 &&
            !RequiresCompleteRender &&
            appearance.BackgroundMode == BackgroundMode.Opaque &&
            (!appearance.Shadow.IsVisible ||
                (appearance.Shadow.Mode == ShadowMode.BlockGlyph &&
                 appearance.ShadowBackgroundMode == BackgroundMode.Opaque));
    }

    /// <summary>
    /// Re-asserts any out-of-band paint state a render-clean control's own cell copy cannot
    /// reproduce, called at the exact traversal position <see cref="OnRenderContent"/> would
    /// otherwise run. The default does nothing - a plain cell copy is already a complete
    /// reproduction for the overwhelming majority of controls. <see cref="Display.Image"/>
    /// overrides this to re-record its semantic <see cref="TerminalCanvas.DrawImage"/> placement,
    /// which a cell copy alone never replays.
    /// </summary>
    /// <param name="canvas">The same clipped canvas <see cref="OnRenderContent"/> would receive.</param>
    internal virtual void OnReuseCleanRender(TerminalCanvas canvas)
    {
    }

    /// <summary>Requests a phase and every dependent later phase.</summary>
    /// <param name="value">The earliest dirty phase.</param>
    internal void Invalidate(Invalidation value)
    {
        unchecked
        {
            SelectableTextInvalidationVersion++;
        }

        var expanded = Expand(value);
        var added = expanded & ~Pending;

        if (added == Invalidation.None)
        {
            Parent?.PropagateSelectableTextInvalidationVersion();
            return;
        }

        Pending |= expanded;

        // A parent may remeasure a child while resolving its final arrangement, as Grid does for
        // finite tracks. The parent is trusted to arrange that child in the same transaction, so
        // propagating the child's resulting arrange request here would schedule an identical
        // ancestor layout forever. That trust is a contract this method cannot itself enforce -
        // if the parent's override measures a child it then does not arrange, record the child
        // here instead of dropping the request outright. Arrange's finally block re-runs the
        // withheld propagation for any recorded child still dirty once it stops arranging, so a
        // rare contract violation still reaches an ancestor on a later pass instead of freezing
        // the child's subtree forever.
        if (value == Invalidation.Arrange && Parent is { IsArranging: true } arrangingParent)
        {
            arrangingParent.PropagateSelectableTextInvalidationVersion();
            (arrangingParent.SwallowedArrangeChildren ??= []).Add(this);
            return;
        }

        Parent?.Invalidate(value);
    }

    /// <summary>Marks this control's own dirty phases - and every phase <see cref="Invalidate(Invalidation)"/>
    /// would also expand them into - without notifying <see cref="Parent"/> or touching
    /// <see cref="SwallowedArrangeChildren"/>.</summary>
    /// <remarks>
    /// For a caller that already knows it is about to <see cref="Measure"/> and/or
    /// <see cref="Arrange(Rect, bool, bool)"/> this exact control synchronously afterward, and only
    /// needs to bypass those methods' own unchanged-constraint/slot short-circuit rather than
    /// schedule a fresh ancestor layout pass. <see cref="Invalidate(Invalidation)"/> would do both:
    /// it walks up through <paramref name="value"/>'s dependents on <em>every</em> control from here
    /// to <see cref="Parent"/>'s root, which is the correct behavior for a control whose own state
    /// changed, but far more than a control that merely needs its own next layout call to actually
    /// run. Bypassing <see cref="Invalidate(Invalidation)"/> entirely - rather than reusing it and
    /// suppressing propagation afterward - also means this never registers with a currently
    /// arranging <see cref="Parent"/>'s <see cref="SwallowedArrangeChildren"/> self-heal list: that
    /// list exists to recover a propagation this same method already declines to make, so adding to
    /// it here would just make the self-heal finally block re-propagate on this control's behalf,
    /// defeating the point.
    ///
    /// The caller owns the contract this leaves unenforced: <see cref="Pending"/> is set exactly as
    /// if <see cref="Invalidate(Invalidation)"/> ran, so the very next <see cref="Measure"/>/
    /// <see cref="Arrange(Rect, bool, bool)"/> call clears it in the ordinary way - but if the
    /// caller never makes that call, the bits strand: nothing else clears them, and nothing else
    /// ever notified an ancestor a fresh layout pass is owed.
    /// </remarks>
    /// <param name="value">The earliest dirty phase.</param>
    internal void InvalidateSelf(Invalidation value)
    {
        unchecked
        {
            SelectableTextInvalidationVersion++;
        }

        Pending |= Expand(value);
    }

    private void PropagateSelectableTextInvalidationVersion()
    {
        unchecked
        {
            SelectableTextInvalidationVersion++;
        }

        Parent?.PropagateSelectableTextInvalidationVersion();
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

    /// <summary>Requests the earliest UI phase on a retained descendant owned by this composite.</summary>
    /// <param name="descendant">The non-null retained descendant to invalidate.</param>
    /// <param name="impact">The validated earliest affected phase.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descendant"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="impact"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="descendant"/> is not retained
    /// beneath this control, or the attached tree is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">This control or the descendant is disposed.</exception>
    protected void InvalidateRetainedDescendant(ControlBase descendant, InvalidationImpact impact)
    {
        ArgumentNullException.ThrowIfNull(descendant);
        ValidateImpact(impact);
        VerifyMutable();

        if (!IsRetainedDescendant(descendant))
        {
            throw new InvalidOperationException("Only a retained descendant can be invalidated through its composite owner.");
        }

        descendant.Invalidate(impact);
    }

    /// <summary>Requests a repaint of this control's rendered output.</summary>
    /// <remarks>
    /// Composition-based code that does not own a subclass - an adapter, a view-model bridge, or
    /// anything wrapping a sealed concrete control - has no other public seam to ask for a
    /// repaint. This deliberately stays render-level only: measure and arrange invalidation remain
    /// <see cref="Invalidate(InvalidationImpact)"/>, reserved for a control's own derived state,
    /// to preserve phase-correctness.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void Invalidate()
    {
        VerifyMutable();
        Invalidate(Invalidation.Render);
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
        ExceptionAggregation.Capture(DisposeStyleBindings, ref failure);
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
            EnabledChanged = null;
            VisibilityChanged = null;
        }

        ExceptionAggregation.Capture(OnDisposed, ref failure);
        failure?.Throw();
    }

    /// <summary>Visits direct owned children without allocating an intermediate list.</summary>
    /// <param name="visitor">The non-null synchronous visitor.</param>
    internal void VisitChildren(Action<ControlBase> visitor) => OwnedControls.Visit(visitor);

    /// <summary>Gets the total number of direct controls across every ownership slot.</summary>
    internal int OwnedControlCount => OwnedControls.Count;

    /// <summary>Gets one direct control in slot-registration and item order.</summary>
    /// <param name="index">The valid zero-based global position.</param>
    /// <returns>The owned control at the requested position.</returns>
    internal ControlBase OwnedControlAt(int index) => OwnedControls.At(index);

    /// <summary>Returns whether this control or any owned descendant currently holds keyboard focus.</summary>
    /// <param name="control">The root of the subtree to search.</param>
    /// <returns><see langword="true"/> when <paramref name="control"/> or a descendant is focused.</returns>
    internal static bool ContainsFocused(ControlBase control)
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
    internal virtual ControlBase NavigationAt(int index) => OwnedControls.NavigationAt(index);

    /// <summary>Returns the topmost open popup descendant containing one screen-cell point.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <returns>An open popup target, or null when this subtree has none.</returns>
    internal ControlBase? HitTestPopup(Point point) =>
        CanHitTestSelf(point, requireContainment: false) ? HitTestPopupCore(point) : null;

    /// <summary>Searches elevated descendants after owner eligibility has been validated.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <returns>An open popup target, or null when this subtree has none.</returns>
    internal virtual ControlBase? HitTestPopupCore(Point point) => OwnedControls.HitTestPopup(point);

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
    internal ControlBase? HitTestPopupBranch(Point point, OwnedControlLayer slotLayer) =>
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
    internal void CommitOwnership(ControlBase? parent, OwnedControlSlot? slot)
    {
        Debug.Assert((parent is null) == (slot is null), "Parent and owning-slot state change together.");
        Debug.Assert(slot is null || ReferenceEquals(slot.Registry.Owner, parent),
            "The slot belongs to the committed parent.");
        Parent = parent;
        ReleaseInvalidStyleBindings();
        InvalidateEffectiveState();
        OwningSlot = slot;
    }

    /// <summary>Publishes one already committed parent transition.</summary>
    /// <param name="previous">The previous owner, or null.</param>
    /// <param name="current">The committed owner, or null.</param>
    internal void PublishParentChanged(ControlBase? previous, ControlBase? current) =>
        OnParentChanged(previous, current);

    /// <summary>Throws when mutation is not valid for this owner.</summary>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected internal void VerifyMutable()
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

    /// <summary>Publishes inherited input events, then runs this route member's unhandled default behavior.</summary>
    internal void InvokeDefault(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        PublishInputEvent(eventArgs);

        if (!eventArgs.IsHandled)
        {
            OnEvent(eventArgs);
        }

        if (!eventArgs.IsHandled &&
            eventArgs is PointerEventArgs { Pointer: { Action: PointerAction.Press, Cells: { } cells } pointer } &&
            pointer.Buttons.HasFlag(Buttons.Secondary) &&
            ContextMenu is { } contextMenu)
        {
            contextMenu.Show(cells.Y, cells.X);
            eventArgs.IsHandled = true;
        }

        if (!eventArgs.IsHandled &&
            eventArgs is KeyEventArgs
            {
                IsInitialKeyDown: true,
                Stroke:
                {
                    Code: Code.Tab,
                    Modifiers: var modifiers
                }
            } &&
            (modifiers & ~Modifiers.Shift) == 0)
        {
            eventArgs.IsHandled = true;
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
               text.TryGetKey(out var declared) &&
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

    /// <summary>Scrolls every enclosing armed <see cref="Container"/> so this control's own
    /// arranged bounds become visible, after keyboard traversal or an access key committed
    /// keyboard focus onto it.</summary>
    /// <remarks>
    /// <para>
    /// FocusManager calls this only for <see cref="FocusReason.Keyboard"/> - Tab and Shift+Tab
    /// traversal, and access-key-driven focus, both of which move focus to a target the caller
    /// never directly interacted with and therefore cannot already be looking at. A pointer press
    /// already proves the target was visible and clickable, and a programmatic
    /// <see cref="Focus()"/> call leaves the deliberate choice of whether to reveal its
    /// target to the caller, who can already call <see cref="Container.BringIntoView"/> directly;
    /// neither reason invokes this method.
    /// </para>
    /// <para>
    /// This walks the complete owned-parent chain up to the nearest active modal boundary -
    /// matching the boundary check every other ancestor walk in this codebase applies before
    /// crossing a modal plane - remembering the outermost <see cref="Container"/> found along the
    /// way rather than the innermost. A single call to that outermost container's own
    /// <see cref="Container.BringIntoView"/> already reveals through every intervening armed
    /// container in between, innermost to outermost, so this never needs to call it more than
    /// once.
    /// </para>
    /// </remarks>
    internal void RevealForKeyboardFocus()
    {
        Container? outermost = null;

        for (var ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ModalityOwner?.Allows(ancestor) == false)
            {
                break;
            }

            if (ancestor is Container container)
            {
                outermost = container;
            }
        }

        _ = outermost?.BringIntoView(this);
    }

    /// <summary>Publishes one already-committed focus-within entry.</summary>
    internal void PublishFocusEntered() => FocusEntered?.Invoke(this, EventArgs.Empty);

    /// <summary>Publishes one already-committed focus-within exit.</summary>
    internal void PublishFocusLeft() => FocusLeft?.Invoke(this, EventArgs.Empty);

    /// <summary>Publishes a focus-eligibility change after deferred manager cleanup commits.</summary>
    internal void PublishDeferredFocusableChange()
    {
        Debug.Assert(FocusableNotificationPending, "Only a deferred eligibility change is published.");
        FocusableNotificationPending = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFocusable)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanFocus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanTabStop)));
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

    // Deterministic structural counters, not wall-clock timing: SetSelectedState/SetCurrentState
    // are internal and not virtual, and TableCellReference is a value type, so neither subclassing
    // nor allocation-counting can distinguish an O(1) targeted state update from an O(rows *
    // columns) blanket one. A bounded call count can. Safe from cross-test interference because
    // performance tests that read these run serialized under [Collection(PerformanceGroup.Name)].

    /// <summary>Gets the total number of <see cref="SetSelectedState(bool)"/> calls across every
    /// control since process start, for instrumentation.</summary>
    internal static long SetSelectedStateCallCount;

    /// <summary>Gets the total number of <see cref="SetCurrentState(bool)"/> calls across every
    /// control since process start, for instrumentation.</summary>
    internal static long SetCurrentStateCallCount;

    /// <summary>Propagates semantic selected visual state through one realized item subtree.</summary>
    /// <param name="value">Whether the subtree is selected.</param>
    internal void SetSelectedState(bool value)
    {
        SetSelectedStateCallCount++;
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
        SetCurrentStateCallCount++;
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

    /// <summary>Runs target-specific default behavior after inherited input event publication.</summary>
    /// <param name="eventArgs">The non-null event state and typed payload.</param>
    protected virtual void OnEvent(RoutedEventArgs eventArgs) =>
        ArgumentNullException.ThrowIfNull(eventArgs);

    /// <summary>Publishes the inherited convenience event before concrete default behavior.</summary>
    /// <param name="eventArgs">The non-null routed event and immutable input payload.</param>
    private void PublishInputEvent(RoutedEventArgs eventArgs)
    {
        switch (eventArgs)
        {
            case PointerEventArgs pointer:
                switch (pointer.Pointer.Action)
                {
                    case PointerAction.Press:
                        if ((pointer.Pointer.Buttons & Buttons.Primary) != 0)
                        {
                            PointerPressed?.Invoke(this, pointer);
                        }

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
                if (key.IsKeyDown)
                {
                    KeyDown?.Invoke(this, key);
                }
                else if (key.IsKeyUp)
                {
                    KeyUp?.Invoke(this, key);
                }

                break;
            default:
                break;
        }
    }

    /// <summary>Responds after this control's keyboard-focus state changes.</summary>
    /// <param name="focused">The newly committed focus state.</param>
    protected virtual void OnFocusChanged(bool focused)
    {
        Debug.Assert(!IsDisposed, "A disposed control cannot change focus state.");

        if (!focused && TextSelectionPhase == Text.TextSelectionGesturePhase.Selecting)
        {
            _textSelectionGesture?.Cancel(releaseCapture: true);
        }
    }

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
    protected virtual void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        Debug.Assert(Enum.IsDefined(reason), "Capture-loss reasons are validated internally.");
        _textSelectionGesture?.Cancel(releaseCapture: false);
    }

    /// <summary>Responds after this control's direct ownership changes.</summary>
    /// <param name="previous">The previous owner, or null.</param>
    /// <param name="current">The committed owner, or null.</param>
    protected virtual void OnParentChanged(ControlBase? previous, ControlBase? current)
    {
        _ = previous;
        _ = current;
        Debug.Assert(!IsDisposed, "A disposed control cannot change parent.");
        ParentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Releases derived transient state when this control becomes unavailable.</summary>
    /// <param name="reason">The precise unavailability reason.</param>
    protected virtual void OnUnavailable(ReleaseReason reason)
    {
        Debug.Assert(Enum.IsDefined(reason), "Unavailable reasons are validated internally.");
        _textSelectionGesture?.Cancel(releaseCapture: false);
    }

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
        RenderTextSelectionAdornment(canvas);
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
            return Bounds.ExpandVisualBounds(shadow.IsVisible, shadow.Mode, shadow.Offset);
        }
    }

    /// <summary>Gets whether this control forms a hard clip for descendant visual overflow.</summary>
    /// <remarks>Own visual overflow remains eligible for propagation through the control's parent.</remarks>
    internal virtual bool ClipsDescendantVisualOverflow => false;

    /// <summary>Adds ordered retained children that contribute semantic selectable text.</summary>
    /// <param name="children">The caller-owned destination receiving borrowed child references.</param>
    /// <returns>True when this control is an aggregate node; false when it is a leaf.</returns>
    /// <remarks>
    /// Aggregate overrides expose only semantic presentation children and must not include generated
    /// chrome. The selectable-text collector consumes this seam without constructing intermediate
    /// snapshots.
    /// </remarks>
    internal virtual bool AddSelectableTextChildren(List<ControlBase> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        return false;
    }

    /// <summary>Gets the effective absolute clipping aperture inherited from retained ancestors.</summary>
    /// <returns>The finite absolute cell rectangle that may contain this control's visual output.</returns>
    internal Rect GetSelectableTextInheritedClip()
    {
        if (StartsPopupRenderBranch)
        {
            return RootBounds(Bounds);
        }

        if (Parent is null)
        {
            return Bounds;
        }

        var inherited = Parent.GetSelectableTextInheritedClip();
        return Parent.ResolveSelectableTextDescendantClip(inherited);
    }

    /// <summary>Gets the authoritative inherited selectable-text clip for one retained descendant.</summary>
    /// <param name="descendant">The non-null retained descendant whose aperture is requested.</param>
    /// <returns>The finite absolute cell rectangle that may contain the descendant's visual output.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descendant"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="descendant"/> is not this control or its descendant.</exception>
    protected internal Rect GetDescendantSelectableTextInheritedClip(ControlBase descendant)
    {
        ArgumentNullException.ThrowIfNull(descendant);

        for (var current = descendant; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, this))
            {
                return descendant.GetSelectableTextInheritedClip();
            }
        }

        throw new ArgumentException("The selectable-text clip target must be a retained descendant.", nameof(descendant));
    }

    /// <summary>Gets whether the active modal plane permits traversal from this control to one ancestor.</summary>
    /// <param name="ancestor">The non-null proposed ancestor traversal target.</param>
    /// <returns>True when modality is inactive or the ancestor belongs to the active plane.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ancestor"/> is null.</exception>
    protected internal bool AllowsModalAncestor(ControlBase ancestor)
    {
        ArgumentNullException.ThrowIfNull(ancestor);
        return ModalityOwner?.Allows(ancestor) is not false;
    }

    /// <summary>Gets whether this control begins a branch rendered from the elevated root plane.</summary>
    /// <remarks>
    /// The predicate deliberately matches popup rendering by resolving both the owning slot layer
    /// and <see cref="IntrinsicLayer"/>. It therefore covers intrinsic popup surfaces, ordinary
    /// controls promoted by popup slots, and each independently nested popup boundary.
    /// </remarks>
    internal bool StartsPopupRenderBranch =>
        OwningSlot is { } slot &&
        ResolveOwnedLayer(slot.Options.Layer) == OwnedControlLayer.Popup;

    /// <summary>Resolves the absolute aperture inherited by this control's semantic descendants.</summary>
    /// <param name="inheritedClip">The finite clipping aperture inherited by this control.</param>
    /// <returns>The effective aperture after ordinary child and visual-overflow clipping.</returns>
    internal virtual Rect ResolveSelectableTextDescendantClip(Rect inheritedClip)
    {
        var descendantBounds = DescendantRenderBounds;
        var clip = ClipsChildren ? inheritedClip.Intersect(descendantBounds) : inheritedClip;
        return ClipsDescendantVisualOverflow ? clip.Intersect(descendantBounds) : clip;
    }

    /// <summary>Gets whether this control's own paint has an effect a copied cell region cannot
    /// reproduce, so it always runs the complete paint sequence instead of a render-clean copy.</summary>
    /// <remarks>Overridden by <see cref="Display.Image"/>: <see cref="TerminalCanvas.DrawImage"/>
    /// records a backend-neutral placement alongside the cells it paints, and copying previous cells
    /// never replays that call.</remarks>
    internal virtual bool RequiresCompleteRender => false;

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
            result |= VisualState.IsPointerOver;
        }

        if (ContainsFocus)
        {
            result |= VisualState.FocusWithin;
        }

        if (IsFocused)
        {
            result |= VisualState.Focused;
        }

        if (IsPressedState)
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

    /// <summary>Gets whether the control currently holds pressed interaction state.</summary>
    /// <remarks>
    /// Defaults to the pointer/keyboard press tracked by the framework's own gesture behaviors; a
    /// control with its own press concept - continuous drag tracking rather than one-shot
    /// activation, for example - overrides this to drive <see cref="VisualState.Pressed"/> without
    /// depending on <see cref="PressBehavior"/> or <see cref="DragBehavior"/>. This is the supported
    /// seam for participating in pressed styling without overriding <see cref="GetAppearanceState"/>.
    /// </remarks>
    protected virtual bool IsPressedState => _interactionState.IsPressed;

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
        if (AppearanceStates.StateCanChangeChromeGeometry)
        {
            return Invalidation.Measure;
        }

        foreach (var appearance in _appearanceSets.Values)
        {
            if (AppearanceStates.ChangesChromeGeometry(appearance))
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
    protected static InvalidationImpact MaximumImpact(InvalidationImpact left, InvalidationImpact right) =>
        (int) left >= (int) right ? left : right;

    /// <summary>Measures the fixed cell columns two edge-pinned affixes reserve beside a caption.</summary>
    /// <param name="start">The optional leading affix.</param>
    /// <param name="end">The optional trailing affix.</param>
    /// <param name="gap">The non-negative cell gap between a present affix and the caption, from
    /// the hosting style's <see cref="InputStyle.AffixGap"/> (or an equivalent control-owned
    /// value).</param>
    /// <returns>Zero cells for a null affix - zero cost when unused - otherwise the affix's own
    /// resolved content width plus <paramref name="gap"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="gap"/> is negative.</exception>
    protected AffixMetrics MeasureAffixes(Affix? start, Affix? end, [NonNegativeValue] int gap)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gap);

        var startCells = start is { } startAffix ? checked(ResolveAffixCells(startAffix, out _) + gap) : 0;
        var endCells = end is { } endAffix ? checked(ResolveAffixCells(endAffix, out _) + gap) : 0;
        return new AffixMetrics(startCells, endCells);
    }

    /// <summary>Deflates a content box by the reserved affix columns, leaving the middle box a
    /// caption arranges into - outside the affix columns, inside the face.</summary>
    /// <param name="contentBox">The undeflated content box, before affix reservation.</param>
    /// <param name="metrics">The affix metrics <see cref="MeasureAffixes"/> already measured.</param>
    /// <returns>The saturated middle box; never negative on either axis.</returns>
    protected static Rect DeflateForAffixes(Rect contentBox, AffixMetrics metrics) =>
        new Thickness(metrics.StartCells, 0, metrics.EndCells, 0).Deflate(contentBox);

    /// <summary>Draws up to two edge-pinned affixes into a content box, live against current
    /// bounds.</summary>
    /// <remarks>
    /// Overflow is decided here, against <paramref name="contentBox"/> as arranged right now, not
    /// against the <paramref name="metrics"/> a possibly-stale measure pass reserved - matching how
    /// <c>MenuItem</c> gates its shortcut column against live bounds. The caption already shrinks
    /// first, by construction: <see cref="DeflateForAffixes"/> saturates its middle box at zero
    /// width instead of going negative. When what remains cannot hold both affixes, the end affix
    /// drops whole before the start affix does; neither ever draws a partial cluster. Drawing goes
    /// through the string-based <c>canvas.Draw</c> path, which clips at cluster boundaries, and each
    /// affix falls back to its validated printable-ASCII <see cref="Affix.Fallback"/> whenever its
    /// own <see cref="Affix.Content"/> does not resolve to one clean grapheme cluster under the
    /// tree's live <see cref="CellPolicy"/> - mirroring the value-or-fallback rule
    /// <see cref="CellGlyphResolver"/> uses for theme-owned glyphs.
    /// </remarks>
    /// <param name="canvas">The frame-owned canvas clipped to <see cref="VisualBounds"/>.</param>
    /// <param name="contentBox">The undeflated content box affixes are pinned inside.</param>
    /// <param name="metrics">The affix metrics <see cref="MeasureAffixes"/> already measured.</param>
    /// <param name="start">The optional leading affix.</param>
    /// <param name="end">The optional trailing affix.</param>
    /// <param name="style">The row's resolved style; a null <see cref="Affix.Color"/> inherits its
    /// foreground.</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> is null.</exception>
    protected void RenderAffixes(
        TerminalCanvas canvas,
        Rect contentBox,
        AffixMetrics metrics,
        Affix? start,
        Affix? end,
        TerminalStyle style)
    {
        Debug.Assert(!IsDisposed, "A disposed control cannot render affixes.");

        if ((metrics.StartCells == 0 && metrics.EndCells == 0) || contentBox.Width <= 0 || contentBox.Height <= 0)
        {
            return;
        }

        // Start claims space first and end only gets what is left over, so a box too narrow for
        // both drops the end affix while the start affix keeps drawing - the documented priority
        // order (caption first, then end, then start) requires end to be the one sacrificed first.
        var drawStart = false;
        var startWidth = 0;
        string? startText = null;

        if (start is { } startAffix)
        {
            startWidth = ResolveAffixCells(startAffix, out var startUsesFallback);
            startText = startUsesFallback ? startAffix.Fallback : startAffix.Content;
            drawStart = startWidth > 0 && startWidth <= contentBox.Width;
        }

        var remaining = contentBox.Width - (drawStart ? startWidth : 0);
        var drawEnd = false;
        var endWidth = 0;
        string? endText = null;

        if (end is { } endAffix)
        {
            endWidth = ResolveAffixCells(endAffix, out var endUsesFallback);
            endText = endUsesFallback ? endAffix.Fallback : endAffix.Content;
            drawEnd = endWidth > 0 && endWidth <= remaining;
        }

        if (drawStart)
        {
            var foreground = start!.Value.Color is { } startColor ? ResolveColor(startColor, Theme) : style.Foreground;
            _ = canvas.Draw(startText!.AsSpan(), new Point(contentBox.X, contentBox.Y), style.WithForeground(foreground));
        }

        if (drawEnd)
        {
            var foreground = end!.Value.Color is { } endColor ? ResolveColor(endColor, Theme) : style.Foreground;
            var origin = new Point(contentBox.Right - endWidth, contentBox.Y);
            _ = canvas.Draw(endText!.AsSpan(), origin, style.WithForeground(foreground));
        }
    }

    /// <summary>Grades an affix property change by its resolved cell-width delta: null-to-set or
    /// set-to-null requires <see cref="InvalidationImpact.Measure"/> (the reserved width changes
    /// between zero and non-zero cells), a same-width content or color swap requires only
    /// <see cref="InvalidationImpact.Render"/>, and a different resolved width requires
    /// <see cref="InvalidationImpact.Measure"/> again. Keeps an animated affix (a spinner swapping
    /// frames, for example) a per-frame repaint instead of a per-frame remeasure of the subtree.</summary>
    /// <param name="previous">The committed affix value.</param>
    /// <param name="current">The proposed replacement affix value.</param>
    /// <returns>The earliest phase the change requires.</returns>
    protected InvalidationImpact GetAffixChangeImpact(Affix? previous, Affix? current)
    {
        if (previous.HasValue != current.HasValue)
        {
            return InvalidationImpact.Measure;
        }

        if (!previous.HasValue)
        {
            return InvalidationImpact.None;
        }

        var previousCells = ResolveAffixCells(previous.GetValueOrDefault(), out _);
        var currentCells = ResolveAffixCells(current.GetValueOrDefault(), out _);
        return previousCells != currentCells ? InvalidationImpact.Measure : InvalidationImpact.Render;
    }

    /// <summary>Resolves the active theme's affix gap from the shared input style.</summary>
    /// <remarks>
    /// The gap lives on <see cref="InputStyle"/> rather than on the control, so a control that never
    /// acquires its own <see cref="StyleSlot{TStyle}"/> reads it from the ambient theme's shared
    /// "input" style key directly instead of forwarding it through an <see cref="InputStyle"/>-derived
    /// style of its own.
    /// </remarks>
    /// <returns>The cell gap between a present affix and the content it sits beside.</returns>
    protected int ResolveAffixGap()
    {
        TrackThemeStructuralDependency(ThemeStructuralDependency.InputAffixGap);
        return (Theme ?? ThemeCatalog.Dark).GetStyleSet(InputStyle.Default).Normal.AffixGap;
    }

    /// <summary>Records one non-appearance root Theme value used by this control.</summary>
    /// <param name="dependency">The defined structural dependency.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dependency"/> is unknown.</exception>
    private protected void TrackThemeStructuralDependency(ThemeStructuralDependency dependency)
    {
        if (dependency == ThemeStructuralDependency.None ||
            (dependency & ~(ThemeStructuralDependency.InputAffixGap |
                            ThemeStructuralDependency.InputDropDownGlyph |
                            ThemeStructuralDependency.PopupAnchorGlyphs)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dependency), dependency, "The Theme structural dependency is unknown.");
        }

        _themeStructuralDependencies |= dependency;
    }

    /// <summary>Resolves the printable cell width an affix reserves under the tree's live
    /// <see cref="CellPolicy"/>, and reports which of <see cref="Affix.Content"/> or
    /// <see cref="Affix.Fallback"/> that width belongs to.</summary>
    /// <remarks>
    /// Deliberately not <see cref="CellGlyphResolver"/>: that resolver hard-requires exactly one
    /// cell, by design, for code-owned chrome glyphs. An affix reserves whatever it measures, zero
    /// to two cells, so this mirrors the same value-or-fallback decision rule against a variable
    /// budget instead of a fixed one.
    /// </remarks>
    /// <param name="affix">The affix to resolve.</param>
    /// <param name="usesFallback">Receives whether <see cref="Affix.Fallback"/> must be drawn in
    /// place of <see cref="Affix.Content"/>.</param>
    /// <returns>The resolved printable cell width.</returns>
    [Pure]
    private int ResolveAffixCells(Affix affix, out bool usesFallback)
    {
        var measurement = Terminal.Unicode.Width.Measure(affix.Content, CellPolicy.AmbiguousWidth);

        if (measurement is { Graphemes: 1, Controls: 0 })
        {
            usesFallback = false;
            return measurement.Cells;
        }

        usesFallback = true;
        return Terminal.Unicode.Width.Measure(affix.Fallback, CellPolicy.AmbiguousWidth).Cells;
    }

    /// <summary>Maps one validated public change impact to the complete internal dirty-phase closure.</summary>
    /// <param name="impact">The validated earliest affected UI phase.</param>
    /// <returns>The internal dirty phases requested by the change.</returns>
    [Pure]
    internal static Invalidation InvalidationFor(InvalidationImpact impact) => impact switch
    {
        InvalidationImpact.None => Invalidation.None,
        InvalidationImpact.Render => Invalidation.Render,
        InvalidationImpact.Arrange => Invalidation.Arrange | Invalidation.Render,
        InvalidationImpact.Measure => Invalidation.All,
        _ => throw new UnreachableException()
    };

    [Pure]
    private static Invalidation Expand(Invalidation value) => value switch
    {
        Invalidation.None => Invalidation.None,
        Invalidation.Render => Invalidation.Render,
        Invalidation.Arrange => Invalidation.Arrange | Invalidation.Render,
        Invalidation.Measure => Invalidation.All,
        Invalidation.All => Invalidation.All,
        _ => value & Invalidation.All
    };

    [Pure]
    private static int Align(
        int origin,
        int available,
        int desired,
        HorizontalAlignment alignment) => alignment switch
        {
            HorizontalAlignment.Left or HorizontalAlignment.Stretch => origin,
            HorizontalAlignment.Center => origin.SaturatingAdd((available - desired) / 2),
            HorizontalAlignment.Right => origin.SaturatingAdd(available - desired),
            _ => throw new UnreachableException()
        };

    [Pure]
    private static int Align(
        int origin,
        int available,
        int desired,
        VerticalAlignment alignment) => alignment switch
        {
            VerticalAlignment.Top or VerticalAlignment.Stretch => origin,
            VerticalAlignment.Center => origin.SaturatingAdd((available - desired) / 2),
            VerticalAlignment.Bottom => origin.SaturatingAdd(available - desired),
            _ => throw new UnreachableException()
        };

    [Pure]
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

    [Pure]
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
            LengthKind.Auto => intrinsic.SaturatingAdd(inset),
            LengthKind.Cells => (int) length.Value,
            LengthKind.Percent => slot.HasValue
                ? ResolvePercent(slot.Value, length.Value)
                : intrinsic.SaturatingAdd(inset),
            LengthKind.Star => slot.HasValue
                ? Math.Max(0, slot.Value - margin)
                : intrinsic.SaturatingAdd(inset),
            _ => throw new UnreachableException()
        };
        var clamped = Math.Clamp(requested, minimum, maximum);

        return slot.HasValue
            ? Math.Min(Math.Max(0, slot.Value - margin), clamped)
            : clamped;
    }

    [Pure]
    private static int? ResolveContentAxis(
        Length length,
        int? slot,
        int margin,
        int inset,
        int minimum,
        int maximum)
    {
        int? border = length.Kind switch
        {
            LengthKind.Auto => slot.HasValue ? Math.Max(0, slot.Value - margin) : null,
            LengthKind.Cells => (int) length.Value,
            LengthKind.Percent => slot.HasValue ? ResolvePercent(slot.Value, length.Value) : null,
            LengthKind.Star => slot.HasValue ? Math.Max(0, slot.Value - margin) : null,
            _ => throw new UnreachableException()
        };

        // Min/Max must bound the border box handed to MeasureOverride the same way
        // ResolveMeasureAxis bounds the final resolved size - otherwise wrap-capable content
        // measures against the unclamped slot and the later arrange-time clamp silently clips
        // the surplus lines it never accounted for. An unresolved border (Auto,
        // Percent, or Star with no slot) still respects an explicit MaxWidth/MaxHeight, since a
        // finite ceiling is knowable even without a slot; MinWidth/MinHeight cannot expand it
        // without a slot to expand into, so they are left for ResolveDesiredSize to apply to
        // the reported size after measurement.
        border = border.HasValue
            ? Math.Clamp(border.Value, minimum, maximum)
            : maximum == int.MaxValue ? null : maximum;

        if (!border.HasValue)
        {
            return null;
        }

        var available = slot.HasValue ? Math.Max(0, slot.Value - margin) : int.MaxValue;
        return Math.Max(0, Math.Min(border.Value, available) - inset);
    }

    [Pure]
    private static int ResolvePercent(int value, double percent)
    {
        var result = Math.Round(value * percent / 100, MidpointRounding.AwayFromZero);
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }


    /// <summary>Walks the parent chain and returns the first ancestor of the given type, or null.</summary>
    protected T? FindAncestor<T>() where T : ControlBase
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
    [NotifyPropertyChangedInvocator]
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

    /// <summary>Commits and publishes one property while advancing a caller-owned transition
    /// version that later dependent work can use to reject a superseded callback continuation.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="field">The current backing field.</param>
    /// <param name="value">The validated replacement value.</param>
    /// <param name="impact">The validated earliest affected phase.</param>
    /// <param name="version">The caller-owned version for this logical property.</param>
    /// <param name="commitVersion">Receives this transition's version, or the current version for a no-op.</param>
    /// <param name="propertyName">The non-empty property name supplied by the compiler.</param>
    /// <returns>Whether a changed value was committed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="impact"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NotifyPropertyChangedInvocator]
    private protected bool SetVersionedProperty<T>(
        ref T field,
        T value,
        InvalidationImpact impact,
        ref long version,
        out long commitVersion,
        [CallerMemberName] string? propertyName = null)
    {
        ValidateImpact(impact);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        VerifyMutable();

        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            commitVersion = version;
            return false;
        }

        field = value;
        commitVersion = ++version;
        Invalidate(InvalidationFor(impact));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    /// <summary>Gets whether a versioned property transition still owns dependent work.</summary>
    private protected static bool IsVersionedPropertyCurrent<T>(
        T field,
        T value,
        long version,
        long commitVersion) =>
        version == commitVersion && EqualityComparer<T>.Default.Equals(field, value);

    /// <summary>Commits and publishes one property, then runs required dependent work even when a
    /// property observer throws, rethrowing the first failure after the continuation completes.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="field">The current backing field.</param>
    /// <param name="value">The validated replacement value.</param>
    /// <param name="impact">The validated earliest affected phase.</param>
    /// <param name="continuation">Required invariant repair or dependent-state synchronization.</param>
    /// <param name="propertyName">The non-empty property name supplied by the compiler.</param>
    /// <returns>Whether a changed value was committed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="continuation"/> or
    /// <paramref name="propertyName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="impact"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NotifyPropertyChangedInvocator]
    private protected bool SetPropertyAndContinue<T>(
        ref T field,
        T value,
        InvalidationImpact impact,
        Action continuation,
        [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        ValidateImpact(impact);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        VerifyMutable();

        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(() => NotifyPropertyChanged(propertyName, impact), ref failure);
        ExceptionAggregation.Capture(continuation, ref failure);
        failure?.Throw();
        return true;
    }

    /// <summary>Commits one property, synchronizes its dependent retained state, and only then
    /// publishes the still-current transition.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="field">The current backing field.</param>
    /// <param name="value">The validated replacement value.</param>
    /// <param name="impact">The validated earliest affected phase.</param>
    /// <param name="synchronize">Updates retained state derived from the committed value.</param>
    /// <param name="propertyName">The non-empty property name supplied by the compiler.</param>
    /// <returns>Whether a changed value was committed.</returns>
    /// <remarks>
    /// The per-property generation makes a nested owner transition authoritative even when it
    /// changes away and back. A dependent-state callback that commits a newer owner value also
    /// suppresses this transition's later property notification. Synchronization and publication
    /// are both attempted when either throws, with the earliest failure rethrown afterward.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="synchronize"/> or
    /// <paramref name="propertyName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="propertyName"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="impact"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NotifyPropertyChangedInvocator]
    private protected bool SetPropertyAndSynchronize<T>(
        ref T field,
        T value,
        InvalidationImpact impact,
        Action synchronize,
        [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(synchronize);
        ValidateImpact(impact);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        VerifyMutable();

        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        _synchronizedPropertyVersions ??= [];
        _ = _synchronizedPropertyVersions.TryGetValue(propertyName, out var version);
        version++;
        _synchronizedPropertyVersions[propertyName] = version;
        Invalidate(InvalidationFor(impact));
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(synchronize, ref failure);

        if (_synchronizedPropertyVersions[propertyName] == version &&
            EqualityComparer<T>.Default.Equals(field, value))
        {
            ExceptionAggregation.Capture(
                () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)),
                ref failure);
        }

        failure?.Throw();
        return true;
    }

    /// <summary>Initializes the single primary complete-style slot owned by this control.</summary>
    /// <typeparam name="TStyle">The small immutable complete style value.</typeparam>
    /// <param name="definition">The immutable primary-style policy.</param>
    /// <param name="changed">An optional callback after a changed resolved style commits.</param>
    /// <returns>The initialized slot used by the public Style and ActualStyle properties.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A primary slot was already initialized, or
    /// <paramref name="definition"/> describes a named part rather than a primary control or aggregate.</exception>
    protected StyleSlot<TStyle> InitializeStyle<TStyle>(
        StyleDefinition<TStyle> definition,
        Action<TStyle, TStyle>? changed = null)
        where TStyle : ControlStyle
    {
        ArgumentNullException.ThrowIfNull(definition);
        VerifyMutable();

        if (_styleSlots?.ContainsKey("Style") == true)
        {
            throw new InvalidOperationException("A control can initialize only one primary style slot.");
        }

        if (definition.Kind == StyleDefinitionKind.Part)
        {
            throw new InvalidOperationException("A part-style definition cannot initialize a primary Style slot.");
        }

        var slot = new StyleSlot<TStyle>(
            this,
            definition,
            "Style",
            "ActualStyle",
            ownsAppearance: definition.IsControl,
            changed);
        var registration = new StyleSlotRegistration<TStyle>(slot);
        if (definition.IsControl)
        {
            _primaryStyle = registration;
        }
        RegisterStyleSlot(registration);
        return slot;
    }

    /// <summary>Initializes one named secondary style slot owned by this control.</summary>
    /// <typeparam name="TStyle">The small immutable complete style value.</typeparam>
    /// <param name="definition">The immutable fallback and comparison policy.</param>
    /// <param name="propertyName">The conventional local property name ending in Style.</param>
    /// <param name="changed">An optional callback after a changed resolved style commits.</param>
    /// <returns>The initialized secondary slot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="propertyName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="propertyName"/> is empty, Style, or does not end in Style.</exception>
    /// <exception cref="InvalidOperationException">The property name is already registered, or
    /// <paramref name="definition"/> does not describe a named part.</exception>
    protected StyleSlot<TStyle> InitializePartStyle<TStyle>(
        StyleDefinition<TStyle> definition,
        string propertyName,
        Action<TStyle, TStyle>? changed = null)
        where TStyle : ControlStyle
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        VerifyMutable();

        if (propertyName == "Style" || !propertyName.EndsWith("Style", StringComparison.Ordinal))
        {
            throw new ArgumentException("A part-style property must be a named FooStyle property.", nameof(propertyName));
        }

        if (definition.Kind != StyleDefinitionKind.Part)
        {
            throw new InvalidOperationException("Only a part-style definition can initialize a named part slot.");
        }

        var slot = new StyleSlot<TStyle>(
            this,
            definition,
            propertyName,
            $"Actual{propertyName}",
            ownsAppearance: false,
            changed);
        RegisterStyleSlot(new StyleSlotRegistration<TStyle>(slot));
        return slot;
    }

    /// <summary>Binds one owned slot to a matching registered slot on a retained descendant.</summary>
    /// <typeparam name="TStyle">The exact complete style value type.</typeparam>
    /// <param name="source">The slot owned by this control.</param>
    /// <param name="target">The retained target control.</param>
    /// <param name="targetPropertyName">The target local property name.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="targetPropertyName"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Ownership, ancestry, type, duplication, or graph validation fails.</exception>
    /// <remarks>The binding releases when the target leaves the source owner's retained subtree.
    /// Source mutations preflight and commit the complete downstream graph before callbacks run.</remarks>
    protected void BindStyle<TStyle>(
        StyleSlot<TStyle> source,
        ControlBase target,
        string targetPropertyName = "Style")
        where TStyle : ControlStyle
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrEmpty(targetPropertyName);
        VerifyMutable();

        if (!ReferenceEquals(source.Owner, this))
        {
            throw new InvalidOperationException("The source style slot is not owned by this control.");
        }

        if (ReferenceEquals(this, target))
        {
            throw new InvalidOperationException("A style slot cannot bind to its own control.");
        }

        if (!IsRetainedDescendant(target))
        {
            throw new InvalidOperationException("Style slots can bind only to retained descendants.");
        }

        if (target._styleSlots is null || !target._styleSlots.TryGetValue(targetPropertyName, out var candidate))
        {
            throw new InvalidOperationException($"The target has no registered {targetPropertyName} style slot.");
        }

        if (candidate.Slot is not StyleSlot<TStyle> targetSlot)
        {
            throw new InvalidOperationException("The source and target style-slot types do not match.");
        }

        source.Bind(targetSlot);
    }

    private bool IsRetainedDescendant(ControlBase target)
    {
        for (var current = target.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

    private void ReleaseInvalidStyleBindings()
    {
        if (_styleSlots is null)
        {
            return;
        }

        foreach (var slot in _styleSlots.Values)
        {
            slot.ReleaseInvalidBinding();
        }
    }

    private void RegisterStyleSlot(StyleSlotBase slot)
    {
        _styleSlots ??= new Dictionary<string, StyleSlotBase>(StringComparer.Ordinal);
        if (!_styleSlots.TryAdd(slot.PropertyName, slot))
        {
            throw new InvalidOperationException($"The {slot.PropertyName} style slot is already initialized.");
        }
    }

    /// <summary>Commits one registered primary slot value atomically.</summary>
    internal void CommitStyle<TStyle>(StyleSlot<TStyle> slot, TStyle? value, bool fromBinding = false)
        where TStyle : ControlStyle
    {
        ArgumentNullException.ThrowIfNull(slot);
        VerifyMutable();

        if (!ReferenceEquals(slot.Owner, this))
        {
            throw new InvalidOperationException("The style slot is not owned by this control.");
        }

        if (!fromBinding && slot.Source is not null)
        {
            throw new InvalidOperationException("A bound style slot is owned by its upstream binding.");
        }

        if (EqualityComparer<TStyle?>.Default.Equals(slot.LocalValue, value))
        {
            return;
        }

        var commits = new List<StyleCommit<TStyle>>();

        foreach (var target in slot.GetPropagationGraph())
        {
            if (!EqualityComparer<TStyle?>.Default.Equals(target.LocalValue, value))
            {
                commits.Add(target.Owner.PrepareStyleCommit(target, value));
            }
        }

        var ownerVersions = new Dictionary<ControlBase, long>();
        foreach (var commit in commits)
        {
            var owner = commit.Slot.Owner;
            if (!ownerVersions.TryGetValue(owner, out var ownerVersion))
            {
                ownerVersion = owner.AdvanceStylePublicationVersion();
                ownerVersions.Add(owner, ownerVersion);
            }

            commit.OwnerVersion = ownerVersion;
            owner.ApplyStyleCommit(commit);
        }

        ExceptionDispatchInfo? failure = null;
        foreach (var commit in commits)
        {
            commit.Slot.Owner.PublishStyleCommit(commit, ref failure);
        }

        failure?.Throw();
    }

    private StyleCommit<TStyle> PrepareStyleCommit<TStyle>(StyleSlot<TStyle> slot, TStyle? value)
        where TStyle : ControlStyle
    {
        VerifyMutable();

        if (!ReferenceEquals(slot.Owner, this))
        {
            throw new InvalidOperationException("The style slot is not owned by this control.");
        }

        var definition = slot.Definition;
        var previousStyle = definition.Resolve(slot.LocalValue, Theme);
        var currentStyle = definition.Resolve(value, Theme);
        var styleImpact = MaximumImpact(
            definition.Compare(previousStyle, Theme, currentStyle, Theme),
            StyleSlot<TStyle>.GetSemanticValueImpact(previousStyle, Theme, currentStyle, Theme));
        ValidateImpact(styleImpact);
        var impact = styleImpact;
        ResolvedAppearance previousAppearance = default;
        ResolvedAppearance currentAppearance = default;
        var ambientFaceChanged = false;
        if (slot.OwnsAppearance)
        {
            var parentAmbientFace = AppearanceSnapshot.ResolveParentAmbient(Parent);
            var appearanceState = GetAppearanceState();
            var previousProfile = slot.GetAppearance(previousStyle, slot.LocalValue is not null, Theme);
            var currentProfile = slot.GetAppearance(currentStyle, value is not null, Theme);
            impact = MaximumImpact(
                impact,
                this.GetImpact(Theme, previousProfile, Theme, currentProfile, parentAmbientFace, parentAmbientFace));
            previousAppearance = this.ResolveSnapshot(appearanceState, Theme, previousProfile, parentAmbientFace);
            currentAppearance = this.ResolveSnapshot(appearanceState, Theme, currentProfile, parentAmbientFace);

            // Compared RESOLVED, and in the state descendants actually inherit. Two authored faces
            // that differ can resolve to the same thing - a semantic reference against the literal
            // the theme maps it to, or any authored foreground at all on a transparent control that
            // inherits its parent's. Comparing the raw values would render-invalidate a whole
            // subtree for a change no descendant can observe.
            var ambientState = AmbientAppearanceState;
            ambientFaceChanged =
                this.ResolveSnapshot(ambientState, Theme, previousProfile, parentAmbientFace).Face !=
                this.ResolveSnapshot(ambientState, Theme, currentProfile, parentAmbientFace).Face;
        }

        return new StyleCommit<TStyle>(
            slot,
            value,
            previousStyle,
            currentStyle,
            impact,
            previousAppearance,
            currentAppearance,
            ambientFaceChanged);
    }

    private void ApplyStyleCommit<TStyle>(StyleCommit<TStyle> commit)
        where TStyle : ControlStyle
    {
        var slot = commit.Slot;
        slot.LocalValue = commit.Value;
        slot.ClearCache();
        commit.SlotVersion = slot.AdvanceCommitVersion();

        // A slot that owns appearance replaces the face descendants inherit. The bare cache clear
        // leaves the subtree with no scheduled frame, which is invisible right up until this
        // control's own active state masks the change: a disabled Button whose `disabled` overlay
        // pins the foreground resolves byte-identically, so Compare returns None and Invalidate is
        // a no-op - while its transparent Text child now resolves a different foreground from a
        // cleared cache and never repaints.
        if (commit.AmbientFaceChanged)
        {
            InvalidateSubtreeAmbientAppearance();
        }
        else
        {
            InvalidateSubtreeResolvedStyleCache();
        }

        Invalidate(InvalidationFor(commit.Impact));
    }

    private void PublishStyleCommit<TStyle>(
        StyleCommit<TStyle> commit,
        ref ExceptionDispatchInfo? failure)
        where TStyle : ControlStyle
    {
        if (!IsCurrentStyleCommit(commit))
        {
            return;
        }

        var slot = commit.Slot;
        ExceptionAggregation.Capture(
            () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(slot.PropertyName)),
            ref failure);

        if (!IsCurrentStyleCommit(commit) || !commit.ResolvedStyleChanged)
        {
            return;
        }

        ExceptionAggregation.Capture(
            () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(slot.ActualPropertyName)),
            ref failure);

        if (!IsCurrentStyleCommit(commit))
        {
            return;
        }

        if (slot.OwnsAppearance)
        {
            ExceptionAggregation.Capture(
                () => PublishAppearanceChanges(commit.PreviousAppearance, commit.CurrentAppearance),
                ref failure);
        }

        if (IsCurrentStyleCommit(commit))
        {
            ExceptionAggregation.Capture(
                () => slot.PublishChanged(commit.PreviousStyle, commit.CurrentStyle),
                ref failure);
        }
    }

    [Pure]
    private bool IsCurrentStyleCommit<TStyle>(StyleCommit<TStyle> commit)
        where TStyle : ControlStyle =>
        commit.OwnerVersion == _stylePublicationVersion &&
        commit.Slot.IsCurrentVersion(commit.SlotVersion);

    /// <summary>Advances the generation that invalidates stale style and Theme publication.</summary>
    private long AdvanceStylePublicationVersion() => unchecked(++_stylePublicationVersion);

    /// <summary>Calculates one registered slot's exact Theme-transition impact.</summary>
    internal InvalidationImpact GetStyleThemeImpact<TStyle>(
        StyleSlot<TStyle> slot,
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace)
        where TStyle : ControlStyle
    {
        var definition = slot.Definition;
        var previousStyle = definition.Resolve(slot.LocalValue, previous);
        var currentStyle = definition.Resolve(slot.LocalValue, current);
        var impact = MaximumImpact(
            definition.Compare(previousStyle, previous, currentStyle, current),
            StyleSlot<TStyle>.GetSemanticValueImpact(previousStyle, previous, currentStyle, current));
        ValidateImpact(impact);
        return slot.OwnsAppearance
            ? MaximumImpact(
                impact,
                this.GetImpact(
                    previous,
                    slot.GetAppearance(previousStyle, slot.LocalValue is not null, previous),
                    current,
                    slot.GetAppearance(currentStyle, slot.LocalValue is not null, current),
                    previousParentAmbientFace,
                    currentParentAmbientFace))
            : impact;
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
    [NotifyPropertyChangedInvocator]
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
    [NotifyPropertyChangedInvocator]
    protected void NotifyPropertyChanged(string propertyName, InvalidationImpact impact)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        ValidateImpact(impact);
        VerifyMutable();
        Invalidate(InvalidationFor(impact));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void EnsureDirectOwnedChild(ControlBase child)
    {
        Debug.Assert(child is not null, "Direct-child validation requires an instance.");

        if (!ReferenceEquals(child.Parent, this))
        {
            throw new ArgumentException(
                "The control must be a direct child of this owner.",
                nameof(child));
        }
    }

    private static void ValidateImpact(InvalidationImpact impact) =>
        ArgumentOutOfRangeException.ThrowIfNotDefined(impact, nameof(impact), "The change impact is unknown.");

    internal void InvalidateDescendants(Invalidation value) =>
        VisitChildren(child =>
        {
            child.Invalidate(value);
            child.InvalidateDescendants(value);
        });

    /// <summary>Invalidates every descendant's own visual state, letting each one's own appearance
    /// profile decide its own invalidation impact - the same seam <see cref="InvalidateVisualState"/>
    /// uses for this control itself. An inherited state change (such as Disabled cascading from an
    /// ancestor) must repair each descendant's chrome geometry exactly as if that descendant had
    /// reached the state directly.</summary>
    private void InvalidateDescendantsVisualState() =>
        VisitChildren(child =>
        {
            child.InvalidateVisualStateCore();
            child.InvalidateDescendantsVisualState();
        });

    /// <summary>Clears this control's and every descendant's cached <see cref="EffectiveIsVisible"/>
    /// and <see cref="EffectiveIsEnabled"/> values, so the next read recomputes them from the (now
    /// current) local state and ancestor chain. Stops descending once this node is already
    /// invalidated: a descendant can only hold a cached value derived from THIS node's ancestor
    /// state (rather than its own already-false local state, which short-circuits and is invariant
    /// to any ancestor change) by first reading through this node - which cannot happen while this
    /// node's own cache is clear, since that read would have repopulated it.</summary>
    private void InvalidateEffectiveState()
    {
        if (_effectiveIsVisible is null && _effectiveIsEnabled is null)
        {
            return; // no descendant can hold a stale ancestor-derived value while this node is clear
        }

        _effectiveIsVisible = null;
        _effectiveIsEnabled = null;
        VisitChildren(child => child.InvalidateEffectiveState());
    }

    /// <summary>Captures this control's and every descendant's current derived focus/visibility
    /// state, for comparison against a later snapshot once an inherited change (Visibility or
    /// IsEnabled) has committed. Nothing has changed yet when this runs, so each live property
    /// getter is already the correct "before" value.</summary>
    private List<(ControlBase Control, bool EffectiveIsVisible, bool EffectiveIsEnabled, bool CanFocus, bool CanTabStop)>
        SnapshotDerivedFocusState()
    {
        List<(ControlBase, bool, bool, bool, bool)> snapshot = [Capture(this)];
        VisitChildren(Add);
        return snapshot;

        void Add(ControlBase control)
        {
            snapshot.Add(Capture(control));
            control.VisitChildren(Add);
        }

        static (ControlBase, bool, bool, bool, bool) Capture(ControlBase control) =>
            (control, control.EffectiveIsVisible, control.EffectiveIsEnabled, control.CanFocus, control.CanTabStop);
    }

    /// <summary>Raises <see cref="PropertyChanged"/> for exactly the derived properties that
    /// actually changed, on exactly the controls where they changed - comparing the given
    /// pre-mutation snapshot against each control's current (post-mutation) value.</summary>
    private static void PublishDerivedFocusStateChanges(
        List<(ControlBase Control, bool EffectiveIsVisible, bool EffectiveIsEnabled, bool CanFocus, bool CanTabStop)> before)
    {
        ExceptionDispatchInfo? failure = null;

        foreach (var (control, effectiveIsVisible, effectiveIsEnabled, canFocus, canTabStop) in before)
        {
            if (effectiveIsVisible != control.EffectiveIsVisible)
            {
                ExceptionAggregation.Capture(
                    () => control.PropertyChanged?.Invoke(
                        control,
                        new PropertyChangedEventArgs(nameof(EffectiveIsVisible))),
                    ref failure);
            }

            if (effectiveIsEnabled != control.EffectiveIsEnabled)
            {
                ExceptionAggregation.Capture(
                    () => control.PropertyChanged?.Invoke(
                        control,
                        new PropertyChangedEventArgs(nameof(EffectiveIsEnabled))),
                    ref failure);
            }

            if (canFocus != control.CanFocus)
            {
                ExceptionAggregation.Capture(
                    () => control.PropertyChanged?.Invoke(control, new PropertyChangedEventArgs(nameof(CanFocus))),
                    ref failure);
            }

            if (canTabStop != control.CanTabStop)
            {
                ExceptionAggregation.Capture(
                    () => control.PropertyChanged?.Invoke(control, new PropertyChangedEventArgs(nameof(CanTabStop))),
                    ref failure);
            }
        }

        failure?.Throw();
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
        var horizontalInset = Padding.Horizontal.SaturatingAdd(BorderInset.Horizontal);
        var verticalInset = Padding.Vertical.SaturatingAdd(BorderInset.Vertical);
        return new Constraint(
            ResolveContentAxis(Width, constraint.Width, Margin.Horizontal, horizontalInset, MinWidth, MaxWidth),
            ResolveContentAxis(Height, constraint.Height, Margin.Vertical, verticalInset, MinHeight, MaxHeight));
    }

    private Size ResolveDesiredSize(Constraint constraint, Size content)
    {
        var horizontalInset = Padding.Horizontal.SaturatingAdd(BorderInset.Horizontal);
        var verticalInset = Padding.Vertical.SaturatingAdd(BorderInset.Vertical);
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
            if (!ReferenceEquals(Dispatcher, transition.Dispatcher))
            {
                AttachmentVersion++;
            }

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
            _ = AdvanceStylePublicationVersion();

            if (_styleSlots is { } styleSlots)
            {
                foreach (var slot in styleSlots.Values)
                {
                    slot.ClearResolvedCache();
                }
            }

            Invalidate(InvalidationFor(themeTransition.Impact));
        }
    }

    /// <summary>Publishes one exact already-committed Theme or appearance change.</summary>
    /// <param name="change">The staged Theme metadata and exact appearance snapshots.</param>
    internal void PublishAppearanceChanged(in AppearanceChange change)
    {
        Debug.Assert(ReferenceEquals(change.Control, this), "Appearance changes publish through their owning control.");

        var publicationVersion = _stylePublicationVersion;
        ExceptionDispatchInfo? failure = null;

        if (change.ThemeTransition is { } transition)
        {
            ExceptionAggregation.Capture(
                () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Theme))),
                ref failure);

            if (publicationVersion == _stylePublicationVersion)
            {
                foreach (var slot in transition.ChangedStyleSlots)
                {
                    ExceptionAggregation.Capture(
                        () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(slot.ActualPropertyName)),
                        ref failure);

                    if (publicationVersion != _stylePublicationVersion)
                    {
                        break;
                    }
                }
            }
        }

        if (publicationVersion == _stylePublicationVersion)
        {
            var previousAppearance = change.PreviousAppearance;
            var currentAppearance = change.CurrentAppearance;
            ExceptionAggregation.Capture(
                () => PublishAppearanceChanges(previousAppearance, currentAppearance),
                ref failure);
        }

        if (publicationVersion == _stylePublicationVersion && change.ThemeTransition is { } resolvedTransition)
        {
            foreach (var slot in resolvedTransition.ChangedStyleSlots)
            {
                ExceptionAggregation.Capture(
                    () => slot.PublishThemeChanged(resolvedTransition.PreviousTheme, resolvedTransition.CurrentTheme),
                    ref failure);

                if (publicationVersion != _stylePublicationVersion)
                {
                    break;
                }
            }
        }

        failure?.Throw();
    }

    /// <summary>Publishes this control's already committed attachment.</summary>
    internal void PublishAttached()
    {
        ExceptionDispatchInfo? failure = null;

        if (this is IDispatcherAttachmentObserver observer)
        {
            ExceptionAggregation.Capture(observer.OnDispatcherAttached, ref failure);
        }

        if (_bindingRegistry is { } bindingRegistry)
        {
            ExceptionAggregation.Capture(bindingRegistry.OnDispatcherAttached, ref failure);
        }

        ExceptionAggregation.Capture(OnAttached, ref failure);
        failure?.Throw();
    }

    /// <summary>Publishes this control's already committed detachment.</summary>
    internal void PublishDetached()
    {
        ExceptionDispatchInfo? failure = null;

        if (this is IDispatcherAttachmentObserver observer)
        {
            ExceptionAggregation.Capture(observer.OnDispatcherDetached, ref failure);
        }

        if (_bindingRegistry is { } bindingRegistry)
        {
            ExceptionAggregation.Capture(bindingRegistry.OnDispatcherDetached, ref failure);
        }

        ExceptionAggregation.Capture(OnDetached, ref failure);
        failure?.Throw();
    }

    private void CommitAndPublishContext(
        Dispatcher? dispatcher,
        UnicodePolicy cellPolicy,
        FocusManager? focusOwner,
        PointerManager? captureOwner,
        ModalityManager? modalityOwner,
        Theme? theme,
        Action? configure)
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
        List<ControlBase> attached,
        List<ControlBase> detached,
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

    /// <summary>Rejects attached-tree mutation from outside the owning dispatcher.</summary>
    internal void VerifyAccess() => Dispatcher?.VerifyAccess();

    /// <summary>Gets whether a live cache may be read or written by the current caller.</summary>
    internal bool HasDispatcherAccess => Dispatcher?.CheckAccess() ?? true;

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

    private void DisposeStyleBindings()
    {
        if (_styleSlots is null)
        {
            return;
        }

        foreach (var slot in _styleSlots.Values)
        {
            slot.DisposeBindings();
        }
    }

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
        var changed = new List<(ControlBase Control, TerminalCapabilities Previous)>();
        CommitCapabilities(value, changed);
        ExceptionDispatchInfo? failure = null;

        foreach (var (Control, Previous) in changed)
        {
            ExceptionAggregation.Capture(
                () => Control.OnCapabilitiesChanged(Previous, value),
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
        List<(ControlBase Control, TerminalCapabilities Previous)> changed)
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
    private bool _chromeAuthoringEnabled;

    /// <summary>Opts into the public <see cref="Border"/>/<see cref="Shadow"/> authoring surface,
    /// for a derived control whose whole purpose is letting a caller author its own chrome
    /// directly. Meant to be called once from the constructor.</summary>
    /// <exception cref="InvalidOperationException">Chrome authoring is already enabled.</exception>
    protected void EnableChromeAuthoring()
    {
        VerifyMutable();

        if (_chromeAuthoringEnabled)
        {
            throw new InvalidOperationException("Chrome authoring is already enabled.");
        }

        _chromeAuthoringEnabled = true;
    }

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
        get => LocalFaceValue ?? GetNormalAppearance().Face;
        set => SetFace(value);
    }

    /// <summary>Gets the fully composed current face with concrete terminal values. An attached
    /// off-dispatcher read resolves a cache-neutral snapshot.</summary>
    public Face ActualFace
    {
        get
        {
            var state = GetAppearanceState();
            return HasDispatcherAccess ? GetActualFace(state) : ResolveAppearance(Theme, state).Face;
        }
    }

    /// <summary>Clears the complete local face and returns ownership to the active semantic appearance.</summary>
    public void ResetFace()
    {
        VerifyMutable();
        if (LocalFaceValue is null)
        {
            return;
        }

        LocalFaceValue = null;
        InvalidateSubtreeAmbientAppearance();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Face)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActualFace)));
    }

    /// <summary>Gets or sets derived-control border authoring, or the semantic normal border when
    /// unset. Requires <see cref="EnableChromeAuthoring"/>.</summary>
    /// <exception cref="InvalidOperationException">The chrome authoring capability is not enabled.</exception>
    public virtual Border Border
    {
        get => _chromeAuthoringEnabled
            ? LocalBorderValue ?? GetNormalAppearance().Border
            : throw new InvalidOperationException("The chrome authoring capability is not enabled.");
        set => SetBorder(value);
    }

    /// <summary>Gets the fully composed current border with concrete terminal values. An attached
    /// off-dispatcher read resolves a cache-neutral snapshot.</summary>
    public Border ActualBorder
    {
        get
        {
            var state = GetAppearanceState();
            return HasDispatcherAccess ? GetActualBorder(state) : ResolveAppearance(Theme, state).Border;
        }
    }

    /// <summary>Clears the complete local border and returns ownership to the active semantic
    /// appearance. Requires <see cref="EnableChromeAuthoring"/>.</summary>
    /// <exception cref="InvalidOperationException">The chrome authoring capability is not enabled.</exception>
    public virtual void ResetBorder()
    {
        VerifyMutable();

        if (!_chromeAuthoringEnabled)
        {
            throw new InvalidOperationException("The chrome authoring capability is not enabled.");
        }

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

    /// <summary>Gets or sets derived-control shadow authoring, or the semantic normal shadow when
    /// unset. Requires <see cref="EnableChromeAuthoring"/>.</summary>
    /// <exception cref="InvalidOperationException">The chrome authoring capability is not enabled.</exception>
    public Shadow Shadow
    {
        get => _chromeAuthoringEnabled
            ? LocalShadowValue ?? GetNormalAppearance().Shadow
            : throw new InvalidOperationException("The chrome authoring capability is not enabled.");
        set => SetShadow(value);
    }

    /// <summary>Gets the fully composed current shadow with concrete terminal values. An attached
    /// off-dispatcher read resolves a cache-neutral snapshot.</summary>
    public Shadow ActualShadow
    {
        get
        {
            var state = GetAppearanceState();
            return HasDispatcherAccess ? GetActualShadow(state) : ResolveAppearance(Theme, state).Shadow;
        }
    }

    /// <summary>Clears the complete local shadow and returns ownership to the active semantic
    /// appearance. Requires <see cref="EnableChromeAuthoring"/>.</summary>
    /// <exception cref="InvalidOperationException">The chrome authoring capability is not enabled.</exception>
    public void ResetShadow()
    {
        VerifyMutable();

        if (!_chromeAuthoringEnabled)
        {
            throw new InvalidOperationException("The chrome authoring capability is not enabled.");
        }

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

    private readonly Dictionary<VisualState, AppearanceOverlay> _appearanceSets = [];

    // A real control reaches only a handful of distinct VisualState combinations, so this grows
    // from a small inline capacity rather than allocating the full 512-slot combinatorial space
    // Linear scan is cheap at this size and avoids Dictionary's per-entry overhead.
    private ResolvedAppearanceCacheSlot[]? _resolvedAppearanceCache;
    private int _resolvedAppearanceCacheCount;

    /// <summary>Gets the immutable theme inherited from the owning application.</summary>
    public Theme? Theme => InheritedTheme;

    /// <summary>Gets the complete appearance states that own normal and visual-state presentation.</summary>
    protected virtual AppearanceStates AppearanceStates =>
        _primaryStyle?.GetAppearance(Theme) ?? GetDefaultAppearanceStates(Theme);

    /// <summary>Resolves the complete appearance states for one explicit prospective Theme.</summary>
    /// <param name="theme">The inherited Theme to resolve, or null for the library fallback.</param>
    /// <returns>The non-null complete appearance states.</returns>
    /// <remarks>
    /// The current Theme resolves through <see cref="AppearanceStates"/> so derived property overrides remain
    /// authoritative. Styled controls override this hook as well to resolve a different prospective Theme without
    /// temporarily mutating inherited state.
    /// </remarks>
    protected virtual AppearanceStates GetAppearanceStates(Theme? theme) =>
        _primaryStyle?.GetAppearance(theme) ??
        (ReferenceEquals(theme, Theme)
            ? AppearanceStates
            : GetDefaultAppearanceStates(theme));

    /// <summary>Resolves the well-known base style this control type uses when it owns no primary
    /// style slot of its own - the extension point a control overrides to choose which
    /// well-known base style it resolves. The base implementation
    /// resolves the universal root ("control"); a control that instead wants one of
    /// <see cref="ControlStyle"/>'s five siblings (an input-like, framed, top-level, popup,
    /// or passive-hint appearance) overrides this to resolve that sibling's own key instead.</summary>
    /// <param name="theme">The Theme to resolve against, or null for the library fallback.</param>
    protected virtual AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetStyleSet(ControlStyle.Default).ToAppearanceStates();

    /// <summary>Resolves the concrete appearance this control presents for one explicit
    /// prospective Theme and one exact visual-state combination, without attachment.</summary>
    /// <param name="theme">The prospective inherited Theme to resolve against, or null for the
    /// library fallback.</param>
    /// <param name="visualState">The exact visual-state flags to fold, exactly as the live
    /// properties fold the control's current state.</param>
    /// <returns>The complete appearance with every semantic color and decoration resolved to a
    /// literal against the supplied Theme.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="visualState"/> contains
    /// unknown flags.</exception>
    /// <exception cref="ArgumentException">A paint channel resolved to transparent, or resolving
    /// decorations against the supplied Theme produced a conflict.</exception>
    /// <remarks>
    /// This is the sanctioned seam for asserting theme-resolved appearance without a mounted
    /// application: it runs the identical resolution <see cref="ActualFace"/>,
    /// <see cref="ActualBorder"/>, and <see cref="ActualShadow"/> read - appearance-state
    /// selection through the same derived hooks, visual-state and local-overlay folding, ambient
    /// inheritance, and semantic resolution - against the supplied Theme instead of the inherited
    /// one, bypassing the resolved-appearance cache and publishing no change. The control and its
    /// ancestors resolve as if the entire tree inherited the supplied Theme, so ambient
    /// inheritance across a transparent chain previews consistently. Local style, face, border,
    /// shadow, and per-state appearance values participate exactly as they do live.
    /// </remarks>
    public ControlAppearance ResolveAppearance(Theme? theme, VisualState visualState = VisualState.Normal)
    {
        VerifyKnownVisualState(visualState, nameof(visualState));

        var resolved = this.ResolveSnapshot(
            visualState,
            theme,
            GetAppearanceStates(theme),
            ResolveProspectiveAmbientFace(theme));
        return new ControlAppearance(resolved.Face, resolved.Border, resolved.Shadow);
    }

    /// <summary>Resolves the ambient face one prospective Theme would publish from this control's
    /// ancestor chain, mirroring the live parent-ambient walk at explicit-Theme fidelity.</summary>
    /// <param name="theme">The prospective inherited Theme, or null for the library fallback.</param>
    /// <returns>The prospective parent ambient face, or null at a root.</returns>
    private Face? ResolveProspectiveAmbientFace(Theme? theme) =>
        Parent is { } parent
            ? parent.ResolveSnapshot(
                parent.AmbientAppearanceState,
                theme,
                parent.GetAppearanceStates(theme),
                parent.ResolveProspectiveAmbientFace(theme)).Face
            : null;

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
        Face? currentParentAmbientFace)
    {
        var impact = _primaryStyle?.GetThemeImpact(
            previous,
            current,
            previousParentAmbientFace,
            currentParentAmbientFace) ??
            this.GetImpact(
                previous,
                GetDefaultAppearanceStates(previous),
                current,
                GetDefaultAppearanceStates(current),
                previousParentAmbientFace,
                currentParentAmbientFace);

        if (_styleSlots is not null)
        {
            foreach (var slot in _styleSlots.Values)
            {
                if (!ReferenceEquals(slot, _primaryStyle))
                {
                    impact = MaximumImpact(
                        impact,
                        slot.GetThemeImpact(
                            previous,
                            current,
                            previousParentAmbientFace,
                            currentParentAmbientFace));
                }
            }
        }

        return MaximumImpact(impact, GetTrackedThemeStructuralImpact(previous, current));
    }

    /// <summary>Compares every root Theme value previously resolved outside appearance profiles.</summary>
    private InvalidationImpact GetTrackedThemeStructuralImpact(Theme? previous, Theme? current)
    {
        var previousTheme = previous ?? ThemeCatalog.Dark;
        var currentTheme = current ?? ThemeCatalog.Dark;
        var affixGapChanged =
            (_themeStructuralDependencies & ThemeStructuralDependency.InputAffixGap) != 0 &&
            previousTheme.GetStyleSet(InputStyle.Default).Normal.AffixGap !=
            currentTheme.GetStyleSet(InputStyle.Default).Normal.AffixGap;
        var glyphChanged =
            ((_themeStructuralDependencies & ThemeStructuralDependency.InputDropDownGlyph) != 0 &&
                previousTheme.GetStyleSet(InputStyle.Default).Normal.DropDownGlyph !=
                currentTheme.GetStyleSet(InputStyle.Default).Normal.DropDownGlyph) ||
            ((_themeStructuralDependencies & ThemeStructuralDependency.PopupAnchorGlyphs) != 0 &&
                previousTheme.GetStyleSet(PopupStyle.Default).Normal.AnchorGlyphs !=
                currentTheme.GetStyleSet(PopupStyle.Default).Normal.AnchorGlyphs);
        return affixGapChanged
            ? InvalidationImpact.Measure
            : glyphChanged
                ? InvalidationImpact.Render
                : InvalidationImpact.None;
    }

    /// <summary>Calculates the exact invalidation impact of one resolved appearance change.</summary>
    /// <param name="previous">The resolved appearance before the change.</param>
    /// <param name="current">The resolved appearance after the change.</param>
    /// <returns>The earliest UI phase affected by the change.</returns>
    /// <remarks>Specialized controls may refine layout impact when intrinsic chrome has control-specific geometry.</remarks>
    internal virtual InvalidationImpact GetAppearanceChangeImpact(
        ResolvedAppearance previous,
        ResolvedAppearance current) => previous.GetImpact(current);

    /// <summary>Collects the slots whose resolved values change across a prospective Theme transition.</summary>
    private List<StyleSlotBase> GetThemeResolvedStyleSlots(Theme? previous, Theme? current)
    {
        if (_styleSlots is null)
        {
            return _primaryStyle is { } primary && primary.GetThemeResolvedProperty(previous, current) is not null
                ? [primary]
                : [];
        }

        var slots = new List<StyleSlotBase>(_styleSlots.Count);
        foreach (var slot in _styleSlots.Values)
        {
            if (slot.GetThemeResolvedProperty(previous, current) is { } propertyName &&
                !slots.Exists(existing => string.Equals(existing.ActualPropertyName, propertyName, StringComparison.Ordinal)))
            {
                slots.Add(slot);
            }
        }

        return slots;
    }

    /// <summary>Gets or sets whether this control stops ambient text appearance inheritance.</summary>
    public bool IsAppearanceBoundary
    {
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.Render))
            {
                InvalidateSubtreeAmbientAppearance();
            }
        }
    }

    /// <summary>Sets or removes one partial local visual-state appearance contribution.</summary>
    /// <param name="state">One non-normal visual state.</param>
    /// <param name="appearance">The partial contribution, or null to remove it.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is normal, combined, or unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected void SetAppearance(VisualState state, AppearanceOverlay? appearance)
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

        // For a control whose ambient face is its ACTIVE state rather than its Normal - a caption-
        // enabled InputBase descendant and ListItem, which override StateAffectsAmbientAppearance -
        // a per-state overlay is folded into the face descendants inherit, so clearing this control
        // alone leaves them holding a ResolvedAppearance built from the previous overlay. They
        // cannot take the render-clean-reuse fast path either (that needs an opaque background), so
        // they repaint from the stale cache and a Button's caption keeps its old colour while the
        // button face changes. This mirrors what InvalidateVisualStateCore already does for the
        // same two kinds of control.
        if (StateAffectsAmbientAppearance)
        {
            InvalidateSubtreeAmbientAppearance();
        }
        else
        {
            InvalidateResolvedStyleCache();
        }

        var geometryChanged = AppearanceStates.ChangesChromeGeometry(previous) ||
                              (appearance is { } added && AppearanceStates.ChangesChromeGeometry(added));
        Invalidate(geometryChanged ? Invalidation.Measure : Invalidation.Render);
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
        var changedStyleSlots = GetThemeResolvedStyleSlots(previousTheme, theme);

        foreach (var changedStyleSlot in changedStyleSlots)
        {
            ArgumentException.ThrowIfNullOrEmpty(changedStyleSlot.ActualPropertyName);
        }

        return new ThemeTransition(
            this,
            previousTheme,
            theme,
            changedStyleSlots,
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

    private static void VerifyKnownVisualState(VisualState state, string paramName)
    {
        const int stateCount = 1 << 9;

        if ((uint) state >= stateCount)
        {
            throw new ArgumentOutOfRangeException(paramName, state, "The visual state contains unknown flags.");
        }
    }

    internal ResolvedAppearance GetResolvedAppearance(VisualState state)
    {
        VerifyKnownVisualState(state, nameof(state));

        var cache = _resolvedAppearanceCache;

        for (var slot = 0; slot < _resolvedAppearanceCacheCount; slot++)
        {
            if (cache![slot].State == state)
            {
                return cache[slot].Appearance;
            }
        }

        UncachedAppearanceResolutionCount++;
        var resolved = this.Resolve(state);

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

    internal IReadOnlyDictionary<VisualState, AppearanceOverlay> AppearanceSets => _appearanceSets;

    internal AppearanceStates ResolvedAppearanceStates => AppearanceStates;

    /// <summary>Resolves one prospective profile through the derived control hook.</summary>
    /// <param name="theme">The explicit prospective inherited Theme, or null.</param>
    /// <returns>The non-null complete appearance states.</returns>
    internal AppearanceStates ResolveAppearanceStates(Theme? theme) => GetAppearanceStates(theme);

    [Pure]
    internal static Color ResolveThemeColor(Color color) => color;

    /// <summary>Resolves a possibly-literal color value against an optional theme.</summary>
    /// <param name="value">The literal or theme-referenced color value.</param>
    /// <param name="theme">The active theme, or null when no theme resolves the value.</param>
    /// <returns>The literal color, or the theme-resolved color, or <see cref="Color.Default"/>.</returns>
    [Pure]
    protected internal static Color ResolveColor(ControlColor value, Theme? theme) => value.Resolve(theme);

    /// <summary>Resolves a possibly-literal color value against this control's active theme.</summary>
    /// <param name="value">The literal or theme-referenced color value.</param>
    /// <returns>The literal color, or the theme-resolved color, or <see cref="Color.Default"/>.</returns>
    [Pure]
    protected Color ResolveColor(ControlColor value) => ResolveColor(value, Theme);

    internal Rune ResolveControlGlyph(ControlGlyph glyph) =>
        glyph.Value.Resolve(glyph.Fallback, CellPolicy.AmbiguousWidth);

    internal BorderGlyphStyle ResolveBorderGlyphs(BorderGlyphStyle glyphs)
    {
        var fallback = ControlGlyphs.Chrome;
        return new BorderGlyphStyle(
            glyphs.TopLeft.Resolve(fallback.TopLeft.Fallback, CellPolicy.AmbiguousWidth),
            glyphs.Top.Resolve(fallback.Top.Fallback, CellPolicy.AmbiguousWidth),
            glyphs.TopRight.Resolve(fallback.TopRight.Fallback, CellPolicy.AmbiguousWidth),
            glyphs.Right.Resolve(fallback.Right.Fallback, CellPolicy.AmbiguousWidth),
            glyphs.BottomRight.Resolve(fallback.BottomRight.Fallback, CellPolicy.AmbiguousWidth),
            glyphs.Bottom.Resolve(fallback.Bottom.Fallback, CellPolicy.AmbiguousWidth),
            glyphs.BottomLeft.Resolve(fallback.BottomLeft.Fallback, CellPolicy.AmbiguousWidth),
            glyphs.Left.Resolve(fallback.Left.Fallback, CellPolicy.AmbiguousWidth));
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
            InvalidateSubtreeAmbientAppearance();
        }
        else
        {
            InvalidateResolvedStyleCache();
        }

        Invalidate(VisualStateInvalidation());
    }

    internal void InvalidateSubtreeResolvedStyleCache()
    {
        var stack = new Stack<ControlBase>();
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

    /// <summary>Clears the resolved-appearance cache and render-invalidates every visited control in
    /// this subtree, for a change to an ambient Face-authoring source (<see cref="Face"/>,
    /// <see cref="ResetFace"/>, <see cref="IsAppearanceBoundary"/>, or a visual-state change with
    /// <see cref="StateAffectsAmbientAppearance"/>). A render-clean descendant whose ambient-derived
    /// appearance depends on this change must repaint instead of taking the render-clean-reuse fast
    /// path and copying stale previous-frame cells that no longer reflect the new ambient state.
    /// Unlike the bare cache clear above, this is deliberately unconditional - the caller has no
    /// precomputed per-descendant impact to compare, since the change may affect an arbitrary number
    /// of ambiently-inheriting descendants it never resolved.</summary>
    private void InvalidateSubtreeAmbientAppearance()
    {
        var stack = new Stack<ControlBase>();
        stack.Push(this);

        while (stack.TryPop(out var control))
        {
            control.InvalidateResolvedStyleCache();
            control.Invalidate(Invalidation.Render);

            for (var index = control.OwnedControlCount - 1; index >= 0; index--)
            {
                stack.Push(control.OwnedControlAt(index));
            }
        }
    }

    private ControlAppearance GetNormalAppearance() =>
        AppearanceStates.Normal;

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
        InvalidateSubtreeAmbientAppearance();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Face)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActualFace)));
    }

    private void SetBorder(Border value)
    {
        VerifyMutable();

        if (!_chromeAuthoringEnabled)
        {
            throw new InvalidOperationException("The chrome authoring capability is not enabled.");
        }

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

        if (!_chromeAuthoringEnabled)
        {
            throw new InvalidOperationException("The chrome authoring capability is not enabled.");
        }

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
