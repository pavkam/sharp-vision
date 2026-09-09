// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;
using System.Windows.Input;

using SharpVision.Controls.Input;

using SharpVision.Terminal.Input;

using SharpVision.Text;

using DisplayText = Display.Text;

/// <summary>
/// Defines a focusable control that opts into the shared editing and drop-down interaction
/// primitives value editors and popup-backed inputs need, without imposing any of them.
/// </summary>
/// <remarks>
/// Every concrete control derived from this type is focusable and participates in Tab traversal
/// by default. It also resolves its fallback appearance from <see cref="InputStyle"/>, even when
/// no optional capability is enabled; a concrete typed style still supersedes that fallback
/// through the ordinary style slot. <see cref="StartAffix"/> and <see cref="EndAffix"/> provide the
/// common optional edge-decoration contract; each concrete input decides how its layout reserves
/// those cells. Beyond that, nothing is assumed: a control calls whichever
/// <c>Enable*</c> method
/// matches the capability it actually composes - press activation, a single owned text caption, an
/// optional command, segmented temporal editing, a step-key translation, the shared drop-down
/// glyph, or an owned popup - and every capability is independent of the others. Calling an
/// <c>Enable*</c> method a second time throws <see cref="InvalidOperationException"/>: each
/// capability is meant to be wired once, from the constructor.
/// </remarks>
[PublicAPI]
public abstract class InputBase: ControlBase, IAccessKeyCaptionOwner
{
    private static readonly ThemeValueDependency<Rune> _dropDownGlyphThemeDependency = new(
        static theme => theme.GetStyleSet(InputStyle.Default).Normal.DropDownGlyph,
        InvalidationImpact.Render);

    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetStyleSet(InputStyle.Default).ToAppearanceStates();

    /// <summary>Initializes a focusable control participating in Tab traversal.</summary>
    protected InputBase()
    {
        IsFocusable = true;
        IsTabStop = true;
    }

    #region Affixes

    /// <summary>Gets or sets the optional leading edge-pinned decoration that a concrete input
    /// reserves inside its authored content and outside its primary caption or value.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set
        {
            if (SetProperty(ref field, value, GetAffixChangeImpact(field, value)))
            {
                OnAffixChanged();
            }
        }
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration that a concrete input
    /// reserves inside its authored content and outside its primary caption or value.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? EndAffix
    {
        get;
        set
        {
            if (SetProperty(ref field, value, GetAffixChangeImpact(field, value)))
            {
                OnAffixChanged();
            }
        }
    }

    /// <summary>Lets a derived input reconcile cached viewport geometry after either affix
    /// changes.</summary>
    /// <remarks>
    /// Runs after the changed affix's own property has already committed and requested its
    /// invalidation impact, so an override reads <see cref="StartAffix"/> and
    /// <see cref="EndAffix"/> at their new values. The base implementation does nothing; a control
    /// that caches an affix-dependent value box, such as a segmented field's reserved content
    /// bounds, overrides this instead of adding its own <see cref="StartAffix"/>/<see cref="EndAffix"/>
    /// change subscriptions.
    /// </remarks>
    protected virtual void OnAffixChanged()
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
    protected internal override bool AddSelectableTextChildren(List<ControlBase> children)
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
    bool IAccessKeyCaptionOwner.OwnsAccessKeyCaption(ControlBase candidate) =>
        ReferenceEquals(TextControl, candidate);

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
    protected internal override VisualState AmbientAppearanceState =>
        _textSlot is not null ? GetAppearanceState() : base.AmbientAppearanceState;

    /// <inheritdoc/>
    protected internal override bool StateAffectsAmbientAppearance => _textSlot is not null;

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

    #region Selection mark

    /// <summary>Measures one fixed-width selection mark followed or preceded by the owned caption,
    /// with the inherited affixes outside that combined content.</summary>
    /// <remarks>
    /// Call this from <c>MeasureOverride</c> once <see cref="EnableCaption"/> has run - the pair
    /// <see cref="CheckBox"/> and <see cref="RadioButton"/> both call from their constructors. A
    /// control that never enables the caption capability may still call this: the mark reserves its
    /// own width and the caption side reports zero, exactly as if <see cref="Text"/> were empty.
    /// Pair this with <see cref="ArrangeSelectionMarkCaption"/> passing the identical
    /// <paramref name="markWidth"/>, <paramref name="markGap"/>, and <paramref name="affixGap"/>, or
    /// the measured and arranged caption box diverge.
    /// </remarks>
    /// <param name="constraint">The available layout constraint.</param>
    /// <param name="markWidth">The positive terminal-cell width of the formatted mark.</param>
    /// <param name="markGap">The non-negative terminal-cell gap kept between the mark and a present
    /// caption; ignored when the caption is empty or collapsed.</param>
    /// <param name="affixGap">The non-negative terminal-cell gap kept beside each present affix.</param>
    /// <returns>The desired marked-caption size, including affixes.</returns>
    protected Size MeasureSelectionMarkCaption(
        Constraint constraint,
        int markWidth,
        int markGap,
        int affixGap)
    {
        Debug.Assert(markWidth > 0, "A selection mark reserves at least one terminal cell.");
        Debug.Assert(markGap >= 0, "A selection mark gap cannot be negative.");
        Debug.Assert(affixGap >= 0, "An affix gap cannot be negative.");

        var affixes = MeasureAffixes(StartAffix, EndAffix, affixGap);
        var affixInset = affixes.StartCells.Add(affixes.EndCells);
        var content = TextControl;

        if (content is null)
        {
            return new Size(markWidth.Add(affixInset), 1);
        }

        var hasCaption = content.Visibility != Visibility.Collapsed && content.Content.Length != 0;
        var captionGap = hasCaption ? markGap : 0;
        var markedInset = markWidth.Add(captionGap).Add(affixInset);
        var desired = MeasureChild(
            content,
            new Constraint(constraint.Width.Subtract(markedInset), constraint.Height));

        return !hasCaption
            ? new Size(markWidth.Add(affixInset), 1)
            : new Size(
                markedInset.Add(desired.Width.Add(content.Margin.Horizontal)),
                Math.Max(1, desired.Height.Add(content.Margin.Vertical)));
    }

    /// <summary>Arranges the owned caption on the configured side opposite a fixed-width selection
    /// mark, inside inherited affix reservations.</summary>
    /// <remarks>
    /// Call this from <c>ArrangeOverride</c> with the identical <paramref name="markWidth"/>,
    /// <paramref name="markGap"/>, and <paramref name="affixGap"/> passed to the matching
    /// <see cref="MeasureSelectionMarkCaption"/> call for the same pass. A no-op when
    /// <see cref="EnableCaption"/> was never called or <see cref="Text"/> is still empty.
    /// </remarks>
    /// <param name="bounds">The marked-caption bounds.</param>
    /// <param name="markWidth">The positive terminal-cell width of the formatted mark.</param>
    /// <param name="markGap">The non-negative terminal-cell gap kept between the mark and a present
    /// caption; ignored when the caption is empty or collapsed.</param>
    /// <param name="placement">The validated edge that owns the mark.</param>
    /// <param name="affixGap">The non-negative terminal-cell gap kept beside each present affix.</param>
    protected void ArrangeSelectionMarkCaption(
        Rect bounds,
        int markWidth,
        int markGap,
        SelectionMarkPlacement placement,
        int affixGap)
    {
        Debug.Assert(markWidth > 0, "A selection mark reserves at least one terminal cell.");
        Debug.Assert(markGap >= 0, "A selection mark gap cannot be negative.");
        Debug.Assert(Enum.IsDefined(placement), "A selection mark placement must be defined.");
        Debug.Assert(affixGap >= 0, "An affix gap cannot be negative.");

        if (TextControl is not { } content)
        {
            return;
        }

        var affixes = MeasureAffixes(StartAffix, EndAffix, affixGap);
        var deflated = DeflateForAffixes(bounds, affixes);
        var hasCaption = content.Visibility != Visibility.Collapsed && content.Content.Length != 0;
        var captionGap = hasCaption ? markGap : 0;
        var consumed = Math.Min(markWidth.Add(captionGap), deflated.Width);
        var captionBounds = placement == SelectionMarkPlacement.Leading
            ? new Rect(deflated.X.Add(consumed), deflated.Y, deflated.Width - consumed, deflated.Height)
            : new Rect(deflated.X, deflated.Y, deflated.Width - consumed, deflated.Height);
        ArrangeChild(content, captionBounds, ResolvedAxes.Both);
    }

    /// <summary>Paints one selection mark at the configured caption edge and renders inherited
    /// affixes after applying the control's resolved opaque fill.</summary>
    /// <remarks>
    /// Call this from <c>OnRenderContent</c> after <see cref="ArrangeSelectionMarkCaption"/> has
    /// already positioned the caption for the current bounds, passing the identical
    /// <paramref name="markWidth"/> and <paramref name="affixGap"/>. An affix is drawn only when its
    /// full reserved width still fits inside <c>ContentBounds</c> alongside the mark; a starved
    /// layout drops the affix rather than truncating it or the mark.
    /// </remarks>
    /// <param name="canvas">The frame-owned terminal canvas.</param>
    /// <param name="mark">The already formatted mark text.</param>
    /// <param name="markWidth">The positive terminal-cell width reserved for <paramref name="mark"/>.</param>
    /// <param name="placement">The validated edge that owns the mark.</param>
    /// <param name="affixGap">The non-negative terminal-cell gap kept beside each present affix.</param>
    protected void RenderSelectionMark(
        TerminalCanvas canvas,
        ReadOnlySpan<char> mark,
        int markWidth,
        SelectionMarkPlacement placement,
        int affixGap)
    {
        Debug.Assert(markWidth > 0, "A selection mark reserves at least one terminal cell.");
        Debug.Assert(Enum.IsDefined(placement), "A selection mark placement must be defined.");
        Debug.Assert(affixGap >= 0, "An affix gap cannot be negative.");

        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;

        if (HasOpaqueFill(GetAppearanceState()))
        {
            canvas.Clear(Bounds, style);
        }

        var content = ContentBounds;
        var affixes = MeasureAffixes(StartAffix, EndAffix, affixGap);
        var drawStart = affixes.StartCells != 0 &&
            content.Width >= markWidth.Add(affixes.StartCells);
        var drawEnd = affixes.EndCells != 0 &&
            content.Width >= markWidth.Add(affixes.StartCells).Add(affixes.EndCells);
        var renderedAffixes = new AffixMetrics(
            drawStart ? affixes.StartCells : 0,
            drawEnd ? affixes.EndCells : 0);
        var markX = placement == SelectionMarkPlacement.Leading
            ? content.X.SaturatingAdd(renderedAffixes.StartCells)
            : Math.Max(
                content.X,
                content.Right.SaturatingSubtract(renderedAffixes.EndCells).SaturatingSubtract(markWidth));
        RenderAffixes(
            canvas,
            content,
            renderedAffixes,
            drawStart ? StartAffix : null,
            drawEnd ? EndAffix : null,
            style);
        _ = canvas.Draw(
            mark,
            new Point(markX, content.Y),
            style,
            background: BackgroundMode.Transparent);
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
    /// <returns>The command and parameter currently bound to this control.</returns>
    protected CommandBinding CaptureCommand() => new(Command, CommandParameter);

    /// <summary>Invokes the current command binding when it allows execution.</summary>
    /// <remarks>
    /// This extension seam retains its dynamic lookup contract for derived controls. First-party
    /// activation implementations capture their binding before public callbacks instead.
    /// </remarks>
    protected void ExecuteCommandIfAny() => ExecuteCommandIfAny(new CommandBinding(Command, CommandParameter));

    /// <summary>Invokes one previously captured command binding when it allows execution.</summary>
    /// <param name="binding">The command and parameter captured at activation entry.</param>
    /// <remarks>
    /// Execution follows the control's own committed state and events, so a command that cannot
    /// execute never suppresses the control's activation semantics.
    /// </remarks>
    protected static void ExecuteCommandIfAny(CommandBinding binding) => binding.ExecuteIfAny();

    /// <summary>Gets whether the currently bound <see cref="Command"/> allows execution, or no
    /// command is bound.</summary>
    /// <remarks>
    /// A control that presents its disabled appearance while a bound command cannot execute reads
    /// this from its <see cref="ControlBase.GetAppearanceState"/> override, the way
    /// <see cref="Button"/> and <see cref="HyperlinkButton"/> do. Not every
    /// <see cref="InputBase"/> ties its appearance to command executability, so this reports the
    /// raw fact only - a derived control decides on its own whether and how to react to it.
    /// </remarks>
    protected bool IsCommandExecutable => Command is not { } command || command.CanExecute(CommandParameter);

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

        if (!TryCaptureAttachment(out var attachment))
        {
            return;
        }

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
                //
                // A bare Render invalidation only clears this control's own cache; a caption-owning
                // control (Button, HyperlinkButton) presents its bound command's executability
                // through its caption's ambient-inherited face, so the full visual-state cascade is
                // required or the caption keeps its old color while the face changes underneath it.
                PostForCurrentAttachment(attachment, InvalidateVisualStateCore);
            }
            catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException)
            {
            }

            return;
        }

        InvalidateVisualStateCore();
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
            CaptureFailure(
                () => subscription.Command.CanExecuteChanged -= subscription.Handler,
                ref failure);
        }

        failure?.Throw();
    }

    #endregion

    #region Segment editing

    private SegmentFieldBehavior? _segmentEditing;
    private Func<IReadOnlyList<SegmentDescriptor>>? _segmentsProvider;
    private SegmentFieldKeyOptions? _segmentKeyOptions;
    private Action? _beforeSegmentInput;
    private bool _segmentReservesDropDownIndicator;
    private bool _activateFirstSegmentOnFocus;

    /// <summary>Opts into the shared active-segment navigation, digit-entry buffering, pointer
    /// hit-testing, and routed key/pointer dispatch used by every segmented temporal field
    /// control.</summary>
    /// <param name="segmentsProvider">Returns the current, possibly culture- or format-dependent, segment layout.</param>
    /// <param name="applyDigitValue">
    /// Applies a fully or partially typed numeric value to a segment descriptor, clamping as the
    /// control sees fit, and returns whether the value actually changed.
    /// </param>
    /// <param name="incrementSegment">Applies a one-step increment (positive or negative delta) to a segment descriptor and returns whether the value changed.</param>
    /// <param name="clearSegment">Resets a segment descriptor to its lowest representable value and returns whether the value changed.</param>
    /// <param name="keyOptions">The owner's typed step, clear, character, and popup key commands,
    /// consulted by the shared routed key dispatch <see cref="OnEvent"/> now performs on this
    /// capability's behalf.</param>
    /// <param name="reservesDropDownIndicator">Whether <see cref="ResolveSegmentBox"/>,
    /// <see cref="MeasureSegmentedField"/>, and <see cref="RenderSegmentedField"/> reserve
    /// <see cref="DropDownIndicatorReservedWidth"/> for an owned drop-down indicator (true for
    /// <see cref="DateInput"/> and <see cref="DateTimeInput"/>;
    /// false for a field with no popup, such as <see cref="TimeInput"/>).</param>
    /// <param name="activateFirstSegmentOnFocus">Whether each focus entry returns to the first editable segment.</param>
    /// <param name="beforeInput">Optional work the shared routed dispatch runs first, before any
    /// other check, such as lazily seeding the value from the current clock on first interaction.</param>
    /// <returns>The newly constructed behavior, whose focus lifecycle is owned by this base class.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="keyOptions"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Segment editing is already enabled.</exception>
    private protected SegmentFieldBehavior EnableSegmentEditing(
        Func<IReadOnlyList<SegmentDescriptor>> segmentsProvider,
        Func<SegmentDescriptor, int, bool> applyDigitValue,
        Func<SegmentDescriptor, int, bool> incrementSegment,
        Func<SegmentDescriptor, bool> clearSegment,
        SegmentFieldKeyOptions keyOptions,
        bool reservesDropDownIndicator = false,
        bool activateFirstSegmentOnFocus = false,
        Action? beforeInput = null)
    {
        ArgumentNullException.ThrowIfNull(keyOptions);
        VerifyMutable();

        if (_segmentEditing is not null)
        {
            throw new InvalidOperationException("Segment editing is already enabled.");
        }

        _activateFirstSegmentOnFocus = activateFirstSegmentOnFocus;
        _segmentReservesDropDownIndicator = reservesDropDownIndicator;
        _segmentKeyOptions = keyOptions;
        _beforeSegmentInput = beforeInput;
        _segmentsProvider = segmentsProvider;
        _segmentEditing = new SegmentFieldBehavior(
            segmentsProvider,
            applyDigitValue,
            incrementSegment,
            clearSegment,
            () => Invalidate(InvalidationImpact.Render));
        return _segmentEditing;
    }

    /// <summary>Gets whether an owner with an enabled popup capability swallows every routed
    /// event - key and pointer alike - while <see cref="ControlBase.IsPopupOpen"/> is true, before
    /// segment routing ever inspects it.</summary>
    /// <remarks>
    /// Defaults to true, matching every shipped segmented field that also owns a popup
    /// (<see cref="DateInput"/>, <see cref="DateTimeInput"/>): the
    /// popup owns the interaction surface while open, so nothing beneath it - not even a pointer
    /// press into the closed field's own segment box - can reach the field underneath. This is
    /// only consulted when the popup capability is enabled at all (see
    /// <see cref="ControlBase.HasPopup"/>); a segmented field without a popup
    /// (<see cref="TimeInput"/>) never reaches this check, so it never swallows on
    /// this account.
    /// </remarks>
    protected virtual bool SwallowsInputWhilePopupOpen => true;

    /// <summary>Resolves the box editable segment text is drawn into: the content box with the
    /// drop-down indicator's own reserved columns subtracted first when
    /// <see cref="EnableSegmentEditing"/> reserved one, then deflated for any active
    /// <see cref="StartAffix"/>/<see cref="EndAffix"/> - keeping both affixes strictly inboard of
    /// the indicator, and never overlapping it.</summary>
    /// <returns>The rendered segment rectangle, excluding affixes and any reserved indicator.</returns>
    /// <exception cref="InvalidOperationException">Segment editing is not enabled.</exception>
    protected Rect ResolveSegmentBox()
    {
        if (_segmentEditing is null)
        {
            throw new InvalidOperationException("Segment editing is not enabled.");
        }

        var content = ContentBounds;
        var fieldBox = _segmentReservesDropDownIndicator
            ? new Rect(content.X, content.Y, Math.Max(0, content.Width - DropDownIndicatorReservedWidth), 1)
            : content;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        return DeflateForAffixes(fieldBox, affixes);
    }

    /// <summary>Measures a segmented field's full desired size: the current segment layout's
    /// resolved cell width, plus affixes, plus <see cref="DropDownIndicatorReservedWidth"/> when
    /// <see cref="EnableSegmentEditing"/> reserved one, on a single content row.</summary>
    /// <returns>The desired size for the enclosing <c>MeasureOverride</c> to return.</returns>
    /// <exception cref="InvalidOperationException">Segment editing is not enabled.</exception>
    protected Size MeasureSegmentedField()
    {
        if (_segmentsProvider is not { } provider)
        {
            throw new InvalidOperationException("Segment editing is not enabled.");
        }

        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        var width = affixes.StartCells.Add(affixes.EndCells).Add(MeasureSegmentedWidth(provider()));

        if (_segmentReservesDropDownIndicator)
        {
            width = width.Add(DropDownIndicatorReservedWidth);
        }

        return new Size(width, 1);
    }

    /// <summary>Renders one segmented field's affixes, active-segment highlighted value, and
    /// reserved drop-down indicator in the shared layout every segmented field composes: affixes
    /// at the field edges, the shared engine's active-segment highlight (suppressed while an
    /// enabled popup is open), and - only when <see cref="EnableSegmentEditing"/> reserved one -
    /// the drop-down glyph beyond the segment box.</summary>
    /// <param name="canvas">The destination canvas.</param>
    /// <param name="isPlaceholder">Whether the current segments represent a null value.</param>
    /// <exception cref="InvalidOperationException">Segment editing is not enabled.</exception>
    protected void RenderSegmentedField(TerminalCanvas canvas, bool isPlaceholder)
    {
        if (_segmentsProvider is not { } provider)
        {
            throw new InvalidOperationException("Segment editing is not enabled.");
        }

        var content = ContentBounds;
        var style = ResolvedStyle;
        var fieldBox = _segmentReservesDropDownIndicator
            ? new Rect(content.X, content.Y, Math.Max(0, content.Width - DropDownIndicatorReservedWidth), 1)
            : content;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        RenderAffixes(canvas, fieldBox, affixes, StartAffix, EndAffix, style);
        var segmentBox = DeflateForAffixes(fieldBox, affixes);
        RenderSegmentedValue(
            canvas,
            segmentBox,
            provider(),
            isPlaceholder,
            canHighlight: IsFocused && !(HasPopup && IsPopupOpen));

        if (_segmentReservesDropDownIndicator)
        {
            DrawDropDownIndicator(canvas, content, style);
        }
    }

    /// <summary>Clamps the active segment back into range and discards any partially typed digit,
    /// the pair every owner repeats after a layout-affecting property change (a new
    /// <c>Format</c>, culture, or structural flag) shrinks or reorders the segment layout. Does
    /// not itself invalidate: the property setter's own <c>SetProperty</c> or
    /// <c>SetPropertyAndSynchronize</c> call already carries the
    /// <see cref="InvalidationImpact"/> for the property change.</summary>
    /// <exception cref="InvalidOperationException">Segment editing is not enabled.</exception>
    protected void InvalidateSegmentLayout()
    {
        if (_segmentEditing is not { } segments)
        {
            throw new InvalidOperationException("Segment editing is not enabled.");
        }

        segments.ClampActiveSegment();
        segments.ResetDigitBuffer();
    }

    /// <summary>Sums the resolved cell width of a candidate segment layout, without requiring a
    /// concrete value type, so an owner can measure a layout or grade a value transition before
    /// committing it.</summary>
    /// <param name="segments">The ordered literal and editable segments to measure.</param>
    /// <returns>The total terminal-cell width of every segment's rendered text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="segments"/> is null.</exception>
    private protected int MeasureSegmentedWidth(IReadOnlyList<SegmentDescriptor> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var width = 0;

        foreach (var segment in segments)
        {
            width += MeasureCells(segment.Text);
        }

        return width;
    }

    /// <summary>Grades a segmented value transition by its resolved display-width delta: a
    /// same-width transition (for example incrementing a zero-padded segment) needs only
    /// <see cref="InvalidationImpact.Render"/>, while a transition that widens or narrows the
    /// formatted text (a single-digit month or day widening to two digits under a non-padded
    /// format) needs <see cref="InvalidationImpact.Measure"/> so the field box is remeasured
    /// instead of leaving stale geometry behind.</summary>
    /// <param name="previousSegments">The segment layout rendering the previous value.</param>
    /// <param name="candidateSegments">The segment layout rendering the candidate value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="previousSegments"/> or <paramref name="candidateSegments"/> is null.</exception>
    private protected InvalidationImpact ResolveSegmentWidthImpact(
        IReadOnlyList<SegmentDescriptor> previousSegments,
        IReadOnlyList<SegmentDescriptor> candidateSegments) =>
        MeasureSegmentedWidth(previousSegments) == MeasureSegmentedWidth(candidateSegments)
            ? InvalidationImpact.Render
            : InvalidationImpact.Measure;

    /// <summary>Routes a primary pointer press to the segmented field's shared hit testing and
    /// guarded focus transfer.</summary>
    /// <param name="eventArgs">The routed pointer event.</param>
    /// <param name="segmentBox">The rendered segment rectangle, excluding affixes and indicators.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Segment editing is not enabled.</exception>
    private protected void HandleSegmentPointer(PointerEventArgs eventArgs, Rect segmentBox)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (_segmentEditing is not { } segments)
        {
            throw new InvalidOperationException("Segment editing is not enabled.");
        }

        var dispatcher = Dispatcher;
        segments.HandlePointer(
            eventArgs,
            segmentBox,
            CellPolicy.AmbiguousWidth,
            IsFocused,
            RequestFocus,
            () => CanContinueAfterFocus(dispatcher));
    }

    /// <summary>Renders one segmented value with consistent active-selection and null-placeholder
    /// styling while preserving every resolved terminal style channel.</summary>
    /// <param name="canvas">The destination canvas.</param>
    /// <param name="segmentBox">The clipped rectangle available to segment text.</param>
    /// <param name="segments">The ordered literal and editable segments.</param>
    /// <param name="isPlaceholder">Whether the segments represent a null value.</param>
    /// <param name="canHighlight">Whether the active editable segment should be highlighted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="segments"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Segment editing is not enabled.</exception>
    private protected void RenderSegmentedValue(
        TerminalCanvas canvas,
        Rect segmentBox,
        IReadOnlyList<SegmentDescriptor> segments,
        bool isPlaceholder,
        bool canHighlight)
    {
        ArgumentNullException.ThrowIfNull(segments);

        if (_segmentEditing is not { } behavior)
        {
            throw new InvalidOperationException("Segment editing is not enabled.");
        }

        var style = ResolvedStyle;
        var selectionStyle = WithAttributes(style, style.Attributes | TerminalAttributes.Reverse);
        var placeholderStyle = WithAttributes(style, style.Attributes | TerminalAttributes.Dim);
        var clipped = canvas.Clip(segmentBox);
        var x = segmentBox.X;
        var editableIndex = -1;

        foreach (var segment in segments)
        {
            if (x >= segmentBox.Right)
            {
                break;
            }

            if (segment.IsEditable)
            {
                editableIndex++;
            }

            var segmentStyle = canHighlight && segment.IsEditable && editableIndex == behavior.ActiveSegment
                ? selectionStyle
                : isPlaceholder
                    ? placeholderStyle
                    : style;
            _ = clipped.Draw(
                segment.Text.AsSpan(),
                new Point(x, segmentBox.Y),
                segmentStyle,
                background: BackgroundMode.Transparent);
            x += MeasureCells(segment.Text);
        }
    }

    /// <summary>Translates one eligible Up or Down key into a signed segment step.</summary>
    /// <param name="eventArgs">The key event to inspect.</param>
    /// <returns>One, negative one, or null when the key is not an eligible step command.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    private protected static int? ResolveSegmentStepDelta(KeyEventArgs eventArgs) =>
        TryGetStepDelta(eventArgs, out var delta) ? delta : null;

    [Pure]
    private static TerminalStyle WithAttributes(TerminalStyle source, TerminalAttributes attributes) => new(
        source.Foreground,
        source.Background,
        attributes,
        source.Hyperlink,
        source.Underline,
        source.UnderlineColor);

    #endregion

    #region Numeric editing

    private NumericEditBehavior? _numericEditing;

    /// <summary>Opts an in-assembly decimal field into the shared transient-buffer event and focus
    /// lifecycle.</summary>
    /// <param name="buffer">The field's retained transient numeric buffer.</param>
    /// <param name="coordinator">The field's authoritative commit and range coordinator.</param>
    /// <param name="configureBuffer">Applies the current culture and precision policy.</param>
    /// <param name="getDecimalPlaces">Returns the precision used by bound jumps.</param>
    /// <param name="resolveCaretIndex">Maps a pointer cell to an index in <paramref name="buffer"/>.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    /// <exception cref="InvalidOperationException">Numeric editing is already enabled.</exception>
    private protected void EnableNumericEditing(
        NumericEditBuffer buffer,
        NumericInputCommitCoordinator coordinator,
        Action configureBuffer,
        Func<int> getDecimalPlaces,
        Func<Point, int> resolveCaretIndex)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(configureBuffer);
        ArgumentNullException.ThrowIfNull(getDecimalPlaces);
        ArgumentNullException.ThrowIfNull(resolveCaretIndex);
        VerifyMutable();

        if (_numericEditing is not null)
        {
            throw new InvalidOperationException("Numeric editing is already enabled.");
        }

        TabNavigation = TabNavigation.None;
#pragma warning disable IDE0200 // A method group would capture the construction-time ContentBounds value.
        _numericEditing = new NumericEditBehavior(
            buffer,
            coordinator,
            configureBuffer,
            getDecimalPlaces,
            () => IsFocused,
            point => ContentBounds.Contains(point),
            TryFocusForInteraction,
            resolveCaretIndex,
            () => Invalidate(InvalidationImpact.Render));
#pragma warning restore IDE0200
    }

    /// <summary>Draws one enabled numeric editor's affixes, formatted text, transient selection,
    /// placeholder, and focused cursor.</summary>
    /// <param name="canvas">The semantic cell canvas to draw into.</param>
    /// <param name="displayText">The control-specific idle or focused formatted text.</param>
    /// <param name="displaySelection">The focused buffer selection projected into
    /// <paramref name="displayText"/>.</param>
    /// <param name="caretIndex">The focused buffer caret projected into
    /// <paramref name="displayText"/>.</param>
    /// <param name="startAffix">The optional leading fixed decoration.</param>
    /// <param name="endAffix">The optional trailing fixed decoration.</param>
    /// <param name="placeholder">The optional hint shown instead of an empty display.</param>
    /// <param name="cursorShape">The semantic cursor shape to request while focused.</param>
    private protected void RenderNumericInputContent(
        TerminalCanvas canvas,
        string displayText,
        Selection displaySelection,
        int caretIndex,
        Affix? startAffix,
        Affix? endAffix,
        string? placeholder,
        CursorShape cursorShape)
    {
        ArgumentNullException.ThrowIfNull(displayText);

        var content = ContentBounds;

        if (content.Width == 0 || content.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;
        var affixes = MeasureAffixes(startAffix, endAffix, ResolveAffixGap());
        RenderAffixes(canvas, content, affixes, startAffix, endAffix, style);

        var valueBox = DeflateForAffixes(content, affixes);
        canvas.Clear(valueBox, style);

        if (displayText.Length == 0 && placeholder is { Length: > 0 })
        {
            RenderInputPlaceholder(canvas, valueBox, placeholder);
        }
        else
        {
            var clipped = canvas.Clip(new Rect(valueBox.X, valueBox.Y, valueBox.Width, 1));
            _ = clipped.Draw(
                displayText.AsSpan(),
                new Point(valueBox.X, valueBox.Y),
                style,
                background: BackgroundMode.Transparent);

            if (IsFocused && !displaySelection.IsEmpty)
            {
                var selectionStart = Math.Clamp(displaySelection.Start, 0, displayText.Length);
                var selectionEnd = Math.Clamp(displaySelection.End, selectionStart, displayText.Length);
                var selectionX = valueBox.X + MeasureCells(displayText.AsSpan(0, selectionStart));
                var selectedStyle = EditableInputSelectionStyle(style);
                _ = clipped.Draw(
                    displayText.AsSpan(selectionStart, selectionEnd - selectionStart),
                    new Point(selectionX, valueBox.Y),
                    selectedStyle,
                    background: BackgroundMode.Transparent);
            }
        }

        if (IsFocused)
        {
            SetNumericInputCursor(canvas, valueBox, displayText, caretIndex, cursorShape);
        }
    }

    /// <summary>Reasserts one focused numeric editor's cursor after clean cell reuse.</summary>
    /// <param name="canvas">The semantic cell canvas receiving the cursor.</param>
    /// <param name="displayText">The focused formatted text.</param>
    /// <param name="caretIndex">The projected caret index in <paramref name="displayText"/>.</param>
    /// <param name="startAffix">The optional leading fixed decoration.</param>
    /// <param name="endAffix">The optional trailing fixed decoration.</param>
    /// <param name="cursorShape">The semantic cursor shape to request.</param>
    private protected void ReplayNumericInputCursor(
        TerminalCanvas canvas,
        string displayText,
        int caretIndex,
        Affix? startAffix,
        Affix? endAffix,
        CursorShape cursorShape)
    {
        ArgumentNullException.ThrowIfNull(displayText);

        if (!IsFocused)
        {
            return;
        }

        var content = ContentBounds;

        if (content.Width == 0 || content.Height == 0)
        {
            return;
        }

        var affixes = MeasureAffixes(startAffix, endAffix, ResolveAffixGap());
        var valueBox = DeflateForAffixes(content, affixes);
        SetNumericInputCursor(canvas, valueBox, displayText, caretIndex, cursorShape);
    }

    /// <summary>Draws a single-line placeholder as complete grapheme clusters with a dimmed field
    /// style.</summary>
    /// <remarks>
    /// Draws from <paramref name="bounds"/>'s top-left cell, stopping at the first embedded line
    /// break or once the next grapheme cluster would cross <c>bounds.Right</c> - it never wraps or
    /// clips a cluster in half. The dimmed style keeps every other resolved channel (foreground,
    /// background, underline, hyperlink) from <c>ResolvedStyle</c>, so a themed placeholder still
    /// reads as this control's own face rather than a fixed gray. Call this only while the field's
    /// live content is empty; it does not check emptiness itself.
    /// </remarks>
    /// <param name="canvas">The semantic cell canvas to draw into.</param>
    /// <param name="bounds">The available single-line content bounds.</param>
    /// <param name="placeholder">The non-null, non-empty hint text.</param>
    /// <exception cref="ArgumentException"><paramref name="placeholder"/> is null or empty.</exception>
    protected void RenderInputPlaceholder(TerminalCanvas canvas, Rect bounds, string placeholder)
    {
        ArgumentException.ThrowIfNullOrEmpty(placeholder);

        var style = ResolvedStyle;
        var placeholderStyle = new TerminalStyle(
            style.Foreground,
            style.Background,
            style.Attributes | TerminalAttributes.Dim,
            style.Hyperlink,
            style.Underline,
            style.UnderlineColor);
        var x = 0;

        foreach (var grapheme in Graphemes.Enumerate(placeholder))
        {
            var cluster = placeholder.AsSpan(grapheme.Offset, grapheme.Length);

            if (cluster.IndexOfAny('\r', '\n') >= 0)
            {
                break;
            }

            var width = Terminal.Unicode.Width.Measure(cluster, CellPolicy.AmbiguousWidth).Cells;
            var point = new Point(bounds.X.Add(x), bounds.Y);

            if (point.X.Add(width) > bounds.Right)
            {
                break;
            }

            _ = canvas.Draw(cluster, point, placeholderStyle);
            x += width;
        }
    }

    private static TerminalStyle EditableInputSelectionStyle(TerminalStyle current) => new(
        current.Foreground,
        current.Background,
        current.Attributes | TerminalAttributes.Reverse,
        current.Hyperlink,
        current.Underline,
        current.UnderlineColor);

    private void SetNumericInputCursor(
        TerminalCanvas canvas,
        Rect valueBox,
        string displayText,
        int caretIndex,
        CursorShape cursorShape)
    {
        var caretColumn = MeasureCells(displayText.AsSpan(0, Math.Clamp(caretIndex, 0, displayText.Length)));
        var position = new Point(valueBox.X + caretColumn, valueBox.Y);

        if (valueBox.Contains(position) && canvas.Bounds.Contains(position))
        {
            canvas.SetCursor(position, visible: true, cursorShape);
        }
    }

    #endregion

    #region Event routing

    /// <inheritdoc/>
    /// <remarks>
    /// Routes exactly one enabled editing capability's own event dispatch, falling through to
    /// <see cref="ControlBase.OnEvent"/> when neither is enabled or neither claims the event.
    /// Segment routing additionally swallows every event - without reaching
    /// <see cref="ControlBase.OnEvent"/> at all - while an owned popup is open and
    /// <see cref="SwallowsInputWhilePopupOpen"/> holds, matching <see cref="DateInput"/>
    /// and <see cref="DateTimeInput"/>; a segmented field with no popup
    /// (<see cref="TimeInput"/>) never reaches that check at all.
    /// </remarks>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (_numericEditing is not null &&
            EffectiveIsEnabled &&
            EffectiveIsVisible &&
            _numericEditing.HandleEvent(eventArgs))
        {
            return;
        }

        if (_segmentEditing is { } segments)
        {
            _beforeSegmentInput?.Invoke();

            if (!EffectiveIsEnabled || !EffectiveIsVisible)
            {
                base.OnEvent(eventArgs);
                return;
            }

            if (HasPopup && SwallowsInputWhilePopupOpen && IsPopupOpen)
            {
                return;
            }

            if (eventArgs is KeyEventArgs keyEventArgs)
            {
                segments.HandleKey(keyEventArgs, _segmentKeyOptions!);

                if (keyEventArgs.IsHandled)
                {
                    return;
                }
            }
            else if (eventArgs is PointerEventArgs pointer)
            {
                HandleSegmentPointer(pointer, ResolveSegmentBox());

                if (pointer.IsHandled)
                {
                    return;
                }
            }

            if (!eventArgs.IsHandled)
            {
                HandlePressActivation(eventArgs);
            }

            if (!eventArgs.IsHandled)
            {
                base.OnEvent(eventArgs);
            }

            return;
        }

        base.OnEvent(eventArgs);
    }

    #endregion

    #region Stepping

    /// <summary>Translates a scalar-eligible Up/Down arrow key press into a one-step increment delta.</summary>
    /// <param name="eventArgs">The key event to inspect.</param>
    /// <param name="delta">Set to <c>1</c> for Up, <c>-1</c> for Down, or <c>0</c> when unmatched.</param>
    /// <returns>True when the key was Up or Down with no command modifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    protected static bool TryGetStepDelta(KeyEventArgs eventArgs, out int delta)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!KeyboardModifierPolicy.IsScalarNavigationEligible(eventArgs.Stroke.Modifiers))
        {
            delta = 0;
            return false;
        }

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

    /// <summary>Gets the cell width reserved for a drop-down disclosure indicator together with
    /// the one-cell gap it keeps before the field's own content: <see cref="DropDownIndicatorWidth"/>
    /// plus one. Every drop-down-backed field (<see cref="ComboBox"/>,
    /// <see cref="DateInput"/>, <see cref="DateTimeInput"/>) reserves
    /// this exact width ahead of its indicator, whether or not it also uses the segment-editing
    /// capability.</summary>
    protected const int DropDownIndicatorReservedWidth = DropDownIndicatorWidth + 1;

    /// <summary>Resolves the disclosure chevron from the active theme's input style.</summary>
    /// <remarks>
    /// The chevron lives on <see cref="InputStyle"/> rather than on the control, so a theme
    /// targeting a terminal without dependable arrow coverage replaces it for every drop-down
    /// input at once instead of per instance. This resolves appearance through the style set
    /// directly rather than through <c>AppearanceStates</c>, which drops non-appearance members.
    /// </remarks>
    /// <param name="fallback">The code-owned narrow-policy fallback.</param>
    /// <returns>The glyph to draw.</returns>
    protected Rune ResolveDropDownGlyph(Rune fallback) =>
        ResolveControlGlyph(new ControlGlyph(ResolveThemeValue(_dropDownGlyphThemeDependency), fallback));

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

    /// <summary>Handles the conventional Alt+Down and F4 gestures for an enabled owned popup.</summary>
    /// <remarks>
    /// Requires the popup capability (<see cref="ControlBase.EnablePopup"/> or
    /// <see cref="ControlBase.EnablePopupNavigationSession"/>) to already be enabled; call this from
    /// <c>OnEvent</c> for every routed key while the popup is closed. Setting <see cref="IsOpen"/>
    /// runs every configured <c>beforeOpen</c> hook and raises <c>DropDownOpened</c> synchronously,
    /// so a caller that also owns other opening side effects sequences them after this call
    /// returns non-null.
    /// </remarks>
    /// <param name="eventArgs">The routed key event.</param>
    /// <returns>True when an exact opening gesture opens the popup, false when a candidate has
    /// extra modifiers, or null when the key is not an initial opening gesture.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The popup capability is not enabled.</exception>
    protected bool? HandleDropDownOpeningCommand(KeyEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var stroke = eventArgs.Stroke;

        if (!eventArgs.IsInitialKeyDown)
        {
            return null;
        }

        var isAltDownGesture = stroke.Code == Code.Down && (stroke.Modifiers & Modifiers.Alt) != 0;
        var isF4Gesture = stroke.Code == Code.F4;

        if (!isAltDownGesture && !isF4Gesture)
        {
            return null;
        }

        var admitted = isAltDownGesture
            ? KeyboardModifierPolicy.MatchesCommand(stroke.Modifiers, Modifiers.Alt)
            : KeyboardModifierPolicy.MatchesCommand(stroke.Modifiers, Modifiers.None);

        if (admitted)
        {
            IsOpen = true;
        }

        return admitted;
    }

    #endregion

    #region Popup

    /// <summary>Gets or sets whether the owned popup is open.</summary>
    /// <remarks>
    /// The input family's public name for <see cref="ControlBase.IsPopupOpen"/>: a combo box,
    /// date input, or any other popup-backed value editor reads more naturally as
    /// <c>input.IsOpen</c>, so this forwards to the capability-level property and
    /// <see cref="PopupOpenPropertyName"/> publishes this name on every transition.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The popup capability is not enabled, or the
    /// control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    /// <exception cref="Exception">A focus, scope, pointer-cleanup, or user callback fails after committed cleanup.</exception>
    public bool IsOpen
    {
        get => IsPopupOpen;
        set => IsPopupOpen = value;
    }

    /// <inheritdoc/>
    protected override string PopupOpenPropertyName => nameof(IsOpen);

    #endregion

    #region Lifecycle

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);
        _numericEditing?.FocusChanged(focused);

        if (_segmentEditing is { } segments)
        {
            if (focused && _activateFirstSegmentOnFocus)
            {
                segments.ActivateFirstSegment();
            }
            else if (!focused)
            {
                segments.ResetDigitBuffer();
            }

            Invalidate(InvalidationImpact.Render);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            _command = null;
            ExceptionDispatchInfo? failure = null;
            CaptureFailure(ReleaseCommandSubscriptions, ref failure);
            failure?.Throw();
        }
    }

    #endregion
}
