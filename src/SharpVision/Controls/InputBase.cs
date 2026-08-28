// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;
using System.Windows.Input;

using Popups;

using SharpVision.Terminal.Input;

using SharpVision.Text;

using DisplayText = Display.Text;

/// <summary>
/// Defines a focusable control that opts into the shared editing and drop-down interaction
/// primitives value editors and popup-backed inputs need, without imposing any of them.
/// </summary>
/// <remarks>
/// Every concrete control derived from this type is focusable and participates in Tab traversal
/// by default. Beyond that, nothing is assumed: a control calls whichever <c>Enable*</c> method
/// matches the capability it actually composes - press activation, a single owned text caption, an
/// optional command, segmented temporal editing, a step-key translation, the shared drop-down
/// glyph, or an owned popup - and every capability is independent of the others. Calling an
/// <c>Enable*</c> method a second time throws <see cref="InvalidOperationException"/>: each
/// capability is meant to be wired once, from the constructor.
/// </remarks>
[PublicAPI]
public abstract class InputBase: ControlBase
{
    /// <summary>Initializes a focusable control participating in Tab traversal.</summary>
    protected InputBase()
    {
        IsFocusable = true;
        IsTabStop = true;
    }

    #region Press activation

    private PressBehavior? _press;

    /// <summary>Gets the rectangle press interaction (pointer press/drag/release and hit testing
    /// via <see cref="ControlBase.HitTest"/>) is evaluated against. Defaults to <see cref="ControlBase.Bounds"/>.</summary>
    /// <remarks>
    /// A concrete control whose pressed visual state translates the drawn face away from
    /// <see cref="ControlBase.Bounds"/> - a Button showing a whole-cell shadow, for example -
    /// must override this to the same translated rectangle it actually paints, so the pointer
    /// geometry a user presses and releases against agrees with what is on screen.
    /// </remarks>
    protected virtual Rect InteractionBounds => Bounds;

    /// <summary>Opts into the shared pointer-press and Enter/Space keyboard-activation
    /// interaction.</summary>
    /// <exception cref="InvalidOperationException">Press activation is already enabled.</exception>
    protected void EnablePressActivation()
    {
        VerifyMutable();

        if (_press is not null)
        {
            throw new InvalidOperationException("Press activation is already enabled.");
        }

        _press = new PressBehavior(
            () => InteractionBounds,
            () => !IsDisposed && EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || IsFocused,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed,
            Activate,
            () => Capabilities.KeyReleaseEvents.Authoritative);
    }

    /// <summary>Routes one event through the press-activation state machine, if enabled.</summary>
    /// <param name="e">The event to evaluate.</param>
    protected void HandlePressActivation(RoutedEventArgs e) => _press?.Handle(e);

    /// <summary>Completes one validated activation in a concrete control that enabled press
    /// activation.</summary>
    /// <param name="cause">The input path that completed activation.</param>
    protected virtual void Activate(ActivationCause cause)
    {
    }

    #endregion

    #region Caption

    private OwnedControlSlot? _textSlot;

    /// <summary>Opts into the shared single-caption authoring role: a lazily materialized owned
    /// <see cref="Display.Text"/> child exposed through <see cref="Text"/>, ambient appearance
    /// tracking of that child's face from this control's visual state, and the shared caption
    /// access-key wiring.</summary>
    /// <exception cref="InvalidOperationException">The caption capability is already enabled.</exception>
    protected void EnableCaption()
    {
        VerifyMutable();

        if (_textSlot is not null)
        {
            throw new InvalidOperationException("The caption capability is already enabled.");
        }

        _textSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.Content,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: null,
                InvalidationImpact.Measure),
            capacity: 1);
    }

    /// <summary>Gets or sets the non-null caption text.</summary>
    /// <remarks>
    /// The default implementation is backed by a lazily materialized owned <see cref="DisplayText"/>
    /// child, created on the first non-default assignment: a control that never sets text never pays
    /// for one. Notifies exactly once per committed change and is silent on same-value assignment.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The caption capability is not enabled, or the
    /// attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public virtual string Text
    {
        get => TextControl?.Content ?? string.Empty;
        set
        {
            if (_textSlot is not { } slot)
            {
                throw new InvalidOperationException("The caption capability is not enabled.");
            }

            VerifyMutable();
            ArgumentNullException.ThrowIfNull(value);

            if (string.Equals(Text, value, StringComparison.Ordinal))
            {
                return;
            }

            if (TextControl is null)
            {
                TextControl = new DisplayText(value);
                slot.ReplaceAll([TextControl]);
            }
            else
            {
                TextControl.Content = value;
            }

            NotifyPropertyChanged(nameof(Text), InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets the lazily materialized owned caption child, or null before <see cref="Text"/>
    /// is first assigned.</summary>
    protected internal DisplayText? TextControl { get; private set; }

    /// <inheritdoc/>
    public override SelectableTextSnapshot GetSelectableTextSnapshot()
    {
        VerifyMutable();
        return CreateSelectableTextSnapshot();
    }

    /// <inheritdoc/>
    internal override bool AddSelectableTextChildren(List<ControlBase> children)
    {
        ArgumentNullException.ThrowIfNull(children);

        if (TextControl is { } text)
        {
            children.Add(text);
        }

        return true;
    }

    /// <summary>Gets whether <paramref name="candidate"/> is this control's own owned caption child.</summary>
    /// <param name="candidate">The control to test.</param>
    internal bool OwnsCaption(ControlBase candidate) => ReferenceEquals(TextControl, candidate);

    /// <inheritdoc/>
    protected override string? AccessKeyText => _textSlot is not null ? TextControl?.Content : base.AccessKeyText;

    /// <inheritdoc/>
    protected override bool OnAccessKey(Rune key)
    {
        if (_textSlot is null)
        {
            return base.OnAccessKey(key);
        }

        _ = key;

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            return false;
        }

        _ = FocusAccessKeyTarget();
        Activate(ActivationCause.Keyboard);
        return true;
    }

    /// <inheritdoc/>
    internal override VisualState AmbientAppearanceState =>
        _textSlot is not null ? GetAppearanceState() : base.AmbientAppearanceState;

    /// <inheritdoc/>
    internal override bool StateAffectsAmbientAppearance => _textSlot is not null;

    /// <summary>Measures the owned caption child for a control that opted into <see cref="EnableCaption"/>
    /// and uses this default single-caption layout, or an empty size before one is materialized.</summary>
    /// <param name="constraint">The available layout constraint.</param>
    /// <returns>The caption child's desired size including its margin, or <see langword="default"/>.</returns>
    protected Size MeasureCaption(Constraint constraint)
    {
        if (TextControl is not { } content)
        {
            return default;
        }

        var desired = MeasureChild(content, constraint);

        return content.Visibility == Visibility.Collapsed
            ? default
            : new Size(
                desired.Width.SaturatingAdd(content.Margin.Horizontal),
                desired.Height.SaturatingAdd(content.Margin.Vertical));
    }

    /// <summary>Arranges the owned caption child to fill the available bounds, if materialized.</summary>
    /// <param name="bounds">The bounds to arrange within.</param>
    protected void ArrangeCaption(Rect bounds)
    {
        if (TextControl is { } content)
        {
            ArrangeChild(content, bounds, ResolvedAxes.Both);
        }
    }

    #endregion

    #region Command

    private bool _commandEnabled;
    private ICommand? _command;
    private readonly List<(ICommand Command, EventHandler Handler)> _retiredCommandSubscriptions = [];
    private ICommand? _subscribedCommand;
    private EventHandler? _subscribedCommandHandler;

    /// <summary>Opts into an optional command a concrete control invokes on activation, exposed
    /// through <see cref="Command"/> and <see cref="CommandParameter"/>.</summary>
    /// <exception cref="InvalidOperationException">The command capability is already enabled.</exception>
    protected void EnableCommand()
    {
        VerifyMutable();

        if (_commandEnabled)
        {
            throw new InvalidOperationException("The command capability is already enabled.");
        }

        _commandEnabled = true;
    }

    /// <summary>Gets or sets the borrowed optional command a concrete control invokes on activation.</summary>
    /// <remarks>
    /// Replacement publishes only after the event subscription has been reconciled. Reentrant
    /// replacement is latest-wins, and event-accessor failures retain enough subscription identity
    /// for a same-reference assignment or disposal to retry cleanup deterministically.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The command capability is not enabled, or the
    /// attached control is mutated off-dispatcher; a command event accessor may also report this exception.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ICommand? Command
    {
        get => _commandEnabled
            ? _command
            : throw new InvalidOperationException("The command capability is not enabled.");
        set
        {
            if (!_commandEnabled)
            {
                throw new InvalidOperationException("The command capability is not enabled.");
            }

            VerifyMutable();

            if (ReferenceEquals(_command, value))
            {
                ReconcileCommandSubscription();
                return;
            }

            _ = SetPropertyAndSynchronize(
                ref _command,
                value,
                InvalidationImpact.Render,
                ReconcileCommandSubscription,
                ReferenceEqualityComparer.Instance);
        }
    }

    /// <summary>Gets or sets the borrowed parameter passed to command queries and execution.</summary>
    /// <exception cref="InvalidOperationException">The command capability is not enabled, or the
    /// attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public object? CommandParameter
    {
        get => _commandEnabled
            ? field
            : throw new InvalidOperationException("The command capability is not enabled.");
        set
        {
            if (!_commandEnabled)
            {
                throw new InvalidOperationException("The command capability is not enabled.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Captures the command binding that owns one activation transaction.</summary>
    /// <remarks>
    /// Concrete controls capture before publishing activation callbacks, then execute the returned
    /// binding afterward. Reentrant rebinding or disposal therefore cannot redirect work already
    /// accepted by the activation entry point.
    /// </remarks>
    /// <returns>The borrowed command and parameter currently bound to this control.</returns>
    internal (ICommand? Command, object? Parameter) CaptureCommand() => (Command, CommandParameter);

    /// <summary>Invokes the current command binding when it allows execution.</summary>
    /// <remarks>
    /// This extension seam retains its dynamic lookup contract for derived controls. First-party
    /// activation implementations capture their binding before public callbacks instead.
    /// </remarks>
    protected void ExecuteCommandIfAny() => ExecuteCommandIfAny((Command, CommandParameter));

    /// <summary>Invokes one previously captured command binding when it allows execution.</summary>
    /// <param name="binding">The command and parameter captured at activation entry.</param>
    /// <remarks>
    /// Execution follows the control's own committed state and events, so a command that cannot
    /// execute never suppresses the control's activation semantics.
    /// </remarks>
    internal static void ExecuteCommandIfAny((ICommand? Command, object? Parameter) binding)
    {
        var (command, parameter) = binding;

        if (command is not null && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    private void OnCanExecuteChanged(ICommand source, object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (IsDisposed ||
            !ReferenceEquals(source, _command) ||
            !ReferenceEquals(source, _subscribedCommand))
        {
            return;
        }

        var dispatcher = Dispatcher;

        if (dispatcher is null)
        {
            return;
        }

        var attachmentVersion = AttachmentVersion;

        if (!dispatcher.CheckAccess())
        {
            try
            {
                // CanExecuteChanged is raised by an arbitrary, caller-supplied ICommand, possibly
                // off-thread, as one handler in that command's own multicast invocation list. A
                // full queue here just means this render invalidation is skipped until the next
                // change notification; propagating InvalidOperationException would fault whatever
                // third-party code raised the event and could break delivery to its other
                // subscribers, which is worse than one stale render.
                dispatcher.Post(() =>
                {
                    if (!IsDisposed &&
                        ReferenceEquals(Dispatcher, dispatcher) &&
                        AttachmentVersion == attachmentVersion)
                    {
                        Invalidate(Invalidation.Render);
                    }
                });
            }
            catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException)
            {
            }

            return;
        }

        Invalidate(Invalidation.Render);
    }

    #endregion

    #region Segment editing

    private bool _segmentEditingEnabled;

    /// <summary>Opts into the shared active-segment navigation, digit-entry buffering, and
    /// pointer hit-testing state machine used by every segmented temporal field control.</summary>
    /// <param name="segmentsProvider">Returns the current, possibly culture- or format-dependent, segment layout.</param>
    /// <param name="applyDigitValue">
    /// Applies a fully or partially typed numeric value to a segment's kind, clamping as the
    /// control sees fit, and returns whether the value actually changed.
    /// </param>
    /// <param name="incrementSegment">Applies a one-step increment (positive or negative delta) to a segment's kind and returns whether the value changed.</param>
    /// <param name="clearSegment">Resets a segment's kind to its lowest representable value and returns whether the value changed.</param>
    /// <returns>The newly constructed behavior, owned by the caller.</returns>
    /// <exception cref="InvalidOperationException">Segment editing is already enabled.</exception>
    private protected SegmentFieldBehavior EnableSegmentEditing(
        Func<IReadOnlyList<SegmentDescriptor>> segmentsProvider,
        Func<TemporalSegmentKind, int, bool> applyDigitValue,
        Func<TemporalSegmentKind, int, bool> incrementSegment,
        Func<TemporalSegmentKind, bool> clearSegment)
    {
        VerifyMutable();

        if (_segmentEditingEnabled)
        {
            throw new InvalidOperationException("Segment editing is already enabled.");
        }

        _segmentEditingEnabled = true;
        return new SegmentFieldBehavior(
            segmentsProvider,
            applyDigitValue,
            incrementSegment,
            clearSegment,
            () => Invalidate(InvalidationImpact.Render));
    }

    #endregion

    #region Stepping

    /// <summary>Translates an Up/Down arrow key press into a one-step increment delta.</summary>
    /// <param name="eventArgs">The key event to inspect.</param>
    /// <param name="delta">Set to <c>1</c> for Up, <c>-1</c> for Down, or <c>0</c> when unmatched.</param>
    /// <returns>True when the key was Up or Down.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    protected static bool TryGetStepDelta(KeyEventArgs eventArgs, out int delta)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs.Stroke.Code == Code.Up)
        {
            delta = 1;
            return true;
        }

        if (eventArgs.Stroke.Code == Code.Down)
        {
            delta = -1;
            return true;
        }

        delta = 0;
        return false;
    }

    #endregion

    #region Drop-down glyph

    /// <summary>Gets the cell width reserved for a drop-down disclosure indicator.</summary>
    protected const int DropDownIndicatorWidth = 1;

    /// <summary>Resolves the disclosure chevron from the active theme's input style.</summary>
    /// <remarks>
    /// The chevron lives on <see cref="InputStyle"/> rather than on the control, so a theme
    /// targeting a terminal without dependable arrow coverage replaces it for every drop-down
    /// input at once instead of per instance. This resolves appearance through the style set
    /// directly rather than through <c>AppearanceStates</c>, which drops non-appearance members.
    /// </remarks>
    /// <param name="fallback">The code-owned narrow-policy fallback.</param>
    /// <returns>The glyph to draw.</returns>
    protected Rune ResolveDropDownGlyph(Rune fallback)
    {
        TrackThemeStructuralDependency(ThemeStructuralDependency.InputDropDownGlyph);
        return (Theme ?? ThemeCatalog.Dark)
            .GetStyleSet(InputStyle.Default)
            .Normal.DropDownGlyph.Resolve(fallback, CellPolicy.AmbiguousWidth);
    }

    /// <summary>Draws the shared disclosure chevron at the content box's trailing edge.</summary>
    /// <param name="canvas">The canvas to draw into.</param>
    /// <param name="content">The control's content bounds; the glyph is drawn at its top row, right-aligned within <see cref="DropDownIndicatorWidth"/>.</param>
    /// <param name="style">The resolved cell style to draw the glyph with.</param>
    protected void DrawDropDownIndicator(TerminalCanvas canvas, Rect content, TerminalStyle style)
    {
        var themed = ControlGlyphs.Disclosure.DropDown;
        var glyph = ResolveDropDownGlyph(themed.Fallback);
        canvas.DrawRune(
            glyph,
            new Point(Math.Max(content.X, content.Right - DropDownIndicatorWidth), content.Y),
            style,
            BackgroundMode.Transparent);
    }

    #endregion

    #region Popup

    private PopupDropDownCoordinator? _popupCoordinator;

    /// <summary>Opts into an owned popup with the shared open/close lifecycle, modal composition,
    /// and framework-part slot every composite drop-down field (a ComboBox, a DateInput, a
    /// DateTimeInput) uses.</summary>
    /// <param name="content">The non-null popup content, also used as its focus scope.</param>
    /// <param name="placement">The preferred anchor-relative placement.</param>
    /// <param name="focusOnOpen">Whether opening transfers focus to the first eligible descendant of <paramref name="content"/>.</param>
    /// <param name="popupTabNavigation">
    /// The Tab-traversal boundary the owned popup itself applies to <paramref name="content"/>.
    /// Controls differ here today: a control whose own keyboard handling drives every navigation
    /// key while its popup is open (a ComboBox, a DateTimeInput) excludes the popup from ordinary
    /// Tab traversal; a control that instead forwards navigation keys straight into its popup
    /// content (a DateInput) leaves the popup's own scope boundary at its default.
    /// </param>
    /// <param name="beforeOpen">Optional work run before the popup opens, such as seeding a value or syncing a calendar.</param>
    /// <param name="beforeCloseFocusRestore">Optional work run before the closing focus-restore check, such as discarding type-ahead state.</param>
    /// <returns>The newly constructed, owned popup.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The popup capability is already enabled.</exception>
    protected Popup EnablePopup(
        ControlBase content,
        PopupPlacement placement = PopupPlacement.Below,
        bool focusOnOpen = false,
        TabNavigation popupTabNavigation = TabNavigation.None,
        Action? beforeOpen = null,
        Action? beforeCloseFocusRestore = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        VerifyMutable();

        if (_popupCoordinator is not null)
        {
            throw new InvalidOperationException("The popup capability is already enabled.");
        }

        var popup = new Popup
        {
            Anchor = this,
            Content = content,
            FocusOnOpen = focusOnOpen,
            ModalBehavior = PopupModalBehavior.None,
            TabNavigation = popupTabNavigation,
            ConnectsToAnchor = true,
            Placement = placement,
            // The owner re-arranges its own popup child from its own ArrangeOverride every pass,
            // so base Popup's anchor-reflow tracking would be a redundant second placement pass
            // reacting to the same self-owned anchor.
            TracksAnchorReflow = false
        };
        var slot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Popup,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "drop-down",
                InvalidationImpact.Measure),
            capacity: 1);
        slot.Add(popup);
        _popupCoordinator = new PopupDropDownCoordinator(
            this,
            popup,
            content,
            RequestFocus,
            () => NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.None),
            OnDropDownOpened,
            OnDropDownClosed,
            beforeOpen,
            beforeCloseFocusRestore);
        return popup;
    }

    /// <summary>Gets or sets whether the owned popup is open.</summary>
    /// <exception cref="InvalidOperationException">The popup capability is not enabled, the
    /// control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    /// <exception cref="Exception">A focus, scope, pointer-cleanup, or user callback fails after committed cleanup.</exception>
    public bool IsOpen
    {
        get => _popupCoordinator is { } coordinator
            ? coordinator.IsOpen
            : throw new InvalidOperationException("The popup capability is not enabled.");
        set
        {
            if (_popupCoordinator is not { } coordinator)
            {
                throw new InvalidOperationException("The popup capability is not enabled.");
            }

            coordinator.SetOpen(value);
        }
    }

    /// <summary>Reconciles exactly one event handler to the still-current borrowed command.</summary>
    private void ReconcileCommandSubscription()
    {
        while (true)
        {
            var desired = IsDisposed ? null : _command;

            if (ReferenceEquals(_subscribedCommand, desired))
            {
                return;
            }

            if (_subscribedCommand is { } subscribed)
            {
                var subscribedHandler = _subscribedCommandHandler;
                Debug.Assert(subscribedHandler is not null, "A tracked command always owns its exact handler.");

                try
                {
                    subscribed.CanExecuteChanged -= subscribedHandler;
                }
                catch
                {
                    TrackRetiredCommandSubscription(subscribed, subscribedHandler);
                    throw;
                }

                if (ReferenceEquals(_subscribedCommand, subscribed) &&
                    _subscribedCommandHandler == subscribedHandler)
                {
                    _subscribedCommand = null;
                    _subscribedCommandHandler = null;
                }

                UntrackRetiredCommandSubscription(subscribed, subscribedHandler);
                continue;
            }

            if (desired is null)
            {
                return;
            }

            void CandidateHandler(object? sender, EventArgs eventArgs) =>
                OnCanExecuteChanged(desired, sender, eventArgs);

            EventHandler candidateHandler = CandidateHandler;

            try
            {
                desired.CanExecuteChanged += candidateHandler;
            }
            catch
            {
                try
                {
                    desired.CanExecuteChanged -= candidateHandler;
                }
                catch
                {
                    TrackRetiredCommandSubscription(desired, candidateHandler);
                }

                throw;
            }

            if (!IsDisposed &&
                ReferenceEquals(_command, desired) &&
                _subscribedCommand is null)
            {
                _subscribedCommand = desired;
                _subscribedCommandHandler = candidateHandler;
                return;
            }

            try
            {
                desired.CanExecuteChanged -= candidateHandler;
            }
            catch
            {
                TrackRetiredCommandSubscription(desired, candidateHandler);
                throw;
            }
        }
    }

    /// <summary>Records a handler whose remove accessor did not complete successfully.</summary>
    private void TrackRetiredCommandSubscription(ICommand command, EventHandler handler)
    {
        if (!_retiredCommandSubscriptions.Any(candidate =>
                ReferenceEquals(candidate.Command, command) && candidate.Handler == handler))
        {
            _retiredCommandSubscriptions.Add((command, handler));
        }
    }

    /// <summary>Forgets a retired handler after a later removal completes successfully.</summary>
    private void UntrackRetiredCommandSubscription(ICommand command, EventHandler handler) =>
        _retiredCommandSubscriptions.RemoveAll(candidate =>
            ReferenceEquals(candidate.Command, command) && candidate.Handler == handler);

    /// <summary>Detaches every known command handler while retaining the first accessor failure.</summary>
    private void ReleaseCommandSubscriptions()
    {
        var subscriptions = _retiredCommandSubscriptions.ToList();

        if (_subscribedCommand is { } subscribed &&
            _subscribedCommandHandler is { } subscribedHandler &&
            !subscriptions.Any(candidate =>
                ReferenceEquals(candidate.Command, subscribed) && candidate.Handler == subscribedHandler))
        {
            subscriptions.Add((subscribed, subscribedHandler));
        }

        _subscribedCommand = null;
        _subscribedCommandHandler = null;
        _retiredCommandSubscriptions.Clear();
        ExceptionDispatchInfo? failure = null;

        foreach (var subscription in subscriptions)
        {
            ExceptionAggregation.Capture(
                () => subscription.Command.CanExecuteChanged -= subscription.Handler,
                ref failure);
        }

        failure?.Throw();
    }

    /// <summary>Gets the current owned-popup request version for continuation validation.</summary>
    internal ulong PopupTransitionVersion => _popupCoordinator is { } coordinator
        ? coordinator.TransitionVersion
        : throw new InvalidOperationException("The popup capability is not enabled.");

    /// <summary>Called after the owned popup opens.</summary>
    protected virtual void OnDropDownOpened()
    {
    }

    /// <summary>Called after the owned popup closes.</summary>
    protected virtual void OnDropDownClosed()
    {
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);
        _press?.FocusChanged(focused);
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        _press?.CaptureLost();
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        _popupCoordinator?.OnOwnerAttached();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _press?.Unavailable();

        if (reason == ReleaseReason.Disposed)
        {
            _command = null;
            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(() => _popupCoordinator?.Detach(), ref failure);
            ExceptionAggregation.Capture(ReleaseCommandSubscriptions, ref failure);
            failure?.Throw();
        }
    }

    #endregion

    /// <summary>Throws when mutation is not valid for this control.</summary>
    /// <remarks>
    /// <see cref="ControlBase.VerifyMutable"/> is internal, so a third-party derivative cannot
    /// call it directly. This exposes the identical off-dispatcher and disposed guard shipped
    /// controls already use, under the same name.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected new void VerifyMutable() => base.VerifyMutable();
}
