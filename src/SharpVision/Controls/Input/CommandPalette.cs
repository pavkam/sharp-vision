// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using Collections;

using Popups;

using SharpVision.Terminal.Input;

/// <summary>Provides an editable command search field with asynchronously resolved popup results.</summary>
/// <remarks>
/// The retained editor remains the focus target in embedded, centered, and top-centered layouts.
/// Each text change cancels the prior request, and only the latest completion may publish items.
/// </remarks>
[PublicAPI]
public sealed class CommandPalette: CompositeControlBase
{
    private const int _defaultDropDownHeight = 8;
    private readonly TextInput _input;
    private readonly ListView _list;
    private readonly Popup _popup;
    private readonly PopupDropDownCoordinator _popupCoordinator;
    private readonly LatestControlOperation _resolutionOperation = new();
    private int _resolutionGeneration;
    private int _openingSelectedIndex = -1;
    private int _openingCurrentIndex = -1;
    private int? _pendingFirstSelectionResolutionGeneration;
    private ulong _pendingFirstSelectionSessionGeneration;
    private Dispatcher? _pendingFirstSelectionDispatcher;
    private PopupItemActivationIdentity? _itemActivation;
    private bool _wantsOpen;

    #region Construction and events

    /// <summary>Initializes an empty command palette with a bordered editor and connected popup.</summary>
    public CommandPalette()
    {
        _input = new TextInput { HorizontalAlignment = HorizontalAlignment.Stretch };
        _input.TextChanged += OnTextChanged;
        _list = new ListView
        {
            IsTabStop = false,
            MaxHeight = _defaultDropDownHeight,
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
            TracksAnchorReflow = false
        };
        var popupSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Popup,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "results",
                InvalidationImpact.Measure),
            capacity: 1);
        popupSlot.Add(_popup);
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
            cancelSession: CancelNavigationSession);
        InitializeContent(_input);
    }

    /// <summary>Raised after a non-empty result popup opens.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised after the result popup closes.</summary>
    public event EventHandler? Closed;

    /// <summary>Raised after the current result snapshot changes.</summary>
    public event EventHandler? ResultsChanged;

    /// <summary>Raised when the still-current resolver request fails.</summary>
    public event EventHandler<CommandPaletteResolutionFailedEventArgs>? ResolutionFailed;

    /// <summary>Raised after keyboard or pointer activation of one resolved item.</summary>
    public event EventHandler<ItemInvokedEventArgs>? ItemInvoked;

    #endregion

    #region Search and results

    /// <summary>Gets or sets the optional resolver invoked for each current search snapshot.</summary>
    /// <remarks>Assigning a resolver immediately resolves the current text.</remarks>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public CommandPaletteResolver? Resolver
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
            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(
                () => NotifyPropertyChanged(nameof(Resolver), InvalidationImpact.None),
                ref failure);

            if (!IsDisposed && ReferenceEquals(field, value))
            {
                ExceptionAggregation.Capture(BeginResolution, ref failure);
            }

            failure?.Throw();
        }
    }

    /// <summary>Gets or sets the freely editable non-null search text.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value violates the retained editor policy.</exception>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public string Text
    {
        get => _input.Text;
        set => _input.Text = value;
    }

    /// <summary>Gets the copied current resolver result snapshot.</summary>
    public IReadOnlyList<object?> Items => _list.Items;

    /// <summary>Gets whether the current resolver request has not completed.</summary>
    public bool IsResolving { get; private set; }

    /// <summary>Gets whether detached or pre-arrange result selection remains queued. Tests use
    /// this seam to prove every popup close path releases deferred session work.</summary>
    internal bool HasPendingFirstResultSelection =>
        _pendingFirstSelectionResolutionGeneration is not null;

    /// <summary>Gets or sets the detached-control factory used to realize each result row.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">Candidate output is invalid or duplicated.</exception>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public ItemTemplate ItemTemplate
    {
        get => _list.ItemTemplate;
        set
        {
            VerifyMutable();
            _list.ItemTemplate = value;
            NotifyPropertyChanged(nameof(ItemTemplate), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets the fixed row height, or null for content-sized rows.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public int? RowHeight
    {
        get => _list.RowHeight;
        set
        {
            VerifyMutable();
            _list.RowHeight = value;
            NotifyPropertyChanged(nameof(RowHeight), InvalidationImpact.None);
        }
    }

    /// <summary>Starts a fresh resolution for the current text and makes results eligible to open.</summary>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public void Refresh()
    {
        VerifyMutable();
        _wantsOpen = true;
        BeginResolution();
    }

    #endregion

    #region Presentation

    /// <summary>Gets or sets optional placeholder text shown while the editor is empty.</summary>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
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
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
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
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
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

    /// <summary>Gets or sets the complete local editor border.</summary>
    /// <remarks>Assigning either <see cref="FieldBorder"/> or <see cref="FieldShadow"/> snapshots
    /// the editor's whole resolved presentation into a local <see cref="TextInputStyle"/>, so a
    /// theme-authored facet neither property names (its affix gap, for instance) is pinned to the
    /// value it resolved to at assignment time and stops tracking a later theme swap until both
    /// <see cref="ResetFieldBorder"/> and <see cref="ResetFieldShadow"/> have been called.</remarks>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public Border FieldBorder
    {
        get => _input.ActualStyle.Border;
        set
        {
            VerifyMutable();

            if (_input.ActualStyle.Border == value)
            {
                return;
            }

            _input.Style = _input.ActualStyle with { Border = value };
            NotifyPropertyChanged(nameof(FieldBorder), InvalidationImpact.None);
        }
    }

    /// <summary>Returns the editor border to the active input appearance.</summary>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public void ResetFieldBorder()
    {
        VerifyMutable();

        if (_input.Style is not { } local)
        {
            return;
        }

        // The border alone returns to the theme-owned appearance; any other locally assigned
        // facet (a shadow set through FieldShadow, for instance) survives untouched. Only when
        // that leaves nothing but the theme-owned appearance does the local style collapse back
        // to null, so a later theme swap keeps tracking the border live instead of staying pinned
        // to today's resolved value.
        var fallback = TextInputStyle.Definition.Resolve(null, _input.Theme);
        var updated = local with { Border = fallback.Border };
        _input.Style = updated == fallback ? null : updated;
        NotifyPropertyChanged(nameof(FieldBorder), InvalidationImpact.None);
    }

    /// <summary>Gets or sets the complete local editor shadow.</summary>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public Shadow FieldShadow
    {
        get => _input.ActualStyle.Shadow;
        set
        {
            VerifyMutable();

            if (_input.ActualStyle.Shadow == value)
            {
                return;
            }

            _input.Style = _input.ActualStyle with { Shadow = value };
            NotifyPropertyChanged(nameof(FieldShadow), InvalidationImpact.None);
        }
    }

    /// <summary>Returns the editor shadow to the active input appearance.</summary>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public void ResetFieldShadow()
    {
        VerifyMutable();

        if (_input.Style is not { } local)
        {
            return;
        }

        // Mirrors ResetFieldBorder: only the shadow facet returns to the theme-owned appearance,
        // collapsing the local style back to null only once nothing local remains.
        var fallback = TextInputStyle.Definition.Resolve(null, _input.Theme);
        var updated = local with { Shadow = fallback.Shadow };
        _input.Style = updated == fallback ? null : updated;
        NotifyPropertyChanged(nameof(FieldShadow), InvalidationImpact.None);
    }

    /// <summary>Gets or sets the result popup's border and shadow together.</summary>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
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

    /// <summary>Returns the result popup border and shadow to its appearance role.</summary>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public void ResetPopupChrome() => PopupChrome = default;

    /// <summary>Gets or sets the positive maximum visible result height in cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public int DropDownHeight
    {
        get => _list.MaxHeight;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            VerifyMutable();

            if (_list.MaxHeight == value)
            {
                return;
            }

            _list.MaxHeight = value;
            NotifyPropertyChanged(nameof(DropDownHeight), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets whether the non-empty result popup is open.</summary>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public bool IsOpen
    {
        get => _popupCoordinator.IsOpen;
        set
        {
            VerifyMutable();

            if (value && (!EffectiveIsEnabled || !EffectiveIsVisible))
            {
                _wantsOpen = false;
                return;
            }

            _wantsOpen = value;

            if (!value)
            {
                _popupCoordinator.SetOpen(false);
                return;
            }

            if (Items.Count > 0)
            {
                _popupCoordinator.SetOpen(true);
            }
            else
            {
                BeginResolution();
            }
        }
    }

    /// <summary>Focuses the retained editor and opens current or freshly resolved results.</summary>
    /// <returns>True when the mounted editor accepted focus.</returns>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public bool Open()
    {
        IsOpen = true;
        return _input.Focus();
    }

    /// <summary>Closes results while preserving the current search text.</summary>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public void Close() => IsOpen = false;

    #endregion

    #region Layout, input, and lifecycle

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = MeasureChild(_popup, new Constraint(constraint.Width, DropDownHeight.Add(1)));
        return base.MeasureOverride(constraint);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        base.ArrangeOverride(bounds);
        ArrangeChild(_popup, RootBounds(bounds), ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        _popupCoordinator.OnOwnerAttached();
        SchedulePendingFirstResultSelection();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        ClearPendingFirstResultSelection();
        _popupCoordinator.OnOwnerUnavailable(reason);
        ExceptionDispatchInfo? failure = null;

        if (reason is ReleaseReason.Detached or ReleaseReason.Disposed)
        {
            _resolutionGeneration++;
            ExceptionAggregation.Capture(_resolutionOperation.Cancel, ref failure);
            ExceptionAggregation.Capture(() => SetIsResolving(false), ref failure);
        }

        if (reason == ReleaseReason.Disposed)
        {
            _input.TextChanged -= OnTextChanged;
            _list.ItemActivationStarting -= OnItemActivationStarting;
            _list.ItemInvoked -= OnItemInvoked;
            _popupCoordinator.Detach();
            Opened = null;
            Closed = null;
            ResultsChanged = null;
            ResolutionFailed = null;
            ItemInvoked = null;
        }

        failure?.Throw();
    }

    private void BeginNavigationSession()
    {
        _openingSelectedIndex = _list.SelectedIndex;
        _openingCurrentIndex = _list.ActiveIndex;
        _itemActivation = null;
    }

    private bool HandleNavigationKey(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;

        if (eventArgs.IsInitialKeyDown &&
            stroke.Code == Code.Escape &&
            stroke.Modifiers.IsActivationEligible())
        {
            eventArgs.IsHandled = true;
            _popupCoordinator.SetOpen(false);
            return true;
        }

        if (eventArgs.IsInitialKeyDown && stroke.Code == Code.Enter)
        {
            if (IsResolving)
            {
                eventArgs.IsHandled = true;
                return true;
            }

            var handled = _list.ActivateCurrent(
                ActivationCause.Keyboard,
                stroke.Code,
                stroke.Modifiers);
            eventArgs.IsHandled |= handled;
            return handled;
        }

        var navigated = _list.HandleSelectionNavigationKey(eventArgs);
        eventArgs.IsHandled |= navigated;
        return navigated;
    }

    private void CancelNavigationSession()
    {
        _itemActivation = null;
        var selectedIndex = IsCurrentResultIndex(_openingSelectedIndex) ? _openingSelectedIndex : -1;
        var currentIndex = IsCurrentResultIndex(_openingCurrentIndex) ? _openingCurrentIndex : -1;
        _list.SelectedIndex = selectedIndex;
        _list.SetProvisionalCurrentIndex(currentIndex);
    }

    /// <summary>Unifies the first available result's selection and current state after the popup
    /// makes its rows eligible, then publishes the completed open transition.</summary>
    private void OnOpened()
    {
        if (Items.Count > 0)
        {
            _ = _list.MoveSelection(Code.Home);
        }

        Opened?.Invoke(this, EventArgs.Empty);
    }

    private void OnClosed()
    {
        _wantsOpen = false;
        ClearPendingFirstResultSelection();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Resolution

    private void OnTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var committedText = Text;
        _wantsOpen |= Resolver is not null;
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(
            () => NotifyPropertyChanged(nameof(Text), InvalidationImpact.None),
            ref failure);

        if (!IsDisposed && string.Equals(Text, committedText, StringComparison.Ordinal))
        {
            ExceptionAggregation.Capture(BeginResolution, ref failure);
        }

        failure?.Throw();
    }

    private void BeginResolution()
    {
        ClearPendingFirstResultSelection();
        var generation = ++_resolutionGeneration;
        var lease = _resolutionOperation.Begin();
        var attachment = Dispatcher is null ? null : CaptureAttachment();
        var resolver = Resolver;

        if (resolver is null)
        {
            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(() => SetIsResolving(false), ref failure);
            ExceptionAggregation.Capture(() => ApplyResults(lease, generation, []), ref failure);
            failure?.Throw();
            return;
        }

        ExceptionDispatchInfo? startupFailure = null;
        ExceptionAggregation.Capture(() => SetIsResolving(true), ref startupFailure);

        if (!IsCurrentResolution(lease))
        {
            startupFailure?.Throw();
            return;
        }

        ValueTask<IReadOnlyList<object?>> pending;

        try
        {
            pending = resolver(Text, lease.CancellationToken);
        }
        catch (Exception exception)
        {
            ExceptionAggregation.Capture(
                () => ApplyFailure(lease, Text, exception),
                ref startupFailure);
            startupFailure?.Throw();
            return;
        }

        if (pending.IsCompletedSuccessfully)
        {
            ExceptionAggregation.Capture(
                () => ApplyCompletion(lease, generation, Text, pending.Result),
                ref startupFailure);
            startupFailure?.Throw();
            return;
        }

        _ = CompleteResolutionAsync(pending, Text, lease, generation, attachment);
        startupFailure?.Throw();
    }

    private async Task CompleteResolutionAsync(
        ValueTask<IReadOnlyList<object?>> pending,
        string searchTerms,
        LatestControlOperationLease lease,
        int generation,
        ControlAttachmentToken? attachment)
    {
        try
        {
            var results = await pending.ConfigureAwait(false);
            DispatchCompletion(
                lease,
                attachment,
                () => ApplyCompletion(lease, generation, searchTerms, results));
        }
        catch (OperationCanceledException) when (lease.CancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            DispatchCompletion(lease, attachment, () => ApplyFailure(lease, searchTerms, exception));
        }
    }

    private void DispatchCompletion(
        LatestControlOperationLease lease,
        ControlAttachmentToken? attachment,
        Action action)
    {
        if (attachment is not { } token)
        {
            if (Dispatcher is null && IsCurrentResolution(lease))
            {
                action();
            }

            return;
        }

        try
        {
            PostForCurrentAttachment(token, action, () => IsCurrentResolution(lease));
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ApplyResults(
        LatestControlOperationLease lease,
        int generation,
        IReadOnlyList<object?> results)
    {
        if (!IsCurrentResolution(lease))
        {
            return;
        }

        SetIsResolving(false);

        if (!IsCurrentResolution(lease))
        {
            return;
        }

        _list.Items = results;
        _list.SelectedIndex = -1;
        NotifyPropertyChanged(nameof(Items), InvalidationImpact.None);

        if (!IsCurrentResolution(lease))
        {
            return;
        }

        ResultsChanged?.Invoke(this, EventArgs.Empty);

        if (!IsCurrentResolution(lease))
        {
            return;
        }

        _popupCoordinator.SetOpen(_wantsOpen && Items.Count > 0);

        if (IsCurrentResolution(lease) &&
            _popupCoordinator.IsOpen &&
            _list.SelectedIndex < 0)
        {
            RequestFirstResultSelection(generation);
        }

        _ = _resolutionOperation.TryComplete(lease);
    }

    /// <summary>Retains refreshed-result selection intent until an attached dispatcher can run it
    /// after the frame requested by Items has arranged the replacement rows.</summary>
    private void RequestFirstResultSelection(int resolutionGeneration)
    {
        _pendingFirstSelectionResolutionGeneration = resolutionGeneration;
        _pendingFirstSelectionSessionGeneration = _popupCoordinator.SessionGeneration;
        SchedulePendingFirstResultSelection();
    }

    private void SchedulePendingFirstResultSelection()
    {
        if (_pendingFirstSelectionResolutionGeneration is null ||
            Dispatcher is not { } dispatcher ||
            ReferenceEquals(_pendingFirstSelectionDispatcher, dispatcher))
        {
            return;
        }

        if (_pendingFirstSelectionDispatcher is { } previousDispatcher)
        {
            previousDispatcher.Idle -= OnPendingFirstSelectionIdle;
        }

        _pendingFirstSelectionDispatcher = dispatcher;
        dispatcher.Idle += OnPendingFirstSelectionIdle;
        dispatcher.RequestIdle();
    }

    private void OnPendingFirstSelectionIdle(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (_pendingFirstSelectionResolutionGeneration is not { } resolutionGeneration)
        {
            ClearPendingFirstResultSelection();

            return;
        }

        var sessionGeneration = _pendingFirstSelectionSessionGeneration;
        ClearPendingFirstResultSelection();

        if (!IsDisposed && resolutionGeneration == _resolutionGeneration &&
            _popupCoordinator.IsOpen &&
            _popupCoordinator.SessionGeneration == sessionGeneration &&
            _list.SelectedIndex < 0)
        {
            _ = _list.MoveSelection(Code.Home);
        }
    }

    private void ClearPendingFirstResultSelection()
    {
        if (_pendingFirstSelectionDispatcher is { } dispatcher)
        {
            _pendingFirstSelectionDispatcher = null;
            dispatcher.Idle -= OnPendingFirstSelectionIdle;
        }

        _pendingFirstSelectionResolutionGeneration = null;
        _pendingFirstSelectionSessionGeneration = 0;
    }

    [Pure]
    private bool IsCurrentResultIndex(int index) => index == -1 || (index >= 0 && index < Items.Count);

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
                searchTerms,
                new InvalidOperationException("A command-palette resolver returned a null result snapshot."));
            return;
        }

        ApplyResults(lease, generation, results);
    }

    private void ApplyFailure(
        LatestControlOperationLease lease,
        string searchTerms,
        Exception exception)
    {
        if (!IsCurrentResolution(lease))
        {
            return;
        }

        SetIsResolving(false);

        if (!IsCurrentResolution(lease))
        {
            return;
        }

        _list.Items = [];
        NotifyPropertyChanged(nameof(Items), InvalidationImpact.None);

        if (!IsCurrentResolution(lease))
        {
            return;
        }

        ResultsChanged?.Invoke(this, EventArgs.Empty);

        if (!IsCurrentResolution(lease))
        {
            return;
        }

        _popupCoordinator.SetOpen(false);

        if (!IsCurrentResolution(lease))
        {
            return;
        }

        ResolutionFailed?.Invoke(
            this,
            new CommandPaletteResolutionFailedEventArgs(searchTerms, exception));
        _ = _resolutionOperation.TryComplete(lease);
    }

    [Pure]
    private bool IsCurrentResolution(LatestControlOperationLease lease) =>
        !IsDisposed && _resolutionOperation.IsCurrent(lease);

    private void SetIsResolving(bool value)
    {
        if (IsResolving == value)
        {
            return;
        }

        IsResolving = value;
        NotifyPropertyChanged(nameof(IsResolving), InvalidationImpact.None);
    }

    private void OnItemInvoked(object? sender, ItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        var activation = _itemActivation;
        _itemActivation = null;
        var isCurrentInvocation =
            activation is { } identity &&
            !IsDisposed &&
            IsOpen &&
            eventArgs.ActivationGeneration == identity.ItemGeneration &&
            eventArgs.Index == identity.ItemIndex &&
            eventArgs.Index == _list.SelectedIndex &&
            eventArgs.Index == _list.ActiveIndex &&
            _popupCoordinator.TransitionVersion == identity.PopupTransitionVersion &&
            _popupCoordinator.SessionGeneration == identity.PopupSessionGeneration;

        if (!isCurrentInvocation)
        {
            return;
        }

        _popupCoordinator.AcceptAndClose();
        ItemInvoked?.Invoke(
            this,
            new ItemInvokedEventArgs(eventArgs.Index, eventArgs.Item, eventArgs.Cause));
    }

    private void OnItemActivationStarting(object? sender, ItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        _itemActivation = !IsDisposed && IsOpen
            ? new PopupItemActivationIdentity(
                eventArgs.ActivationGeneration,
                eventArgs.Index,
                _popupCoordinator.TransitionVersion,
                _popupCoordinator.SessionGeneration)
            : null;
    }

    #endregion
}
