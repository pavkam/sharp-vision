// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using Collections;

using Popups;

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
    private readonly TextInput _input;
    private readonly ListView _list;
    private readonly Popup _popup;
    private readonly PopupDropDownCoordinator _popupCoordinator;
    private readonly LatestControlOperation _resolutionOperation = new();
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;
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
    private ulong _minimumPrefixLengthVersion;
    private ulong _resolverVersion;
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
        _popup = new Popup
        {
            Anchor = _input,
            ConnectsToAnchor = true,
            Content = _list,
            FocusOnOpen = false,
            ModalBehavior = PopupModalBehavior.None,
            Placement = PopupPlacement.Below,
            SuppressCloseOtherPopups = true,
            TabNavigation = TabNavigation.None,
            TracksAnchorReflow = false,
            ContentHeightLimit = Length.Cells(8)
        };
        var popupSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Popup,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "suggestions",
                InvalidationImpact.Measure),
            capacity: 1);
        popupSlot.Add(_popup);
        _scrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ForwardingDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, _list, nameof(ScrollBarStyle));
        _popupCoordinator = new PopupDropDownCoordinator(
            this,
            _popup,
            _list,
            _input.Focus,
            () => NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.None),
            OnOpened,
            OnClosed,
            ownerInitialFocus: _input,
            beginSession: BeginNavigationSession,
            handleNavigationKey: HandleNavigationKey,
            cancelSession: CancelNavigationSession,
            acceptSession: AcceptNavigationSession);
        InitializeContent(_input);
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
        get => _input.Placeholder;
        set
        {
            VerifyMutable();

            if (string.Equals(_input.Placeholder, value, StringComparison.Ordinal))
            {
                return;
            }

            _input.Placeholder = value;
            NotifyPropertyChanged(nameof(Placeholder), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets the optional leading editor affix.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? StartAffix
    {
        get => _input.StartAffix;
        set
        {
            VerifyMutable();

            if (_input.StartAffix == value)
            {
                return;
            }

            _input.StartAffix = value;
            NotifyPropertyChanged(nameof(StartAffix), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets the optional trailing editor affix.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? EndAffix
    {
        get => _input.EndAffix;
        set
        {
            VerifyMutable();

            if (_input.EndAffix == value)
            {
                return;
            }

            _input.EndAffix = value;
            NotifyPropertyChanged(nameof(EndAffix), InvalidationImpact.None);
        }
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
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            var version = ++_minimumPrefixLengthVersion;
            var resolutionGeneration = _resolutionGeneration;
            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(
                () => NotifyPropertyChanged(nameof(MinimumPrefixLength), InvalidationImpact.None),
                ref failure);

            if (!IsDisposed &&
                _minimumPrefixLengthVersion == version &&
                field == value &&
                _resolutionGeneration == resolutionGeneration)
            {
                ExceptionAggregation.Capture(BeginResolution, ref failure);
            }

            failure?.Throw();
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
            VerifyMutable();

            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            var version = ++_resolverVersion;
            var resolutionGeneration = _resolutionGeneration;
            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(
                () => NotifyPropertyChanged(nameof(Resolver), InvalidationImpact.None),
                ref failure);

            if (!IsDisposed &&
                _resolverVersion == version &&
                ReferenceEquals(field, value) &&
                _resolutionGeneration == resolutionGeneration)
            {
                ExceptionAggregation.Capture(BeginResolution, ref failure);
            }

            failure?.Throw();
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
        get => _list.ItemTemplate;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();

            if (ReferenceEquals(_list.ItemTemplate, value))
            {
                return;
            }

            _list.ItemTemplate = value;
            NotifyPropertyChanged(nameof(ItemTemplate), InvalidationImpact.None);
        }
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

    /// <summary>Gets or sets the intrinsic, fixed, or placement-side-relative maximum visible suggestion height.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A fixed or percentage value is zero.</exception>
    /// <exception cref="ArgumentException">The value uses proportional sizing.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Length DropDownHeight
    {
        get => _popup.ContentHeightLimit;
        set
        {
            VerifyMutable();

            if (_popup.ContentHeightLimit == value)
            {
                return;
            }

            _popup.ContentHeightLimit = value;
            NotifyPropertyChanged(nameof(DropDownHeight), InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets the automatic, fixed, or viewport-relative uniform suggestion-row height.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A fixed or percentage value is zero.</exception>
    /// <exception cref="ArgumentException">The value uses proportional sizing.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Length RowHeight
    {
        get => _list.RowHeight;
        set
        {
            VerifyMutable();

            if (_list.RowHeight == value)
            {
                return;
            }

            _list.RowHeight = value;
            NotifyPropertyChanged(nameof(RowHeight), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets the axes available to the suggestion-list overflow host.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBars ScrollBars
    {
        get => _list.ScrollBars;
        set
        {
            VerifyMutable();

            if (_list.ScrollBars == value)
            {
                return;
            }

            _list.ScrollBars = value;
            NotifyPropertyChanged(nameof(ScrollBars), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets the suggestion-list scrollbar reservation policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get => _list.ShowScrollBars;
        set
        {
            VerifyMutable();

            if (_list.ShowScrollBars == value)
            {
                return;
            }

            _list.ShowScrollBars = value;
            NotifyPropertyChanged(nameof(ShowScrollBars), InvalidationImpact.None);
        }
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

    /// <summary>Gets or sets the suggestion popup's border and shadow together.</summary>
    /// <remarks>A null component keeps that facet under the Popup's own appearance ownership.</remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public PopupChrome PopupChrome
    {
        get => _popup.Style;
        set
        {
            VerifyMutable();

            if (_popup.Style == value)
            {
                return;
            }

            _popup.Style = value;
            NotifyPropertyChanged(nameof(PopupChrome), InvalidationImpact.None);
        }
    }

    /// <summary>Returns both suggestion-popup chrome facets to Popup appearance ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void ResetPopupChrome() => PopupChrome = default;

    /// <summary>Gets or sets whether a non-empty current suggestion snapshot is open.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool IsOpen
    {
        get => _popupCoordinator.IsOpen;
        set
        {
            VerifyMutable();

            if (!value)
            {
                _wantsOpen = false;
                CancelPendingAcceptance();
                _popupCoordinator.SetOpen(false);
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

            _popupCoordinator.SetOpen(true);
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
            _popupCoordinator.SetOpen(true);
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

    private bool HandleNavigationKey(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;

        if (eventArgs.IsInitialKeyDown &&
            stroke.Code == Code.Escape &&
            stroke.Modifiers.IsActivationEligible())
        {
            // Mark Escape before closing because ending the session makes the coordinator's
            // post-callback guard intentionally refuse to mutate the old routed record.
            eventArgs.IsHandled = true;
            _popupCoordinator.SetOpen(false);
            return true;
        }

        if (eventArgs.IsInitialKeyDown &&
            stroke.Code == Code.Tab &&
            KeyboardModifierPolicy.IsTabTraversalEligible(stroke.Modifiers))
        {
            _popupCoordinator.SetOpen(false);
            return false;
        }

        if (eventArgs.IsInitialKeyDown &&
            stroke.Code == Code.Enter &&
            stroke.Modifiers.IsActivationEligible())
        {
            // An open suggestion session owns Enter even while the newest request is unresolved.
            // This prevents the editor's ordinary Submitted event from accepting an older row.
            eventArgs.IsHandled = true;

            if (!CanAcceptCurrentSnapshot())
            {
                return true;
            }

            _ = _list.ActivateCurrent(ActivationCause.Keyboard, Code.Enter, stroke.Modifiers);
            return true;
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
            _popupCoordinator.TransitionVersion,
            _popupCoordinator.SessionGeneration);
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
        ExceptionAggregation.Capture(_popupCoordinator.AcceptAndClose, ref failure);

        if (IsCurrentAcceptance(prepared))
        {
            var cause = _acceptanceCause;
            ClearAcceptance(prepared.Generation);
            ExceptionAggregation.Capture(
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
        IsCurrent(attachment) &&
        eventArgs.ActivationGeneration == identity.ItemGeneration &&
        eventArgs.Index == identity.ItemIndex &&
        eventArgs.Index == _list.SelectedIndex &&
        eventArgs.Index == _list.ActiveIndex &&
        IsCurrentSuggestionItem(eventArgs.Index, eventArgs.Item) &&
        _popupCoordinator.TransitionVersion == identity.PopupTransitionVersion &&
        _popupCoordinator.SessionGeneration == identity.PopupSessionGeneration;

    [Pure]
    private bool IsCurrentActivationStarting(
        ItemInvokedEventArgs eventArgs,
        PopupItemActivationIdentity identity,
        int resolutionGeneration,
        ControlAttachmentToken attachment) =>
        CanAcceptCurrentSnapshot() &&
        _resolutionGeneration == resolutionGeneration &&
        _currentSnapshotGeneration == resolutionGeneration &&
        IsCurrent(attachment) &&
        eventArgs.ActivationGeneration == identity.ItemGeneration &&
        eventArgs.Index == identity.ItemIndex &&
        IsCurrentSuggestionItem(eventArgs.Index, eventArgs.Item) &&
        _popupCoordinator.TransitionVersion == identity.PopupTransitionVersion &&
        _popupCoordinator.SessionGeneration == identity.PopupSessionGeneration;

    [Pure]
    private bool CanAcceptCurrentSnapshot() =>
        !IsDisposed &&
        !IsDisposing &&
        Dispatcher is not null &&
        EffectiveIsEnabled &&
        EffectiveIsVisible &&
        _popupCoordinator.IsOpen &&
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
        IsCurrent(attachment) &&
        _popupCoordinator.IsOpen &&
        _popupCoordinator.TransitionVersion == transaction.Activation.PopupTransitionVersion &&
        _popupCoordinator.SessionGeneration == transaction.Activation.PopupSessionGeneration;

    [Pure]
    private bool IsCurrentAcceptance(SuggestionInputAcceptanceTransaction transaction) =>
        !IsDisposed &&
        !IsDisposing &&
        _acceptanceTransaction is { } current &&
        current.Generation == transaction.Generation &&
        _acceptanceGeneration == transaction.Generation &&
        _acceptanceAttachment is { } attachment &&
        IsCurrent(attachment) &&
        string.Equals(Text, transaction.AcceptedText, StringComparison.Ordinal) &&
        !_popupCoordinator.IsOpen;

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
        _pendingFirstSelectionSessionGeneration = _popupCoordinator.SessionGeneration;
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
            _popupCoordinator.IsOpen &&
            _popupCoordinator.SessionGeneration == sessionGeneration &&
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
        ExceptionAggregation.Capture(
            () => NotifyPropertyChanged(nameof(Text), InvalidationImpact.None),
            ref failure);

        if (!IsDisposed &&
            _textCommitVersion == version &&
            string.Equals(Text, committedText, StringComparison.Ordinal) &&
            _resolutionGeneration == resolutionGeneration)
        {
            ExceptionAggregation.Capture(
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
            ExceptionAggregation.Capture(
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

        ExceptionAggregation.Capture(() => SetIsResolving(true), ref startupFailure);

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
            ExceptionAggregation.Capture(
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
            ExceptionAggregation.Capture(
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
            ExceptionAggregation.Capture(
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

            if (!IsCurrent(token) || !IsCurrentResolution(lease, generation))
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
            if (!IsCurrent(token) || !IsCurrentResolution(lease, generation))
            {
                Abandon();
                return;
            }

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

        token.Dispatcher.PostBackgroundCompletion(ApplyOrDiscard, Abandon);
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
                if (IsCurrent(attachment) && IsCurrentResolution(lease, generation))
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
                    ExceptionAggregation.Capture(
                        () => SuggestionsChanged?.Invoke(this, EventArgs.Empty),
                        ref failure);
                }

                if (IsCurrentResolution(lease, generation))
                {
                    ExceptionAggregation.Capture(
                        () => _popupCoordinator.SetOpen(
                            _wantsOpen &&
                            EffectiveIsEnabled &&
                            EffectiveIsVisible &&
                            Suggestions.Count > 0),
                        ref failure);
                }

                if (IsCurrentResolution(lease, generation) &&
                    _popupCoordinator.IsOpen &&
                    _list.SelectedIndex < 0)
                {
                    SelectFirstAvailableSuggestion(commitCurrent: false);
                }

                if (IsCurrentResolution(lease, generation) &&
                    _popupCoordinator.IsOpen &&
                    _list.ActiveIndex != _list.SelectedIndex)
                {
                    ExceptionAggregation.Capture(
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
                ExceptionAggregation.Capture(() => _popupCoordinator.SetOpen(false), ref failure);

                if (changed && IsCurrentResolution(lease, generation))
                {
                    ExceptionAggregation.Capture(
                        () => SuggestionsChanged?.Invoke(this, EventArgs.Empty),
                        ref failure);
                }

                if (IsCurrentResolution(lease, generation))
                {
                    ExceptionAggregation.Capture(
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
                ExceptionAggregation.Capture(() => _popupCoordinator.SetOpen(false), ref failure);

                if (changed && IsCurrentResolution(lease, generation))
                {
                    ExceptionAggregation.Capture(
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

        ExceptionAggregation.Capture(() => SetIsResolving(false), ref failure);

        if (!IsCurrentResolution(lease, generation))
        {
            return false;
        }

        changed = !SnapshotsEqual(Suggestions, results);
        ClearPendingFirstSuggestionSelection();
        ExceptionAggregation.Capture(() => _list.Items = results, ref failure);
        ExceptionAggregation.Capture(() => _list.SelectedIndex = -1, ref failure);
        ExceptionAggregation.Capture(() => _list.SetProvisionalCurrentIndex(-1), ref failure);

        if (!IsCurrentResolution(lease, generation) || !SnapshotsEqual(Suggestions, results))
        {
            return false;
        }

        _currentSnapshotGeneration = markCurrent ? generation : null;

        if (changed)
        {
            ExceptionAggregation.Capture(
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
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = MeasureChild(_popup, new Constraint(constraint.Width, height: null));
        return base.MeasureOverride(constraint);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        base.ArrangeOverride(bounds);
        ArrangeChild(_popup, RootBounds(bounds), ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);

        if (!eventArgs.IsHandled &&
            _popupCoordinator.IsOpen &&
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
            ExceptionAggregation.Capture(_resolutionOperation.Cancel, ref failure);

            if (_resolutionGeneration == cancellationGeneration)
            {
                ExceptionAggregation.Capture(() => SetIsResolving(false), ref failure);
            }
        }

        ExceptionAggregation.Capture(_popupCoordinator.OnOwnerAttached, ref failure);
        ExceptionAggregation.Capture(SchedulePendingFirstSuggestionSelection, ref failure);
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
                ExceptionAggregation.Capture(_resolutionOperation.Cancel, ref failure);

                if (_resolutionGeneration == cancellationGeneration)
                {
                    ExceptionAggregation.Capture(() => SetIsResolving(false), ref failure);
                }
            }

            ExceptionAggregation.Capture(() => base.OnUnavailable(reason), ref failure);
            ExceptionAggregation.Capture(() => _popupCoordinator.OnOwnerUnavailable(reason), ref failure);

            if (reason == ReleaseReason.Disposed)
            {
                _input.TextChanged -= OnTextChanged;
                _list.ItemActivationStarting -= OnItemActivationStarting;
                _list.ItemInvoked -= OnItemInvoked;
                ExceptionAggregation.Capture(_popupCoordinator.Detach, ref failure);
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

    private void OnOpened()
    {
        SelectFirstAvailableSuggestion(commitCurrent: false);

        if (_list.ActiveIndex != _list.SelectedIndex &&
            _currentSnapshotGeneration is { } generation)
        {
            RequestFirstSuggestionSelection(generation);
        }
    }

    private void OnClosed()
    {
        _wantsOpen = false;
        ClearPendingFirstSuggestionSelection();
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
