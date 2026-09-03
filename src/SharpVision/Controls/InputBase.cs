// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;
using System.Windows.Input;

using Popups;

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

    /// <summary>Lets an in-assembly input reconcile cached viewport geometry after either affix changes.</summary>
    private protected virtual void OnAffixChanged()
    {
    }

    #endregion

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
        RegisterLifecycleParticipant(_press);
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

    /// <summary>Attempts one semantic activation after validating the source, mutation context,
    /// and effective availability shared by every programmatic activation entry point.</summary>
    /// <param name="cause">The semantic source of the activation attempt.</param>
    /// <returns><see langword="true"/> when activation was admitted and dispatched to
    /// <see cref="Activate"/>; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected bool TryActivate(ActivationCause cause)
    {
        VerifyMutable();
        ArgumentOutOfRangeException.ThrowIfNotDefined(cause);

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            return false;
        }

        Activate(cause);
        return true;
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

    #region Selection mark

    /// <summary>Measures one fixed-width selection mark followed or preceded by the owned caption,
    /// with the inherited affixes outside that combined content.</summary>
    /// <param name="constraint">The available layout constraint.</param>
    /// <param name="markWidth">The positive terminal-cell width of the formatted mark.</param>
    /// <param name="markGap">The non-negative terminal-cell gap beside a present caption.</param>
    /// <param name="affixGap">The non-negative terminal-cell gap beside each present affix.</param>
    /// <returns>The desired marked-caption size, including affixes.</returns>
    private protected Size MeasureSelectionMarkCaption(
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
    /// <param name="bounds">The marked-caption bounds.</param>
    /// <param name="markWidth">The positive terminal-cell width of the formatted mark.</param>
    /// <param name="markGap">The non-negative terminal-cell gap beside a present caption.</param>
    /// <param name="placement">The validated edge that owns the mark.</param>
    /// <param name="affixGap">The non-negative terminal-cell gap beside each present affix.</param>
    private protected void ArrangeSelectionMarkCaption(
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
    /// <param name="canvas">The frame-owned terminal canvas.</param>
    /// <param name="mark">The already formatted mark text.</param>
    /// <param name="markWidth">The positive terminal-cell width reserved for <paramref name="mark"/>.</param>
    /// <param name="placement">The validated edge that owns the mark.</param>
    /// <param name="affixGap">The non-negative terminal-cell gap beside each present affix.</param>
    private protected void RenderSelectionMark(
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

        if (this.HasOpaqueFill(GetAppearanceState()))
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

    #endregion

    #region Segment editing

    private SegmentFieldBehavior? _segmentEditing;
    private bool _activateFirstSegmentOnFocus;

    /// <summary>Opts into the shared active-segment navigation, digit-entry buffering, and
    /// pointer hit-testing state machine used by every segmented temporal field control.</summary>
    /// <param name="segmentsProvider">Returns the current, possibly culture- or format-dependent, segment layout.</param>
    /// <param name="applyDigitValue">
    /// Applies a fully or partially typed numeric value to a segment descriptor, clamping as the
    /// control sees fit, and returns whether the value actually changed.
    /// </param>
    /// <param name="incrementSegment">Applies a one-step increment (positive or negative delta) to a segment descriptor and returns whether the value changed.</param>
    /// <param name="clearSegment">Resets a segment descriptor to its lowest representable value and returns whether the value changed.</param>
    /// <param name="activateFirstSegmentOnFocus">Whether each focus entry returns to the first editable segment.</param>
    /// <returns>The newly constructed behavior, whose focus lifecycle is owned by this base class.</returns>
    /// <exception cref="InvalidOperationException">Segment editing is already enabled.</exception>
    private protected SegmentFieldBehavior EnableSegmentEditing(
        Func<IReadOnlyList<SegmentDescriptor>> segmentsProvider,
        Func<SegmentDescriptor, int, bool> applyDigitValue,
        Func<SegmentDescriptor, int, bool> incrementSegment,
        Func<SegmentDescriptor, bool> clearSegment,
        bool activateFirstSegmentOnFocus = false)
    {
        VerifyMutable();

        if (_segmentEditing is not null)
        {
            throw new InvalidOperationException("Segment editing is already enabled.");
        }

        _activateFirstSegmentOnFocus = activateFirstSegmentOnFocus;
        _segmentEditing = new SegmentFieldBehavior(
            segmentsProvider,
            applyDigitValue,
            incrementSegment,
            clearSegment,
            () => Invalidate(InvalidationImpact.Render));
        return _segmentEditing;
    }

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
            RequestNumericEditingFocus,
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
    /// <param name="canvas">The semantic cell canvas to draw into.</param>
    /// <param name="bounds">The available single-line content bounds.</param>
    /// <param name="placeholder">The non-empty hint text.</param>
    private protected void RenderInputPlaceholder(TerminalCanvas canvas, Rect bounds, string placeholder)
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

    private bool RequestNumericEditingFocus()
    {
        var dispatcher = Dispatcher;
        _ = RequestFocus();
        return CanContinueAfterFocus(dispatcher);
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

    /// <inheritdoc/>
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
        return ResolveThemeValue(_dropDownGlyphThemeDependency)
            .Resolve(fallback, CellPolicy.AmbiguousWidth);
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

    /// <summary>Handles the conventional Alt+Down and F4 gestures for an enabled owned popup.</summary>
    /// <param name="eventArgs">The routed key event.</param>
    /// <returns>True when an exact opening gesture opens the popup, false when a candidate has
    /// extra modifiers, or null when the key is not an initial opening gesture.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The popup capability is not enabled.</exception>
    private protected bool? HandleDropDownOpeningCommand(KeyEventArgs eventArgs)
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

    private PopupDropDownCoordinator? _popupCoordinator;

    /// <summary>Opts a derived input into an owned popup with shared open/close publication,
    /// modal composition, focus restoration, and a framework-part slot.</summary>
    /// <param name="content">The non-null popup content, also used as its focus scope.</param>
    /// <param name="placement">The preferred anchor-relative placement.</param>
    /// <param name="focusOnOpen">Whether opening transfers focus to the first eligible descendant of <paramref name="content"/>.</param>
    /// <param name="popupTabNavigation">The Tab-traversal boundary the owned popup itself applies
    /// to <paramref name="content"/>.</param>
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
        Action? beforeCloseFocusRestore = null) =>
        EnablePopupCore(
            content,
            placement,
            focusOnOpen,
            popupTabNavigation,
            beforeOpen,
            beforeCloseFocusRestore,
            beginSession: null,
            handleNavigationKey: null,
            cancelSession: null,
            acceptSession: null);

    /// <summary>Opts an in-assembly input into the owned-popup lifecycle plus provisional
    /// navigation delegated once from the owner's preview route.</summary>
    /// <param name="content">The non-null popup content, also used as its focus scope.</param>
    /// <param name="placement">The preferred anchor-relative placement.</param>
    /// <param name="focusOnOpen">Whether opening transfers focus into <paramref name="content"/>.</param>
    /// <param name="popupTabNavigation">The popup content's Tab-traversal boundary.</param>
    /// <param name="beforeOpen">Optional work run before the popup opens.</param>
    /// <param name="beforeCloseFocusRestore">Optional work run before closing focus restoration.</param>
    /// <param name="beginSession">Snapshots and seeds one provisional session.</param>
    /// <param name="handleNavigationKey">Delegates one live owner-preview navigation stroke.</param>
    /// <param name="cancelSession">Restores or rebases a session closed without acceptance.</param>
    /// <param name="acceptSession">Commits provisional state before an accepted close.</param>
    /// <returns>The newly constructed, owned popup.</returns>
    private protected Popup EnablePopupNavigationSession(
        ControlBase content,
        PopupPlacement placement = PopupPlacement.Below,
        bool focusOnOpen = false,
        TabNavigation popupTabNavigation = TabNavigation.None,
        Action? beforeOpen = null,
        Action? beforeCloseFocusRestore = null,
        Action? beginSession = null,
        Func<KeyEventArgs, bool>? handleNavigationKey = null,
        Action? cancelSession = null,
        Action? acceptSession = null) =>
        EnablePopupCore(
            content,
            placement,
            focusOnOpen,
            popupTabNavigation,
            beforeOpen,
            beforeCloseFocusRestore,
            beginSession,
            handleNavigationKey,
            cancelSession,
            acceptSession);

    private Popup EnablePopupCore(
        ControlBase content,
        PopupPlacement placement,
        bool focusOnOpen,
        TabNavigation popupTabNavigation,
        Action? beforeOpen,
        Action? beforeCloseFocusRestore,
        Action? beginSession,
        Func<KeyEventArgs, bool>? handleNavigationKey,
        Action? cancelSession,
        Action? acceptSession)
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
            SuppressCloseOtherPopups = true,
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
            beforeCloseFocusRestore,
            beginSession: beginSession,
            handleNavigationKey: handleNavigationKey,
            cancelSession: cancelSession,
            acceptSession: acceptSession);
        return popup;
    }

    /// <summary>Commits the active popup session's provisional state and closes the owned popup.</summary>
    /// <remarks>Concrete drop-down owners call this only after target-owned keyboard or pointer
    /// activation has accepted the provisional item. The operation is a no-op when the popup has
    /// no active open session.</remarks>
    /// <exception cref="InvalidOperationException">The popup capability is not enabled or the
    /// control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    /// <exception cref="Exception">An acceptance or close callback fails after close cleanup completes.</exception>
    protected void AcceptPopupAndClose()
    {
        VerifyMutable();

        if (_popupCoordinator is not { } coordinator)
        {
            throw new InvalidOperationException("The popup capability is not enabled.");
        }

        coordinator.AcceptAndClose();
    }

    /// <summary>Retires the active popup session and begins a fresh one without closing the popup.</summary>
    /// <remarks>An in-assembly drop-down owner calls this from its acceptance callback when a
    /// selection callback committed a newer selection than the accepted row: the newer decision
    /// keeps the popup open over the current state instead of being dismissed by the superseded
    /// acceptance, and no close or reopen is published. A no-op without an open session.</remarks>
    /// <exception cref="InvalidOperationException">The popup capability is not enabled or the
    /// control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    private protected void RestartPopupNavigationSession()
    {
        VerifyMutable();

        if (_popupCoordinator is not { } coordinator)
        {
            throw new InvalidOperationException("The popup capability is not enabled.");
        }

        coordinator.RestartSession();
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
            CaptureFailure(
                () => subscription.Command.CanExecuteChanged -= subscription.Handler,
                ref failure);
        }

        failure?.Throw();
    }

    /// <summary>Gets the current owned-popup request version for continuation validation.</summary>
    internal ulong PopupTransitionVersion => _popupCoordinator is { } coordinator
        ? coordinator.TransitionVersion
        : throw new InvalidOperationException("The popup capability is not enabled.");

    /// <summary>Gets the current owned-popup navigation-session identity for stale-continuation validation.</summary>
    internal ulong PopupSessionGeneration => _popupCoordinator is { } coordinator
        ? coordinator.SessionGeneration
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
    protected override void OnAttached()
    {
        base.OnAttached();
        _popupCoordinator?.OnOwnerAttached();
    }

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
        _popupCoordinator?.OnOwnerUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            _command = null;
            ExceptionDispatchInfo? failure = null;
            CaptureFailure(() => _popupCoordinator?.Detach(), ref failure);
            CaptureFailure(ReleaseCommandSubscriptions, ref failure);
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
