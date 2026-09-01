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
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = 1;

    /// <summary>Gets or sets the optional asynchronous suggestion resolver.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public SuggestionResolver? Resolver
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
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

        if (!_wantsOpen)
        {
            _wantsOpen = true;
        }
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
        _popupCoordinator.OnOwnerAttached();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _popupCoordinator.OnOwnerUnavailable(reason);
        ExceptionDispatchInfo? failure = null;

        if (reason is ReleaseReason.Detached or ReleaseReason.Disposed)
        {
            ExceptionAggregation.Capture(_resolutionOperation.Cancel, ref failure);
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

    private void OnTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        NotifyPropertyChanged(nameof(Text), InvalidationImpact.None);
    }

    private void OnOpened()
    {
    }

    private void OnClosed() => _wantsOpen = false;

    #endregion
}
