// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using Collections;

using Scrolling;

using SharpVision.Terminal.Input;

/// <summary>Provides a freely editable text field with an owner-managed suggestion popup.</summary>
/// <remarks>
/// The retained editor is the sole focus target. Resolver results are exposed as copied snapshots,
/// and only explicit acceptance may replace the editor text.
/// </remarks>
[PublicAPI]
public sealed class SuggestionInput: CompositeControlBase
{
    private readonly RetainedPartProperty<Affix?> _endAffix;
    private readonly TextInput _input;
    private readonly RetainedPartProperty<ItemTemplate> _itemTemplate;
    private readonly ListView _list;
    private readonly CallbackTransitionStream _minimumPrefixLengthTransitions = new();
    private readonly RetainedPartProperty<string?> _placeholder;
    private readonly LatestControlOperation _resolutionOperation = new();
    private readonly CallbackTransitionStream _resolverTransitions = new();
    private readonly RetainedPartProperty<Length> _rowHeight;
    private readonly RetainedPartProperty<ScrollBars> _scrollBars;
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;
    private readonly RetainedPartProperty<ShowScrollBars> _showScrollBars;
    private readonly RetainedPartProperty<Affix?> _startAffix;
    private int? _currentSnapshotGeneration;
    private int? _openingSnapshotGeneration;
    private int? _pendingFirstSelectionResolutionGeneration;
    private int _resolutionGeneration;
    private int _openingSelectedIndex = -1;
    private int _openingCurrentIndex = -1;
    private ulong _pendingFirstSelectionSessionGeneration;
    private Dispatcher? _pendingFirstSelectionDispatcher;
    private SuggestionInputAcceptanceTransaction? _acceptanceTransaction;
    private ControlAttachmentToken? _acceptanceAttachment;
    private ActivationCause _acceptanceCause;
    private int _acceptanceResolutionGeneration;
    private ulong _acceptanceGeneration;
    private ulong _textCommitVersion;
    private int _resolutionLifecycleCleanupDepth;
    private bool _wantsOpen;

    #region Construction and events

    /// <summary>Initializes an empty suggestion input with one retained editor and connected popup.</summary>
    public SuggestionInput()
    {
        _input = new TextInput { HorizontalAlignment = HorizontalAlignment.Stretch };
        _input.TextChanged += OnTextChanged;
        _list = new ListView
        {
            IsTabStop = false,
            SelectionMode = ListSelectionMode.Single
        };
        _list.ItemActivationStarting += OnItemActivationStarting;
        _list.ItemInvoked += OnItemInvoked;
        _ = EnablePopupNavigationSession(
            _list,
            focusOnOpen: false,
            anchor: _input,
            ownerInitialFocus: _input,
            requestFocus: _input.Focus,
            contentHeightLimit: Length.Cells(8),
            partKey: "suggestions",
            beginSession: BeginNavigationSession,
            handleNavigationKey: HandleNavigationKey,
            cancelSession: CancelNavigationSession,
            acceptSession: AcceptNavigationSession,
            acceptCurrent: AcceptCurrent,
            markEnterHandledWithoutAcceptance: true,
            enterRequiresActivationEligibleModifiers: true);
        _scrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ForwardingDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, _list, nameof(ScrollBarStyle));
        InitializeContent(_input);
        _placeholder = ForwardPartProperty(
            _input,
            nameof(TextInput.Placeholder),
            nameof(Placeholder),
            () => _input.Placeholder,
            value => _input.Placeholder = value);
        _startAffix = ForwardPartProperty(
            _input,
            nameof(TextInput.StartAffix),
            nameof(StartAffix),
            () => _input.StartAffix,
            value => _input.StartAffix = value);
        _endAffix = ForwardPartProperty(
            _input,
            nameof(TextInput.EndAffix),
            nameof(EndAffix),
            () => _input.EndAffix,
            value => _input.EndAffix = value);
        _itemTemplate = ForwardPartProperty(
            _list,
            nameof(ListView.ItemTemplate),
            nameof(ItemTemplate),
            () => _list.ItemTemplate,
            value => _list.ItemTemplate = value);
        _rowHeight = ForwardPartProperty(
            _list,
            nameof(ListView.RowHeight),
            nameof(RowHeight),
            () => _list.RowHeight,
            value => _list.RowHeight = value);
        _scrollBars = ForwardPartProperty(
            _list,
            nameof(ListView.ScrollBars),
            nameof(ScrollBars),
            () => _list.ScrollBars,
            value => _list.ScrollBars = value);
        _showScrollBars = ForwardPartProperty(
            _list,
            nameof(ListView.ShowScrollBars),
            nameof(ShowScrollBars),
            () => _list.ShowScrollBars,
            value => _list.ShowScrollBars = value);
    }

    /// <summary>Raised after the copied current suggestion snapshot changes.</summary>
    public event EventHandler? SuggestionsChanged;

    /// <summary>Raised when the still-current resolver request fails.</summary>
    public event EventHandler<SuggestionResolutionFailedEventArgs>? ResolutionFailed;

    /// <summary>Raised after explicit keyboard or pointer acceptance commits one suggestion.</summary>
    public event EventHandler<ItemInvokedEventArgs>? SuggestionAccepted;

    #endregion

    #region Text and resolution

    /// <summary>Gets or sets the freely editable non-null text.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value violates the retained editor policy.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string Text
    {
        get => _input.Text;
        set => _input.Text = value;
    }

    /// <summary>Gets or sets the optional placeholder displayed while the editor is empty.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string? Placeholder
    {
        get => _placeholder.Value;
        set => _placeholder.Value = value;
    }

    /// <summary>Gets or sets the optional leading editor affix.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? StartAffix
    {
        get => _startAffix.Value;
        set => _startAffix.Value = value;
    }

    /// <summary>Gets or sets the optional trailing editor affix.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? EndAffix
    {
        get => _endAffix.Value;
        set => _endAffix.Value = value;
    }

    /// <summary>Gets or sets the minimum extended-grapheme count eligible for suggestion resolution.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int MinimumPrefixLength
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            var resolutionGeneration = _resolutionGeneration;

            if (!SetTransitionProperty(
                    ref field,
                    value,
                    InvalidationImpact.None,
                    _minimumPrefixLengthTransitions,
                    out var transition))
            {
                return;
            }

            if (_resolutionGeneration == resolutionGeneration)
            {
                transition.CaptureIfCurrent(BeginResolution);
            }

            transition.ThrowIfFailed();
        }
    } = 1;

    /// <summary>Gets or sets the optional asynchronous suggestion resolver.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public SuggestionResolver? Resolver
    {
        get;
        set
        {
            var resolutionGeneration = _resolutionGeneration;

            if (!SetTransitionProperty(
                    ref field,
                    value,
                    InvalidationImpact.None,
                    _resolverTransitions,
                    out var transition,
                    comparer: ReferenceEqualityComparer.Instance))
            {
                return;
            }

            if (_resolutionGeneration == resolutionGeneration)
            {
                transition.CaptureIfCurrent(BeginResolution);
            }

            transition.ThrowIfFailed();
        }
    }

    /// <summary>Gets the copied current suggestion snapshot.</summary>
    public IReadOnlyList<object?> Suggestions => _list.Items;

    /// <summary>Gets whether the current resolver request has not completed.</summary>
    public bool IsResolving { get; private set; }

    /// <summary>Gets the most recently started asynchronous resolution observation. Tests await
    /// this seam before asserting that stale, detached, or rejected completion work reached its
    /// apply-or-discard boundary.</summary>
    internal Task? LastResolutionObservation { get; private set; }

    /// <summary>Gets the completed boundary recorded before the most recent directly executed
    /// settlement. Tests use this seam to prove inline callback failures do not leave a second
    /// task-based fault channel.</summary>
    internal Task? LastInlineResolutionObservation { get; private set; }

    /// <summary>Gets whether detached or pre-arrange suggestion selection remains queued. Tests
    /// use this seam to prove popup and attachment cleanup release deferred session work.</summary>
    internal bool HasPendingFirstSuggestionSelection =>
        _pendingFirstSelectionResolutionGeneration is not null;

    /// <summary>Gets or sets a test synchronization callback invoked after detached completion
    /// acquires exclusive publication authority and before it mutates retained state.</summary>
    internal Action? BeforeDetachedResolutionPublication { get; set; }

    /// <summary>Starts a fresh resolution for the current text and makes current results eligible to open.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void Refresh()
    {
        VerifyMutable();
        _wantsOpen = true;
        BeginResolution();
    }

    #endregion

    #region Presentation

    /// <summary>Gets or sets the detached-control factory used to realize each suggestion row.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">Candidate output is invalid or duplicated.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ItemTemplate ItemTemplate
    {
        get => _itemTemplate.Value;
        set => _itemTemplate.Value = value;
    }

    /// <summary>Gets or sets the optional projection used to obtain accepted text from a suggestion.</summary>
    /// <remarks>
    /// Null selects invariant-culture <see cref="Convert.ToString(object?, IFormatProvider?)"/>,
    /// normalized to an empty string when that conversion returns null.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Func<object?, string>? TextSelector
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    }

    /// <summary>Gets or sets the automatic, fixed, or viewport-relative uniform suggestion-row height.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A fixed or percentage value is zero.</exception>
    /// <exception cref="ArgumentException">The value uses proportional sizing.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Length RowHeight
    {
        get => _rowHeight.Value;
        set => _rowHeight.Value = value;
    }

    /// <summary>Gets or sets the axes available to the suggestion-list overflow host.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBars ScrollBars
    {
        get => _scrollBars.Value;
        set => _scrollBars.Value = value;
    }

    /// <summary>Gets or sets the suggestion-list scrollbar reservation policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get => _showScrollBars.Value;
        set => _showScrollBars.Value = value;
    }

    /// <summary>Gets or sets the complete local style for the owned suggestion-list rails.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBarStyle? ScrollBarStyle
    {
        get => _scrollBarStyle.Local;
        set => _scrollBarStyle.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned suggestion-list rail style.</summary>
    public ScrollBarStyle ActualScrollBarStyle => _scrollBarStyle.Actual;

    /// <summary>Gets or sets whether a non-empty current suggestion snapshot is open.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool IsOpen
    {
        get => IsPopupOpen;
        set => IsPopupOpen = value;
    }

    /// <inheritdoc/>
    protected override string PopupOpenPropertyName => nameof(IsOpen);

    /// <inheritdoc/>
    /// <remarks>Opening is deferred while a resolution is pending or the current snapshot is
    /// empty; the request is remembered and honored once eligible suggestions arrive.</remarks>
    public override bool IsPopupOpen
    {
        get => base.IsPopupOpen;
        set
        {
            VerifyMutable();

            if (!value)
            {
                _wantsOpen = false;
                CancelPendingAcceptance();
                base.IsPopupOpen = false;
                return;
            }

            _wantsOpen = true;

            if (!EffectiveIsEnabled ||
                !EffectiveIsVisible ||
                IsResolving ||
                _currentSnapshotGeneration != _resolutionGeneration ||
                Suggestions.Count == 0)
            {
                return;
            }

            base.IsPopupOpen = true;
        }
    }

    /// <summary>Focuses the retained editor and makes current or freshly resolved suggestions eligible to open.</summary>
    /// <returns>True when the mounted editor accepted focus.</returns>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool Open()
    {
        VerifyMutable();
        _wantsOpen = true;

        if (_currentSnapshotGeneration != _resolutionGeneration)
        {
            BeginResolution();
        }
        else if (EffectiveIsEnabled && EffectiveIsVisible && Suggestions.Count > 0)
        {
            base.IsPopupOpen = true;
        }

        return _input.Focus();
    }

    /// <summary>Closes suggestions while preserving the current editor text and any current resolver request.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void Close() => IsOpen = false;

    #endregion

    #region Popup navigation and acceptance

    private void BeginNavigationSession()
    {
        _openingSnapshotGeneration = _currentSnapshotGeneration;
        _openingSelectedIndex = _list.SelectedIndex;
        _openingCurrentIndex = _list.ActiveIndex;
        CancelPendingAcceptance();
    }

    /// <summary>Activates the provisional row for the shared Enter-acceptance prologue, when the
    /// snapshot it would accept is still current.</summary>
    /// <remarks>An open suggestion session owns Enter even while the newest request is unresolved -
    /// the coordinator marks the stroke handled regardless of this method's result (see the
    /// <c>markEnterHandledWithoutAcceptance: true</c> argument at the enable call), which prevents
    /// the editor's ordinary Submitted event from accepting an older row.</remarks>
    /// <param name="eventArgs">The routed Enter key event.</param>
    /// <returns>True when the current snapshot activated; false when it is not yet safe to
    /// accept.</returns>
    private bool AcceptCurrent(KeyEventArgs eventArgs)
    {
        if (!CanAcceptCurrentSnapshot())
        {
            return false;
        }

        _ = _list.ActivateCurrent(ActivationCause.Keyboard, Code.Enter, eventArgs.Stroke.Modifiers);
        return true;
    }

    private bool HandleNavigationKey(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;

        if (eventArgs.IsInitialKeyDown &&
            stroke.Code == Code.Tab &&
            KeyboardModifierPolicy.IsTabTraversalEligible(stroke.Modifiers))
        {
            base.IsPopupOpen = false;
            return false;
        }

        var moved = _list.HandleCurrentNavigationKey(eventArgs);

        if (moved)
        {
            _list.SetProvisionalSelectionIndex(_list.ActiveIndex);
        }

        return moved;
    }

    private void CancelNavigationSession()
    {
        ClearPendingFirstSuggestionSelection();
        CancelPendingAcceptance();

        if (_openingSnapshotGeneration is not { } openingGeneration ||
            _currentSnapshotGeneration != openingGeneration ||
            _resolutionGeneration != openingGeneration ||
            !IsValidSuggestionIndex(_openingSelectedIndex) ||
            !IsValidSuggestionIndex(_openingCurrentIndex))
        {
            return;
        }

        _list.SetProvisionalSelectionIndex(_openingSelectedIndex);
        _list.SetProvisionalCurrentIndex(_openingCurrentIndex);
    }

    private void AcceptNavigationSession()
    {
        if (_acceptanceTransaction is not { } transaction ||
            !IsAcceptanceReadyToCommit(transaction))
        {
            return;
        }

        Text = transaction.AcceptedText;
    }

    private void OnItemActivationStarting(object? sender, ItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        CancelPendingAcceptance();
        var resolutionGeneration = _resolutionGeneration;

        if (!CanAcceptCurrentSnapshot() ||
            !TryCaptureAttachment(out var attachment) ||
            !IsCurrentSuggestionItem(eventArgs.Index, eventArgs.Item))
        {
            return;
        }

        var identity = new PopupItemActivationIdentity(
            eventArgs.ActivationGeneration,
            eventArgs.Index,
            PopupTransitionVersion,
            PopupSessionGeneration);
        var acceptanceGeneration = _acceptanceGeneration;
        var selector = TextSelector;
        var acceptedText = selector is null
            ? Convert.ToString(eventArgs.Item, CultureInfo.InvariantCulture) ?? string.Empty
            : selector(eventArgs.Item) ?? throw new InvalidOperationException(
                "A suggestion text selector returned null.");

        if (_acceptanceGeneration != acceptanceGeneration ||
            !IsCurrentActivationStarting(eventArgs, identity, resolutionGeneration, attachment))
        {
            return;
        }

        _acceptanceTransaction = new SuggestionInputAcceptanceTransaction(
            identity,
            eventArgs.Item,
            acceptedText,
            acceptanceGeneration);
        _acceptanceAttachment = attachment;
        _acceptanceCause = eventArgs.Cause;
        _acceptanceResolutionGeneration = resolutionGeneration;
    }

    private void OnItemInvoked(object? sender, ItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        var transaction = _acceptanceTransaction;
        var attachment = _acceptanceAttachment;
        var resolutionGeneration = _acceptanceResolutionGeneration;

        if (transaction is not { } prepared ||
            attachment is not { } capturedAttachment ||
            _acceptanceGeneration != prepared.Generation ||
            !ReferenceEquals(eventArgs.Item, prepared.Item) ||
            !IsCurrentInvocation(
                eventArgs,
                prepared.Activation,
                resolutionGeneration,
                capturedAttachment))
        {
            if (transaction is { } stale)
            {
                ClearAcceptance(stale.Generation);
            }

            return;
        }

        ExceptionDispatchInfo? failure = null;
        CaptureFailure(AcceptPopupAndClose, ref failure);

        if (IsCurrentAcceptance(prepared))
        {
            var cause = _acceptanceCause;
            ClearAcceptance(prepared.Generation);
            CaptureFailure(
                () => SuggestionAccepted?.Invoke(
                    this,
                    new ItemInvokedEventArgs(
                        prepared.Activation.ItemIndex,
                        prepared.Item,
                        cause)),
                ref failure);
        }
        else
        {
            ClearAcceptance(prepared.Generation);
        }

        failure?.Throw();
    }

    [Pure]
    private bool IsCurrentInvocation(
        ItemInvokedEventArgs eventArgs,
        PopupItemActivationIdentity identity,
        int resolutionGeneration,
        ControlAttachmentToken attachment) =>
        CanAcceptCurrentSnapshot() &&
        _resolutionGeneration == resolutionGeneration &&
        _currentSnapshotGeneration == resolutionGeneration &&
        IsCurrentAttachment(attachment) &&
        eventArgs.ActivationGeneration == identity.ItemGeneration &&
        eventArgs.Index == identity.ItemIndex &&
        eventArgs.Index == _list.SelectedIndex &&
        eventArgs.Index == _list.ActiveIndex &&
        IsCurrentSuggestionItem(eventArgs.Index, eventArgs.Item) &&
        PopupTransitionVersion == identity.PopupTransitionVersion &&
        PopupSessionGeneration == identity.PopupSessionGeneration;

    [Pure]
    private bool IsCurrentActivationStarting(
        ItemInvokedEventArgs eventArgs,
        PopupItemActivationIdentity identity,
        int resolutionGeneration,
        ControlAttachmentToken attachment) =>
        CanAcceptCurrentSnapshot() &&
        _resolutionGeneration == resolutionGeneration &&
        _currentSnapshotGeneration == resolutionGeneration &&
        IsCurrentAttachment(attachment) &&
        eventArgs.ActivationGeneration == identity.ItemGeneration &&
        eventArgs.Index == identity.ItemIndex &&
        IsCurrentSuggestionItem(eventArgs.Index, eventArgs.Item) &&
        PopupTransitionVersion == identity.PopupTransitionVersion &&
        PopupSessionGeneration == identity.PopupSessionGeneration;

    [Pure]
    private bool CanAcceptCurrentSnapshot() =>
        !IsDisposed &&
        !IsDisposing &&
        Dispatcher is not null &&
        EffectiveIsEnabled &&
        EffectiveIsVisible &&
        IsOpen &&
        !IsResolving &&
        !_resolutionOperation.HasCurrent &&
        _currentSnapshotGeneration == _resolutionGeneration;

    [Pure]
    private bool IsCurrentSuggestionItem(int index, object? item) =>
        (uint) index < (uint) Suggestions.Count &&
        ReferenceEquals(Suggestions[index], item);

    [Pure]
    private bool IsValidSuggestionIndex(int index) =>
        index == -1 || (uint) index < (uint) Suggestions.Count;

    [Pure]
    private bool IsAcceptanceReadyToCommit(SuggestionInputAcceptanceTransaction transaction) =>
        !IsDisposed &&
        !IsDisposing &&
        _acceptanceTransaction is { } current &&
        current.Generation == transaction.Generation &&
        _acceptanceGeneration == transaction.Generation &&
        _acceptanceAttachment is { } attachment &&
        IsCurrentAttachment(attachment) &&
        IsOpen &&
        PopupTransitionVersion == transaction.Activation.PopupTransitionVersion &&
        PopupSessionGeneration == transaction.Activation.PopupSessionGeneration;

    [Pure]
    private bool IsCurrentAcceptance(SuggestionInputAcceptanceTransaction transaction) =>
        !IsDisposed &&
        !IsDisposing &&
        _acceptanceTransaction is { } current &&
        current.Generation == transaction.Generation &&
        _acceptanceGeneration == transaction.Generation &&
        _acceptanceAttachment is { } attachment &&
        IsCurrentAttachment(attachment) &&
        string.Equals(Text, transaction.AcceptedText, StringComparison.Ordinal) &&
        !IsOpen;

    private ulong AdvanceAcceptanceGeneration()
    {
        _acceptanceGeneration++;

        if (_acceptanceGeneration == 0)
        {
            _acceptanceGeneration++;
        }

        _acceptanceTransaction = null;
        _acceptanceAttachment = null;
        _acceptanceCause = default;
        _acceptanceResolutionGeneration = 0;
        return _acceptanceGeneration;
    }

    private void CancelPendingAcceptance() => _ = AdvanceAcceptanceGeneration();

    private void ClearAcceptance(ulong generation)
    {
        if (_acceptanceGeneration != generation)
        {
            return;
        }

        _acceptanceTransaction = null;
        _acceptanceAttachment = null;
        _acceptanceCause = default;
        _acceptanceResolutionGeneration = 0;
    }

    private void RequestFirstSuggestionSelection(int resolutionGeneration)
    {
        _pendingFirstSelectionResolutionGeneration = resolutionGeneration;
        _pendingFirstSelectionSessionGeneration = PopupSessionGeneration;
        SchedulePendingFirstSuggestionSelection();
    }

    private void SchedulePendingFirstSuggestionSelection()
    {
        if (_pendingFirstSelectionResolutionGeneration is null ||
            Dispatcher is not { } dispatcher ||
            ReferenceEquals(_pendingFirstSelectionDispatcher, dispatcher))
        {
            return;
        }

        if (_pendingFirstSelectionDispatcher is { } previousDispatcher)
        {
            previousDispatcher.Idle -= OnPendingFirstSuggestionSelectionIdle;
        }

        _pendingFirstSelectionDispatcher = dispatcher;
        dispatcher.Idle += OnPendingFirstSuggestionSelectionIdle;
        dispatcher.RequestIdle();
    }

    private void OnPendingFirstSuggestionSelectionIdle(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (_pendingFirstSelectionResolutionGeneration is not { } resolutionGeneration)
        {
            ClearPendingFirstSuggestionSelection();
            return;
        }

        var sessionGeneration = _pendingFirstSelectionSessionGeneration;
        ClearPendingFirstSuggestionSelection();

        if (!IsDisposed &&
            !IsResolving &&
            resolutionGeneration == _resolutionGeneration &&
            _currentSnapshotGeneration == resolutionGeneration &&
            IsOpen &&
            PopupSessionGeneration == sessionGeneration &&
            _list.ActiveIndex < 0)
        {
            SelectFirstAvailableSuggestion(commitCurrent: true);
        }
    }

    private void ClearPendingFirstSuggestionSelection()
    {
        if (_pendingFirstSelectionDispatcher is { } dispatcher)
        {
            _pendingFirstSelectionDispatcher = null;
            dispatcher.Idle -= OnPendingFirstSuggestionSelectionIdle;
        }

        _pendingFirstSelectionResolutionGeneration = null;
        _pendingFirstSelectionSessionGeneration = 0;
    }

    private void SelectFirstAvailableSuggestion(bool commitCurrent)
    {
        var first = _list.ResolveCollapsedNavigationIndex(
            new KeyEventArgs(new Stroke(
                Code.Home,
                character: null,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)),
            currentIndex: -1);

        if (first >= 0)
        {
            // Result publication can precede the popup's first arrange. Seed the owner's
            // visual selection without asking ListView to reveal a row against stale geometry.
            // The queued idle pass commits current after replacement rows have been arranged.
            _list.SetProvisionalSelectionIndex(first);

            if (commitCurrent)
            {
                _list.SetProvisionalCurrentIndex(first);
            }
        }
    }

    #endregion

    #region Resolution

    private void OnTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var committedText = Text;
        var version = ++_textCommitVersion;
        var resolutionGeneration = _resolutionGeneration;
        var preservePendingAcceptance =
            _acceptanceTransaction is { } acceptance &&
            _acceptanceGeneration == acceptance.Generation &&
            string.Equals(committedText, acceptance.AcceptedText, StringComparison.Ordinal);
        _wantsOpen |= Resolver is not null;
        ExceptionDispatchInfo? failure = null;
        CaptureFailure(
            () => NotifyPropertyChanged(nameof(Text), InvalidationImpact.None),
            ref failure);

        if (!IsDisposed &&
            _textCommitVersion == version &&
            string.Equals(Text, committedText, StringComparison.Ordinal) &&
            _resolutionGeneration == resolutionGeneration)
        {
            CaptureFailure(
                () => BeginResolution(preservePendingAcceptance),
                ref failure);
        }

        failure?.Throw();
    }

    private void BeginResolution() => BeginResolution(preservePendingAcceptance: false);

    private void BeginResolution(bool preservePendingAcceptance)
    {
        if (_resolutionLifecycleCleanupDepth > 0 ||
            IsDisposed ||
            IsDisposing ||
            TerminalDisposalStartedInAncestry)
        {
            return;
        }

        if (!preservePendingAcceptance)
        {
            CancelPendingAcceptance();
        }

        ClearPendingFirstSuggestionSelection();
        var generation = ++_resolutionGeneration;
        _currentSnapshotGeneration = null;
        var lease = _resolutionOperation.Begin();
        var attachment = TryCaptureAttachment(out var capturedAttachment)
            ? capturedAttachment
            : null;
        var detachedAttachment = attachment is null &&
                                 TryCaptureDetachedAttachment(out var capturedDetachedAttachment)
            ? capturedDetachedAttachment
            : null;
        var searchTerms = Text;
        var resolver = Resolver;
        ExceptionDispatchInfo? startupFailure = null;

        if (!IsCurrentResolution(lease, generation))
        {
            return;
        }

        if (resolver is null || !MeetsMinimumPrefixLength(searchTerms, MinimumPrefixLength))
        {
            CaptureFailure(
                () => DispatchCompletionAsync(
                        lease,
                        generation,
                        attachment,
                        detachedAttachment,
                        () => ApplyResults(lease, generation, [], markCurrent: true),
                        allowDeferredDetachedPublication: true,
                        awaitDeferredDetachedPublication: false)
                    .GetAwaiter()
                    .GetResult(),
                ref startupFailure);
            startupFailure?.Throw();
            return;
        }

        CaptureFailure(() => SetIsResolving(true), ref startupFailure);

        if (!IsCurrentResolution(lease, generation))
        {
            startupFailure?.Throw();
            return;
        }

        ValueTask<IReadOnlyList<object?>> pending;

        try
        {
            pending = resolver(searchTerms, lease.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            CaptureFailure(
                () => DispatchCompletionAsync(
                        lease,
                        generation,
                        attachment,
                        detachedAttachment,
                        () => ApplyCancellation(lease, generation),
                        allowDeferredDetachedPublication: true,
                        awaitDeferredDetachedPublication: false)
                    .GetAwaiter()
                    .GetResult(),
                ref startupFailure);
            startupFailure?.Throw();
            return;
        }
        catch (Exception exception)
        {
            CaptureFailure(
                () => DispatchCompletionAsync(
                        lease,
                        generation,
                        attachment,
                        detachedAttachment,
                        () => ApplyFailure(lease, generation, searchTerms, exception),
                        allowDeferredDetachedPublication: true,
                        awaitDeferredDetachedPublication: false)
                    .GetAwaiter()
                    .GetResult(),
                ref startupFailure);
            startupFailure?.Throw();
            return;
        }

        if (pending.IsCompletedSuccessfully)
        {
            CaptureFailure(
                () => DispatchCompletionAsync(
                        lease,
                        generation,
                        attachment,
                        detachedAttachment,
                        () => ApplyCompletion(lease, generation, searchTerms, pending.Result),
                        allowDeferredDetachedPublication: true,
                        awaitDeferredDetachedPublication: false)
                    .GetAwaiter()
                    .GetResult(),
                ref startupFailure);
            startupFailure?.Throw();
            return;
        }

        var observation = CompleteResolutionAsync(
            pending,
            searchTerms,
            lease,
            generation,
            attachment,
            detachedAttachment);
        LastResolutionObservation = observation;
        ObserveResolution(observation);
        startupFailure?.Throw();
    }

    private async Task CompleteResolutionAsync(
        ValueTask<IReadOnlyList<object?>> pending,
        string searchTerms,
        LatestControlOperationLease lease,
        int generation,
        ControlAttachmentToken? attachment,
        ControlDetachedAttachmentToken? detachedAttachment)
    {
        IReadOnlyList<object?>? results = null;
        Exception? resolverFailure = null;
        var wasCancelled = false;

        try
        {
            results = await pending.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
        }
        catch (Exception exception)
        {
            resolverFailure = exception;
        }

        await DispatchCompletionAsync(
            lease,
            generation,
            attachment,
            detachedAttachment,
            () =>
            {
                if (wasCancelled)
                {
                    ApplyCancellation(lease, generation);
                }
                else if (resolverFailure is not null)
                {
                    ApplyFailure(lease, generation, searchTerms, resolverFailure);
                }
                else
                {
                    ApplyCompletion(lease, generation, searchTerms, results);
                }
            },
            allowDeferredDetachedPublication: true).ConfigureAwait(false);
    }

    private Task DispatchCompletionAsync(
        LatestControlOperationLease lease,
        int generation,
        ControlAttachmentToken? attachment,
        ControlDetachedAttachmentToken? detachedAttachment,
        Action action,
        bool allowDeferredDetachedPublication = false,
        bool awaitDeferredDetachedPublication = true)
    {
        if (attachment is not { } token)
        {
            LastInlineResolutionObservation = Task.CompletedTask;

            if (detachedAttachment is not { } detachedToken)
            {
                CompleteDetachedResolutionWhenStillDetached(lease, generation);
                return LastInlineResolutionObservation;
            }

            var published = TryPublishForCurrentDetachedAttachment(
                detachedToken,
                () =>
                {
                    BeforeDetachedResolutionPublication?.Invoke();
                    action();
                },
                () => IsCurrentResolution(lease, generation));

            if (!published)
            {
                if (allowDeferredDetachedPublication &&
                    Dispatcher is null &&
                    IsCurrentResolution(lease, generation))
                {
                    var retry = Task.Run(
                        () => RetryDetachedCompletion(
                            lease,
                            generation,
                            detachedToken,
                            action),
                        CancellationToken.None);

                    if (awaitDeferredDetachedPublication)
                    {
                        return retry;
                    }

                    LastResolutionObservation = retry;
                    ObserveResolution(retry);
                    return Task.CompletedTask;
                }

                CompleteDetachedResolutionWhenStillDetached(lease, generation);
            }

            return LastInlineResolutionObservation;
        }

        if (token.Dispatcher.CheckAccess())
        {
            LastInlineResolutionObservation = Task.CompletedTask;

            if (!IsCurrentAttachment(token) || !IsCurrentResolution(lease, generation))
            {
                CompleteResolution(lease, generation);
                return LastInlineResolutionObservation;
            }

            action();
            return LastInlineResolutionObservation;
        }

        var observation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Abandon() =>
            DeferAttachedResolutionAbandonment(token, lease, generation, observation);

        void ApplyOrDiscard()
        {
            try
            {
                action();
                _ = observation.TrySetResult();
            }
            catch (Exception exception)
            {
                _ = observation.TrySetException(exception);
                throw;
            }
        }

        PostBackgroundCompletionForCurrentAttachment(
            token,
            ApplyOrDiscard,
            () => IsCurrentResolution(lease, generation),
            onDiscarded: Abandon,
            onAbandoned: Abandon);
        return observation.Task;
    }

    private void DeferAttachedResolutionAbandonment(
        ControlAttachmentToken attachment,
        LatestControlOperationLease lease,
        int generation,
        TaskCompletionSource observation)
    {
        var dispatcher = attachment.Dispatcher;
        var settled = 0;
        EventHandler? idleHandler = null;
        CancellationTokenRegistration stoppingRegistration = default;

        void RetireWithoutPublication()
        {
            if (Interlocked.Exchange(ref settled, 1) != 0)
            {
                return;
            }

            dispatcher.Idle -= idleHandler;

            // Shutdown has revoked dispatcher publication. Retire the public pending state
            // directly so terminal cleanup cannot leave an operation permanently observable.
            if (IsCurrentResolution(lease, generation))
            {
                IsResolving = false;
            }

            CompleteResolution(lease, generation);
            _ = observation.TrySetResult();
        }

        idleHandler = (_, _) =>
        {
            if (Interlocked.Exchange(ref settled, 1) != 0)
            {
                return;
            }

            dispatcher.Idle -= idleHandler;
            stoppingRegistration.Dispose();

            try
            {
                if (IsCurrentAttachment(attachment) && IsCurrentResolution(lease, generation))
                {
                    SetIsResolving(false);
                }
            }
            finally
            {
                CompleteResolution(lease, generation);
                _ = observation.TrySetResult();
            }
        };

        dispatcher.Idle += idleHandler;
        stoppingRegistration = dispatcher.StoppingToken.Register(RetireWithoutPublication);

        if (Volatile.Read(ref settled) != 0)
        {
            stoppingRegistration.Dispose();
            return;
        }

        try
        {
            // A full queue guarantees another drain-to-idle transition. When the queue drained
            // between rejection and subscription, this marker creates that transition without
            // retaining or repeatedly retrying rejected work.
            dispatcher.Post(static () => { }, RetireWithoutPublication);
        }
        catch (ObjectDisposedException)
        {
            RetireWithoutPublication();
        }
        catch (InvalidOperationException)
        {
            // Existing queued work owns the next idle transition.
        }
    }

    private void RetryDetachedCompletion(
        LatestControlOperationLease lease,
        int generation,
        ControlDetachedAttachmentToken detachedToken,
        Action action)
    {
        var published = TryPublishForCurrentDetachedAttachment(
            detachedToken,
            () =>
            {
                BeforeDetachedResolutionPublication?.Invoke();
                action();
            },
            () => IsCurrentResolution(lease, generation));

        if (!published)
        {
            CompleteDetachedResolutionWhenStillDetached(lease, generation);
        }
    }

    private void CompleteDetachedResolutionWhenStillDetached(
        LatestControlOperationLease lease,
        int generation)
    {
        if (Dispatcher is null)
        {
            CompleteResolution(lease, generation);
        }
    }

    private void ApplyCompletion(
        LatestControlOperationLease lease,
        int generation,
        string searchTerms,
        IReadOnlyList<object?>? results)
    {
        if (results is null)
        {
            ApplyFailure(
                lease,
                generation,
                searchTerms,
                new InvalidOperationException("A suggestion resolver returned a null result snapshot."));
            return;
        }

        ApplyResults(lease, generation, results, markCurrent: true);
    }

    private void ApplyResults(
        LatestControlOperationLease lease,
        int generation,
        IReadOnlyList<object?> results,
        bool markCurrent)
    {
        ExceptionDispatchInfo? failure = null;

        try
        {
            if (CommitResultState(lease, generation, results, markCurrent, out var changed, ref failure))
            {
                if (changed)
                {
                    CaptureFailure(
                        () => SuggestionsChanged?.Invoke(this, EventArgs.Empty),
                        ref failure);
                }

                if (IsCurrentResolution(lease, generation))
                {
                    var shouldOpen = _wantsOpen && Suggestions.Count > 0;
                    CaptureFailure(
                        () =>
                        {
                            if (!shouldOpen ||
                                (EffectiveIsEnabled && EffectiveIsVisible) ||
                                IsOpen)
                            {
                                base.IsPopupOpen = shouldOpen;
                            }
                        },
                        ref failure);
                }

                if (IsCurrentResolution(lease, generation) &&
                    IsOpen &&
                    _list.SelectedIndex < 0)
                {
                    SelectFirstAvailableSuggestion(commitCurrent: false);
                }

                if (IsCurrentResolution(lease, generation) &&
                    IsOpen &&
                    _list.ActiveIndex != _list.SelectedIndex)
                {
                    CaptureFailure(
                        () => RequestFirstSuggestionSelection(generation),
                        ref failure);
                }
            }
        }
        finally
        {
            CompleteResolution(lease, generation);
        }

        failure?.Throw();
    }

    private void ApplyFailure(
        LatestControlOperationLease lease,
        int generation,
        string searchTerms,
        Exception exception)
    {
        ExceptionDispatchInfo? failure = null;

        try
        {
            if (CommitResultState(lease, generation, [], markCurrent: false, out var changed, ref failure))
            {
                CaptureFailure(() => base.IsPopupOpen = false, ref failure);

                if (changed && IsCurrentResolution(lease, generation))
                {
                    CaptureFailure(
                        () => SuggestionsChanged?.Invoke(this, EventArgs.Empty),
                        ref failure);
                }

                if (IsCurrentResolution(lease, generation))
                {
                    CaptureFailure(
                        () => ResolutionFailed?.Invoke(
                            this,
                            new SuggestionResolutionFailedEventArgs(searchTerms, exception)),
                        ref failure);
                }
            }
        }
        finally
        {
            CompleteResolution(lease, generation);
        }

        failure?.Throw();
    }

    private void ApplyCancellation(LatestControlOperationLease lease, int generation)
    {
        ExceptionDispatchInfo? failure = null;

        try
        {
            if (CommitResultState(lease, generation, [], markCurrent: false, out var changed, ref failure))
            {
                CaptureFailure(() => base.IsPopupOpen = false, ref failure);

                if (changed && IsCurrentResolution(lease, generation))
                {
                    CaptureFailure(
                        () => SuggestionsChanged?.Invoke(this, EventArgs.Empty),
                        ref failure);
                }
            }
        }
        finally
        {
            CompleteResolution(lease, generation);
        }

        failure?.Throw();
    }

    private bool CommitResultState(
        LatestControlOperationLease lease,
        int generation,
        IReadOnlyList<object?> results,
        bool markCurrent,
        out bool changed,
        ref ExceptionDispatchInfo? failure)
    {
        changed = false;

        if (!IsCurrentResolution(lease, generation))
        {
            return false;
        }

        CaptureFailure(() => SetIsResolving(false), ref failure);

        if (!IsCurrentResolution(lease, generation))
        {
            return false;
        }

        changed = !SnapshotsEqual(Suggestions, results);
        ClearPendingFirstSuggestionSelection();
        CaptureFailure(() => _list.Items = results, ref failure);
        CaptureFailure(() => _list.SelectedIndex = -1, ref failure);
        CaptureFailure(() => _list.SetProvisionalCurrentIndex(-1), ref failure);

        if (!IsCurrentResolution(lease, generation) || !SnapshotsEqual(Suggestions, results))
        {
            return false;
        }

        _currentSnapshotGeneration = markCurrent ? generation : null;

        if (changed)
        {
            CaptureFailure(
                () => NotifyPropertyChanged(nameof(Suggestions), InvalidationImpact.None),
                ref failure);
        }

        return IsCurrentResolution(lease, generation);
    }

    private void CompleteResolution(LatestControlOperationLease lease, int generation)
    {
        if (IsCurrentResolution(lease, generation))
        {
            _ = _resolutionOperation.TryComplete(lease);
        }
    }

    private static void ObserveResolution(Task observation)
    {
        if (observation.IsCompleted)
        {
            _ = observation.Exception;
            return;
        }

        _ = observation.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    [Pure]
    private bool IsCurrentResolution(LatestControlOperationLease lease, int generation) =>
        !IsDisposed &&
        !IsDisposing &&
        !TerminalDisposalStartedInAncestry &&
        _resolutionLifecycleCleanupDepth == 0 &&
        generation == _resolutionGeneration &&
        _resolutionOperation.IsCurrent(lease);

    private void SetIsResolving(bool value)
    {
        if (IsResolving == value)
        {
            return;
        }

        IsResolving = value;
        NotifyPropertyChanged(nameof(IsResolving), InvalidationImpact.None);
    }

    [Pure]
    private static bool MeetsMinimumPrefixLength(string value, int minimum)
    {
        if (minimum == 0)
        {
            return true;
        }

        var count = 0;

        foreach (var unused in Graphemes.Enumerate(value))
        {
            _ = unused;
            count++;

            if (count >= minimum)
            {
                return true;
            }
        }

        return false;
    }

    [Pure]
    private static bool SnapshotsEqual(
        IReadOnlyList<object?> first,
        IReadOnlyList<object?> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var index = 0; index < first.Count; index++)
        {
            if (!EqualityComparer<object?>.Default.Equals(first[index], second[index]))
            {
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Layout and lifetime

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);

        if (!eventArgs.IsHandled &&
            IsOpen &&
            eventArgs is PointerEventArgs { Pointer.Action: PointerAction.Wheel } &&
            OriginatesInSuggestionList(eventArgs.OriginalSource))
        {
            // A wheel that reached the owner has already been offered to ListView and its rails.
            // Consuming the endpoint keeps in-plane scrolling from becoming light dismissal.
            eventArgs.IsHandled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        ExceptionDispatchInfo? failure = null;

        if (_resolutionOperation.HasCurrent)
        {
            var cancellationGeneration = ++_resolutionGeneration;
            _currentSnapshotGeneration = null;
            CaptureFailure(_resolutionOperation.Cancel, ref failure);

            if (_resolutionGeneration == cancellationGeneration)
            {
                CaptureFailure(() => SetIsResolving(false), ref failure);
            }
        }

        CaptureFailure(SchedulePendingFirstSuggestionSelection, ref failure);
        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        ExceptionDispatchInfo? failure = null;
        var endsResolutionLifetime = reason is ReleaseReason.Detached or ReleaseReason.Disposed;
        ClearPendingFirstSuggestionSelection();
        CancelPendingAcceptance();

        if (endsResolutionLifetime)
        {
            _resolutionLifecycleCleanupDepth++;
        }

        try
        {
            if (endsResolutionLifetime)
            {
                var cancellationGeneration = ++_resolutionGeneration;
                _currentSnapshotGeneration = null;
                CaptureFailure(_resolutionOperation.Cancel, ref failure);

                if (_resolutionGeneration == cancellationGeneration)
                {
                    CaptureFailure(() => SetIsResolving(false), ref failure);
                }
            }

            CaptureFailure(() => base.OnUnavailable(reason), ref failure);

            if (reason == ReleaseReason.Disposed)
            {
                _input.TextChanged -= OnTextChanged;
                _list.ItemActivationStarting -= OnItemActivationStarting;
                _list.ItemInvoked -= OnItemInvoked;
                BeforeDetachedResolutionPublication = null;
                SuggestionsChanged = null;
                ResolutionFailed = null;

                if (SuggestionAccepted is not null)
                {
                    SuggestionAccepted = null;
                }
            }
        }
        finally
        {
            if (endsResolutionLifetime)
            {
                _resolutionLifecycleCleanupDepth--;
            }
        }

        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override void OnDropDownOpened()
    {
        SelectFirstAvailableSuggestion(commitCurrent: false);

        if (_list.ActiveIndex != _list.SelectedIndex &&
            _currentSnapshotGeneration is { } generation)
        {
            RequestFirstSuggestionSelection(generation);
        }

        base.OnDropDownOpened();
    }

    /// <inheritdoc/>
    protected override void OnDropDownClosed()
    {
        _wantsOpen = false;
        ClearPendingFirstSuggestionSelection();
        base.OnDropDownClosed();
    }

    [Pure]
    private bool OriginatesInSuggestionList(ControlBase? source)
    {
        for (var current = source; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, _list))
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}
