// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using Collections;

using SharpVision.Terminal.Input;

/// <summary>Provides an editable command search field with asynchronously resolved popup results.</summary>
/// <remarks>
/// The retained editor remains the focus target in embedded, centered, and top-centered layouts.
/// Each text change cancels the prior request, and only the latest completion may publish items.
/// </remarks>
[PublicAPI]
public sealed class CommandPalette: CompositeControlBase
{
    private readonly RetainedPartProperty<Affix?> _endAffix;
    private readonly TextInput _input;
    private readonly RetainedPartProperty<ItemTemplate> _itemTemplate;
    private readonly ListView _list;
    private readonly RetainedPartProperty<string?> _placeholder;
    private readonly LatestControlOperation _resolutionOperation = new();
    private readonly RetainedPartProperty<Length> _rowHeight;
    private readonly RetainedPartProperty<Affix?> _startAffix;
    private int _resolutionGeneration;
    private int _openingSelectedIndex = -1;
    private int _openingCurrentIndex = -1;
    private int _itemsVersion;
    private int _selectionVersion;
    private int _openingItemsVersion;
    private int _openingSelectionVersion;
    private int? _pendingFirstSelectionResolutionGeneration;
    private ulong _pendingFirstSelectionSessionGeneration;
    private Dispatcher? _pendingFirstSelectionDispatcher;
    private PopupItemActivationIdentity? _itemActivation;
    private int _itemActivationResolutionGeneration;
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
            partKey: "results",
            beginSession: BeginNavigationSession,
            handleNavigationKey: HandleNavigationKey,
            cancelSession: CancelNavigationSession,
            acceptCurrent: AcceptCurrent);
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
    }

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
            CaptureFailure(
                () => NotifyPropertyChanged(nameof(Resolver), InvalidationImpact.None),
                ref failure);

            if (!IsDisposed && ReferenceEquals(field, value))
            {
                CaptureFailure(BeginResolution, ref failure);
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
        get => _itemTemplate.Value;
        set => _itemTemplate.Value = value;
    }

    /// <summary>Gets or sets the automatic, fixed, or popup-viewport-relative uniform result-row height.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A fixed or percentage value is zero.</exception>
    /// <exception cref="ArgumentException">The value uses proportional sizing.</exception>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public Length RowHeight
    {
        get => _rowHeight.Value;
        set => _rowHeight.Value = value;
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
        get => _placeholder.Value;
        set => _placeholder.Value = value;
    }

    /// <summary>Gets or sets the optional leading editor affix.</summary>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public Affix? StartAffix
    {
        get => _startAffix.Value;
        set => _startAffix.Value = value;
    }

    /// <summary>Gets or sets the optional trailing editor affix.</summary>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public Affix? EndAffix
    {
        get => _endAffix.Value;
        set => _endAffix.Value = value;
    }

    /// <summary>Gets or sets the complete local editor border.</summary>
    /// <remarks>Assigning either <see cref="FieldBorder"/> or <see cref="FieldShadow"/> sets the
    /// editor's complete local style: it snapshots the editor's whole resolved presentation into a
    /// local <see cref="TextInputStyle"/>, immediately and permanently disabling the editor's
    /// focused, hovered, and disabled state-reactivity for its ENTIRE appearance, not just the
    /// assigned facet, until both <see cref="ResetFieldBorder"/> and <see cref="ResetFieldShadow"/>
    /// have been called. A theme-authored facet neither property names (its affix gap, for
    /// instance) is likewise pinned to the value it resolved to at assignment time and stops
    /// tracking a later theme swap until both resets have been called.</remarks>
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
    /// <remarks>Assigning either <see cref="FieldShadow"/> or <see cref="FieldBorder"/> sets the
    /// editor's complete local style: it snapshots the editor's whole resolved presentation into a
    /// local <see cref="TextInputStyle"/>, immediately and permanently disabling the editor's
    /// focused, hovered, and disabled state-reactivity for its ENTIRE appearance, not just the
    /// assigned facet, until both <see cref="ResetFieldShadow"/> and <see cref="ResetFieldBorder"/>
    /// have been called. A theme-authored facet neither property names is likewise pinned to the
    /// value it resolved to at assignment time and stops tracking a later theme swap until both
    /// resets have been called.</remarks>
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

    /// <summary>Gets or sets whether the non-empty result popup is open.</summary>
    /// <exception cref="InvalidOperationException">The attached palette is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The palette is disposed.</exception>
    public bool IsOpen
    {
        get => IsPopupOpen;
        set => IsPopupOpen = value;
    }

    /// <inheritdoc/>
    protected override string PopupOpenPropertyName => nameof(IsOpen);

    /// <inheritdoc/>
    /// <remarks>Opening with no items starts a resolution instead; the request is remembered and
    /// honored once results arrive.</remarks>
    public override bool IsPopupOpen
    {
        get => base.IsPopupOpen;
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
                base.IsPopupOpen = false;
                return;
            }

            if (Items.Count > 0)
            {
                base.IsPopupOpen = true;
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
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);

        // While results are open the session's own preview handler owns Escape. With nothing open
        // and a query still resolving, nothing else would consume it, the open intent would
        // survive, and the late completion would present results the user had already dismissed.
        // Escape is documented to cancel: revoke the in-flight lease so its completion is discarded,
        // clear the resolving state, and drop the open intent - the retained items and text stay.
        if (eventArgs.IsHandled ||
            IsOpen ||
            !IsResolving ||
            eventArgs is not KeyEventArgs { IsInitialKeyDown: true } key ||
            key.Stroke.Code != Code.Escape ||
            !key.Stroke.Modifiers.IsActivationEligible())
        {
            return;
        }

        _wantsOpen = false;
        _resolutionGeneration++;
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(_resolutionOperation.Cancel, ref failure);
        ExceptionAggregation.Capture(() => SetIsResolving(false), ref failure);
        eventArgs.IsHandled = true;
        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        SchedulePendingFirstResultSelection();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        ClearPendingFirstResultSelection();
        ExceptionDispatchInfo? failure = null;

        if (reason is ReleaseReason.Detached or ReleaseReason.Disposed)
        {
            _resolutionGeneration++;
            CaptureFailure(_resolutionOperation.Cancel, ref failure);
            CaptureFailure(() => SetIsResolving(false), ref failure);
        }

        if (reason == ReleaseReason.Disposed)
        {
            _input.TextChanged -= OnTextChanged;
            _list.ItemActivationStarting -= OnItemActivationStarting;
            _list.ItemInvoked -= OnItemInvoked;
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
        _openingItemsVersion = _itemsVersion;
        _openingSelectionVersion = _selectionVersion;
        _itemActivation = null;
    }

    /// <summary>Activates the provisional row for the shared Enter-acceptance prologue, swallowing
    /// Enter without activating while a request is still resolving.</summary>
    /// <remarks>A resolving palette owns Enter without accepting anything yet: reporting acceptance
    /// here still marks the stroke handled (the coordinator's <c>markEnterHandledWithoutAcceptance</c>
    /// stays false for this owner, so only a true result marks it), which prevents Enter from
    /// falling through to the editor's own fallback handling while results are still in
    /// flight.</remarks>
    /// <param name="eventArgs">The routed Enter key event.</param>
    /// <returns>True when resolving (swallowed) or the provisional row activated.</returns>
    private bool AcceptCurrent(KeyEventArgs eventArgs)
    {
        if (IsResolving)
        {
            return true;
        }

        var stroke = eventArgs.Stroke;
        return _list.ActivateCurrent(ActivationCause.Keyboard, stroke.Code, stroke.Modifiers);
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

        var navigated = _list.HandleSelectionNavigationKey(eventArgs);
        eventArgs.IsHandled |= navigated;
        return navigated;
    }

    private void CancelNavigationSession()
    {
        _itemActivation = null;

        // A stale index that still happens to fall in range must not be restored: the version
        // guard proves the opening snapshot's items and selection are still the ones this session
        // opened against, not merely that the number it captured is coincidentally in bounds
        // against a since-replaced result set (for example a filter keystroke swapping Items for
        // an unrelated, same-or-larger-count snapshot while the session remained open).
        var openingSnapshotIsCurrent =
            _itemsVersion == _openingItemsVersion &&
            _selectionVersion == _openingSelectionVersion;
        var selectedIndex = openingSnapshotIsCurrent && IsCurrentResultIndex(_openingSelectedIndex)
            ? _openingSelectedIndex
            : -1;
        var currentIndex = openingSnapshotIsCurrent && IsCurrentResultIndex(_openingCurrentIndex)
            ? _openingCurrentIndex
            : -1;
        _list.SelectedIndex = selectedIndex;
        _list.SetProvisionalCurrentIndex(currentIndex);
    }

    /// <summary>Unifies the first available result's selection and current state after the popup
    /// makes its rows eligible, then publishes the completed open transition.</summary>
    protected override void OnDropDownOpened()
    {
        if (Items.Count > 0)
        {
            _ = _list.MoveSelection(Code.Home);
        }

        base.OnDropDownOpened();
    }

    /// <inheritdoc/>
    protected override void OnDropDownClosed()
    {
        _wantsOpen = false;
        ClearPendingFirstResultSelection();
        base.OnDropDownClosed();
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
        CaptureFailure(
            () => NotifyPropertyChanged(nameof(Text), InvalidationImpact.None),
            ref failure);

        if (!IsDisposed && string.Equals(Text, committedText, StringComparison.Ordinal))
        {
            CaptureFailure(BeginResolution, ref failure);
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
            CaptureFailure(() => SetIsResolving(false), ref failure);
            CaptureFailure(() => ApplyResults(lease, generation, []), ref failure);
            failure?.Throw();
            return;
        }

        ExceptionDispatchInfo? startupFailure = null;
        CaptureFailure(() => SetIsResolving(true), ref startupFailure);

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
            CaptureFailure(
                () => ApplyFailure(lease, Text, exception),
                ref startupFailure);
            startupFailure?.Throw();
            return;
        }

        if (pending.IsCompletedSuccessfully)
        {
            CaptureFailure(
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
            DispatchToCurrentAttachment(
                attachment,
                () => ApplyCompletion(lease, generation, searchTerms, results),
                () => IsCurrentResolution(lease));
        }
        catch (OperationCanceledException) when (lease.CancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            DispatchToCurrentAttachment(
                attachment,
                () => ApplyFailure(lease, searchTerms, exception),
                () => IsCurrentResolution(lease));
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

        _itemsVersion++;
        _list.Items = results;
        _list.SelectedIndex = -1;
        _selectionVersion++;
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

        SetResultsOpenState(_wantsOpen && Items.Count > 0);

        if (IsCurrentResolution(lease) &&
            IsOpen &&
            _list.SelectedIndex < 0)
        {
            RequestFirstResultSelection(generation);
        }

        _ = _resolutionOperation.TryComplete(lease);
    }

    /// <summary>Applies a results-driven open-state change: never enters a modal scope for an
    /// unavailable owner, and never lets a close the user did not ask for pull focus out of the
    /// editor.</summary>
    private void SetResultsOpenState(bool open)
    {
        if (open)
        {
            if (!EffectiveIsEnabled || !EffectiveIsVisible)
            {
                // Mirrors the IsOpen setter. A completion can land while the palette is hidden or
                // disabled (availability does not cancel the live request), and ModalityManager
                // refuses a hidden or disabled modal root; letting that refusal escape here would
                // surface as an unhandled dispatcher exception from the asynchronous completion.
                // The committed items stay; only the open intent is dropped.
                _wantsOpen = false;
                return;
            }

            base.IsPopupOpen = true;
            return;
        }

        var editorHadFocus = _input.IsFocused;
        base.IsPopupOpen = false;

        // Exiting the modal scope restores the focus that preceded Open(), which is right for
        // Escape, activation, and light dismissal but wrong for a close the user did not request:
        // a query that yields no results (or fails) must leave the editor focused so the next
        // keystroke still edits the query instead of landing on whatever was focused before.
        if (editorHadFocus &&
            !IsDisposed &&
            !_input.IsFocused &&
            _input.EffectiveIsEnabled &&
            _input.EffectiveIsVisible)
        {
            _ = _input.Focus();
        }
    }

    /// <summary>Retains refreshed-result selection intent until an attached dispatcher can run it
    /// after the frame requested by Items has arranged the replacement rows.</summary>
    private void RequestFirstResultSelection(int resolutionGeneration)
    {
        _pendingFirstSelectionResolutionGeneration = resolutionGeneration;
        _pendingFirstSelectionSessionGeneration = PopupSessionGeneration;
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
            IsOpen &&
            PopupSessionGeneration == sessionGeneration &&
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

        _itemsVersion++;
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

        SetResultsOpenState(false);

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
            !IsResolving &&
            _itemActivationResolutionGeneration == _resolutionGeneration &&
            eventArgs.ActivationGeneration == identity.ItemGeneration &&
            eventArgs.Index == identity.ItemIndex &&
            eventArgs.Index == _list.SelectedIndex &&
            eventArgs.Index == _list.ActiveIndex &&
            PopupTransitionVersion == identity.PopupTransitionVersion &&
            PopupSessionGeneration == identity.PopupSessionGeneration;

        if (!isCurrentInvocation)
        {
            return;
        }

        AcceptPopupAndClose();
        ItemInvoked?.Invoke(
            this,
            new ItemInvokedEventArgs(eventArgs.Index, eventArgs.Item, eventArgs.Cause));
    }

    private void OnItemActivationStarting(object? sender, ItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        _itemActivationResolutionGeneration = _resolutionGeneration;
        _itemActivation = !IsDisposed && IsOpen
            ? new PopupItemActivationIdentity(
                eventArgs.ActivationGeneration,
                eventArgs.Index,
                PopupTransitionVersion,
                PopupSessionGeneration)
            : null;
    }

    #endregion
}
