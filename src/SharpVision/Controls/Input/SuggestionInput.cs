// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using Collections;

using Popups;

using Scrolling;

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
    private int _resolutionGeneration;
    private ulong _minimumPrefixLengthVersion;
    private ulong _resolverVersion;
    private ulong _textCommitVersion;
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
            ownerInitialFocus: _input);
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
                _popupCoordinator.SetOpen(false);
                return;
            }

            _wantsOpen = true;

            if (!EffectiveIsEnabled || !EffectiveIsVisible || Suggestions.Count == 0)
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
        IsOpen = true;
        return _input.Focus();
    }

    /// <summary>Closes suggestions while preserving the current editor text and any current resolver request.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void Close() => IsOpen = false;

    #endregion

    #region Resolution

    private void OnTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var committedText = Text;
        var version = ++_textCommitVersion;
        var resolutionGeneration = _resolutionGeneration;
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
            ExceptionAggregation.Capture(BeginResolution, ref failure);
        }

        failure?.Throw();
    }

    private void BeginResolution()
    {
        var generation = ++_resolutionGeneration;
        var lease = _resolutionOperation.Begin();
        var attachment = Dispatcher is null ? null : CaptureAttachment();
        var searchTerms = Text;
        var resolver = Resolver;

        if (!IsCurrentResolution(lease, generation))
        {
            return;
        }

        if (resolver is null || !MeetsMinimumPrefixLength(searchTerms, MinimumPrefixLength))
        {
            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(() => SetIsResolving(false), ref failure);
            ExceptionAggregation.Capture(
                () => ApplyResults(lease, generation, []),
                ref failure);
            failure?.Throw();
            return;
        }

        ExceptionDispatchInfo? startupFailure = null;
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
                () => ApplyCancellation(lease, generation),
                ref startupFailure);
            startupFailure?.Throw();
            return;
        }
        catch (Exception exception)
        {
            ExceptionAggregation.Capture(
                () => ApplyFailure(lease, generation, searchTerms, exception),
                ref startupFailure);
            startupFailure?.Throw();
            return;
        }

        if (pending.IsCompletedSuccessfully)
        {
            ExceptionAggregation.Capture(
                () => ApplyCompletion(lease, generation, searchTerms, pending.Result),
                ref startupFailure);
            startupFailure?.Throw();
            return;
        }

        _ = CompleteResolutionAsync(pending, searchTerms, lease, generation, attachment);
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
                generation,
                attachment,
                () => ApplyCompletion(lease, generation, searchTerms, results));
        }
        catch (OperationCanceledException)
        {
            DispatchCompletion(
                lease,
                generation,
                attachment,
                () => ApplyCancellation(lease, generation));
        }
        catch (Exception exception)
        {
            DispatchCompletion(
                lease,
                generation,
                attachment,
                () => ApplyFailure(lease, generation, searchTerms, exception));
        }
    }

    private void DispatchCompletion(
        LatestControlOperationLease lease,
        int generation,
        ControlAttachmentToken? attachment,
        Action action)
    {
        if (attachment is not { } token)
        {
            if (Dispatcher is null && IsCurrentResolution(lease, generation))
            {
                action();
            }

            return;
        }

        try
        {
            PostForCurrentAttachment(
                token,
                action,
                () => IsCurrentResolution(lease, generation));
        }
        catch (ObjectDisposedException)
        {
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

        ApplyResults(lease, generation, results);
    }

    private void ApplyResults(
        LatestControlOperationLease lease,
        int generation,
        IReadOnlyList<object?> results)
    {
        if (!PublishResults(lease, generation, results, allowOpen: true))
        {
            return;
        }

        CompleteResolution(lease, generation);
    }

    private void ApplyFailure(
        LatestControlOperationLease lease,
        int generation,
        string searchTerms,
        Exception exception)
    {
        if (!PublishResults(lease, generation, [], allowOpen: false))
        {
            return;
        }

        ResolutionFailed?.Invoke(
            this,
            new SuggestionResolutionFailedEventArgs(searchTerms, exception));

        if (!IsCurrentResolution(lease, generation))
        {
            return;
        }

        CompleteResolution(lease, generation);
    }

    private void ApplyCancellation(LatestControlOperationLease lease, int generation)
    {
        if (!PublishResults(lease, generation, [], allowOpen: false))
        {
            return;
        }

        CompleteResolution(lease, generation);
    }

    private bool PublishResults(
        LatestControlOperationLease lease,
        int generation,
        IReadOnlyList<object?> results,
        bool allowOpen)
    {
        if (!IsCurrentResolution(lease, generation))
        {
            return false;
        }

        SetIsResolving(false);

        if (!IsCurrentResolution(lease, generation))
        {
            return false;
        }

        var changed = !SnapshotsEqual(Suggestions, results);
        _list.Items = results;
        _list.SelectedIndex = -1;
        _list.SetProvisionalCurrentIndex(-1);

        if (changed)
        {
            NotifyPropertyChanged(nameof(Suggestions), InvalidationImpact.None);

            if (!IsCurrentResolution(lease, generation))
            {
                return false;
            }

            SuggestionsChanged?.Invoke(this, EventArgs.Empty);

            if (!IsCurrentResolution(lease, generation))
            {
                return false;
            }
        }

        _popupCoordinator.SetOpen(allowOpen && _wantsOpen && Suggestions.Count > 0);
        return IsCurrentResolution(lease, generation);
    }

    private void CompleteResolution(LatestControlOperationLease lease, int generation)
    {
        if (IsCurrentResolution(lease, generation))
        {
            _ = _resolutionOperation.TryComplete(lease);
        }
    }

    [Pure]
    private bool IsCurrentResolution(LatestControlOperationLease lease, int generation) =>
        !IsDisposed && generation == _resolutionGeneration && _resolutionOperation.IsCurrent(lease);

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
    protected override void OnAttached()
    {
        base.OnAttached();
        ExceptionDispatchInfo? failure = null;

        if (_resolutionOperation.HasCurrent)
        {
            var cancellationGeneration = ++_resolutionGeneration;
            ExceptionAggregation.Capture(_resolutionOperation.Cancel, ref failure);

            if (_resolutionGeneration == cancellationGeneration)
            {
                ExceptionAggregation.Capture(() => SetIsResolving(false), ref failure);
            }
        }

        ExceptionAggregation.Capture(_popupCoordinator.OnOwnerAttached, ref failure);
        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _popupCoordinator.OnOwnerUnavailable(reason);
        ExceptionDispatchInfo? failure = null;

        if (reason is ReleaseReason.Detached or ReleaseReason.Disposed)
        {
            var cancellationGeneration = ++_resolutionGeneration;
            ExceptionAggregation.Capture(_resolutionOperation.Cancel, ref failure);

            if (_resolutionGeneration == cancellationGeneration)
            {
                ExceptionAggregation.Capture(() => SetIsResolving(false), ref failure);
            }
        }

        if (reason == ReleaseReason.Disposed)
        {
            _input.TextChanged -= OnTextChanged;
            ExceptionAggregation.Capture(_popupCoordinator.Detach, ref failure);
            if (SuggestionsChanged is not null)
            {
                SuggestionsChanged = null;
            }

            if (ResolutionFailed is not null)
            {
                ResolutionFailed = null;
            }

            if (SuggestionAccepted is not null)
            {
                SuggestionAccepted = null;
            }
        }

        failure?.Throw();
    }

    private void OnOpened()
    {
    }

    private void OnClosed() => _wantsOpen = false;

    #endregion
}
