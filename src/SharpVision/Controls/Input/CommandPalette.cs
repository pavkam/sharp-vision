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
    private CancellationTokenSource? _resolutionCancellation;
    private int _resolutionGeneration;
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
            () => Closed?.Invoke(this, EventArgs.Empty),
            ownerInitialFocus: _input);
        _ = AddHandler(Events.Key, OnKeyRouted, handledEventsToo: true);
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
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _resolutionGeneration++;
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(CancelResolution, ref failure);
        ExceptionAggregation.Capture(() => SetIsResolving(false), ref failure);

        if (reason == ReleaseReason.Disposed)
        {
            _input.TextChanged -= OnTextChanged;
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

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Preview || eventArgs.IsHandled || !IsOpen)
        {
            return;
        }

        if (eventArgs.IsInitialKeyDown &&
            eventArgs.Stroke.Code == Code.Escape &&
            eventArgs.Stroke.Modifiers.IsActivationEligible())
        {
            Close();
            eventArgs.IsHandled = true;
            return;
        }

        if (eventArgs.IsInitialKeyDown && eventArgs.Stroke.Code == Code.Enter)
        {
            eventArgs.IsHandled = _list.ActivateCurrent(
                ActivationCause.Keyboard,
                eventArgs.Stroke.Code,
                eventArgs.Stroke.Modifiers);
            return;
        }

        if (eventArgs.IsKeyDown &&
            eventArgs.Stroke.Code is Code.Up or Code.Down)
        {
            eventArgs.IsHandled = _list.MoveSelection(eventArgs.Stroke.Code);
        }
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
        CancelResolution();
        var generation = ++_resolutionGeneration;
        var resolver = Resolver;

        if (resolver is null)
        {
            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(() => SetIsResolving(false), ref failure);
            ExceptionAggregation.Capture(() => ApplyResults(generation, []), ref failure);
            failure?.Throw();
            return;
        }

        var cancellation = new CancellationTokenSource();
        _resolutionCancellation = cancellation;
        ExceptionDispatchInfo? startupFailure = null;
        ExceptionAggregation.Capture(() => SetIsResolving(true), ref startupFailure);

        if (!IsCurrentResolution(generation))
        {
            startupFailure?.Throw();
            return;
        }

        ValueTask<IReadOnlyList<object?>> pending;

        try
        {
            pending = resolver(Text, cancellation.Token);
        }
        catch (Exception exception)
        {
            ExceptionAggregation.Capture(
                () => ApplyFailure(generation, Text, exception),
                ref startupFailure);
            startupFailure?.Throw();
            return;
        }

        if (pending.IsCompletedSuccessfully)
        {
            ExceptionAggregation.Capture(
                () => ApplyCompletion(generation, Text, pending.Result),
                ref startupFailure);
            startupFailure?.Throw();
            return;
        }

        _ = CompleteResolutionAsync(pending, Text, generation, cancellation.Token);
        startupFailure?.Throw();
    }

    private async Task CompleteResolutionAsync(
        ValueTask<IReadOnlyList<object?>> pending,
        string searchTerms,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await pending.ConfigureAwait(false);
            DispatchCompletion(() => ApplyCompletion(generation, searchTerms, results));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            DispatchCompletion(() => ApplyFailure(generation, searchTerms, exception));
        }
    }

    private void DispatchCompletion(Action action)
    {
        var dispatcher = Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        try
        {
            dispatcher.Post(action);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ApplyResults(int generation, IReadOnlyList<object?> results)
    {
        if (!IsCurrentResolution(generation))
        {
            return;
        }

        SetIsResolving(false);

        if (!IsCurrentResolution(generation))
        {
            return;
        }

        _list.Items = results;
        _list.SelectedIndex = -1;
        NotifyPropertyChanged(nameof(Items), InvalidationImpact.None);

        if (!IsCurrentResolution(generation))
        {
            return;
        }

        ResultsChanged?.Invoke(this, EventArgs.Empty);

        if (!IsCurrentResolution(generation))
        {
            return;
        }

        _popupCoordinator.SetOpen(_wantsOpen && Items.Count > 0);

        if (IsCurrentResolution(generation) &&
            _popupCoordinator.IsOpen &&
            _list.SelectedIndex < 0)
        {
            _ = _list.MoveSelection(Code.Home);
        }
    }

    private void ApplyCompletion(
        int generation,
        string searchTerms,
        IReadOnlyList<object?>? results)
    {
        if (results is null)
        {
            ApplyFailure(
                generation,
                searchTerms,
                new InvalidOperationException("A command-palette resolver returned a null result snapshot."));
            return;
        }

        ApplyResults(generation, results);
    }

    private void ApplyFailure(int generation, string searchTerms, Exception exception)
    {
        if (!IsCurrentResolution(generation))
        {
            return;
        }

        SetIsResolving(false);

        if (!IsCurrentResolution(generation))
        {
            return;
        }

        _list.Items = [];
        NotifyPropertyChanged(nameof(Items), InvalidationImpact.None);

        if (!IsCurrentResolution(generation))
        {
            return;
        }

        ResultsChanged?.Invoke(this, EventArgs.Empty);

        if (!IsCurrentResolution(generation))
        {
            return;
        }

        _popupCoordinator.SetOpen(false);

        if (!IsCurrentResolution(generation))
        {
            return;
        }

        ResolutionFailed?.Invoke(
            this,
            new CommandPaletteResolutionFailedEventArgs(searchTerms, exception));
    }

    [Pure]
    private bool IsCurrentResolution(int generation) =>
        !IsDisposed && generation == _resolutionGeneration;

    private void SetIsResolving(bool value)
    {
        if (IsResolving == value)
        {
            return;
        }

        IsResolving = value;
        NotifyPropertyChanged(nameof(IsResolving), InvalidationImpact.None);
    }

    private void CancelResolution()
    {
        var cancellation = _resolutionCancellation;
        _resolutionCancellation = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private void OnItemInvoked(object? sender, ItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        Invoke(eventArgs.Index, eventArgs.Cause);
    }

    private void Invoke(int index, ActivationCause cause)
    {
        var eventArgs = new ItemInvokedEventArgs(index, Items[index], cause);
        Close();
        ItemInvoked?.Invoke(this, eventArgs);
    }

    #endregion
}
