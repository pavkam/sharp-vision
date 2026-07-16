// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

using SharpVision.Terminal.Input;

/// <summary>Displays one selected value and composes a private popup list for choosing another.</summary>
public sealed class ComboBox: Control
{
    private readonly List _list;
    private readonly Popup _popup;
    private readonly OwnedControlSlot _popupSlot;
    private readonly PressInteraction _interaction;

    #region Construction and properties

    /// <summary>Initializes an empty combo box with a framed private popup list.</summary>
    public ComboBox()
    {
        _list = new List
        {
            SelectionMode = SelectionMode.Single,
            IsTabStop = false,
        };
        _list.ItemInvoked += OnItemInvoked;
        _list.SelectionChanged += OnSelectionChanged;
        _popup = new Popup
        {
            Anchor = this,
            Content = _list,
            TabNavigation = TabNavigation.Contained,
        };
        _popup.Closing += OnPopupClosing;
        _popup.Closed += OnPopupClosed;
        _popupSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Popup,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "drop-down",
                ChangeImpact.Measure),
            capacity: 1);
        _popupSlot.Add(_popup);
        _interaction = new PressInteraction(
            () => Bounds,
            () => EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || IsFocused,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed,
            Activate);
        CanFocus = true;
    }

    /// <summary>Raised after a selected index commits through direct assignment or the drop-down list.</summary>
    public event EventHandler<ListSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Gets or sets a copied list of choices displayed by the drop-down.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">A list template cannot realize the supplied values.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public IReadOnlyList<object?> Items
    {
        get => _list.Items;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();
            _list.Items = value;

            if (_list.SelectedIndex < 0 && _list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }

            NotifyPropertyChanged(nameof(Items), ChangeImpact.Measure);
        }
    }

    /// <summary>Gets or sets the selected index, or -1 when no value is selected.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the item range.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public int SelectedIndex
    {
        get => _list.SelectedIndex;
        set => _list.SelectedIndex = value;
    }

    /// <summary>Gets or sets the maximum visible list height in terminal cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public int DropDownHeight
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = 8;

    /// <summary>Gets or sets the axes available to the owned drop-down overflow host.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
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
            NotifyPropertyChanged(nameof(ScrollBars), ChangeImpact.None);
        }
    }

    /// <summary>Gets or sets the drop-down scrollbar reservation policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
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
            NotifyPropertyChanged(nameof(ShowScrollBars), ChangeImpact.None);
        }
    }

    /// <summary>Gets or sets the compact or full form of the owned drop-down rails.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public ScrollBarChrome ScrollBarChrome
    {
        get => _list.ScrollBarChrome;
        set
        {
            VerifyMutable();

            if (_list.ScrollBarChrome == value)
            {
                return;
            }

            _list.ScrollBarChrome = value;
            NotifyPropertyChanged(nameof(ScrollBarChrome), ChangeImpact.None);
        }
    }

    /// <summary>Gets or sets the generated line or block glyph treatment for owned drop-down rails.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public ScrollBarFill ScrollBarFill
    {
        get => _list.ScrollBarFill;
        set
        {
            VerifyMutable();

            if (_list.ScrollBarFill == value)
            {
                return;
            }

            _list.ScrollBarFill = value;
            NotifyPropertyChanged(nameof(ScrollBarFill), ChangeImpact.None);
        }
    }

    /// <summary>Gets or sets whether the field accepts typed text in addition to list selection.</summary>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public bool IsEditable
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Measure);
    }

    /// <summary>Gets or sets whether the private drop-down is arranged, rendered, and hit-testable.</summary>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public bool IsOpen
    {
        get => _popup.IsOpen;
        set
        {
            VerifyMutable();

            if (_popup.IsOpen != value)
            {
                _popup.IsOpen = value;
            }
        }
    }

    #endregion

    #region Input, layout, and rendering

    /// <inheritdoc/>
    protected override bool OwnsPointerState => true;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = MeasureChild(_popup, new Constraint(constraint.Width, Add(DropDownHeight, 2)));
        var width = Add(Terminal.Unicode.Width.Measure(SelectedText()).Cells, 2);
        return new Size(width, 1);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        ArrangeChild(_popup, RootBounds(bounds), ResolvedAxes.Both);

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        RenderChrome(canvas);

        var content = ContentBounds;
        var style = ResolvedStyle;
        var label = canvas.Clip(new Rect(content.X, content.Y, Math.Max(0, content.Width - 2), 1));
        _ = label.Draw(
            SelectedText().AsSpan(),
            new Point(content.X, content.Y),
            style,
            background: BackgroundMode.Transparent);
        _ = canvas.Draw(
            " ▼".AsSpan(),
            new Point(Math.Max(content.X, content.Right - 2), content.Y),
            style,
            background: BackgroundMode.Transparent);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        _interaction.Handle(eventArgs);

        if (eventArgs.Handled ||
            !IsOpen ||
            eventArgs is not KeyEventArgs { Stroke: { Code: Code.Escape, Action: KeyAction.Press } })
        {
            return;
        }

        IsOpen = false;
        eventArgs.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);
        _interaction.FocusChanged(focused);
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureCancelled(ReleaseReason reason)
    {
        base.OnPointerCaptureCancelled(reason);
        _interaction.CaptureCancelled();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _interaction.Unavailable();

        if (reason == ReleaseReason.Disposed)
        {
            _list.ItemInvoked -= OnItemInvoked;
            _list.SelectionChanged -= OnSelectionChanged;
            _popup.Closing -= OnPopupClosing;
            _popup.Closed -= OnPopupClosed;
            SelectionChanged = null;
        }
    }

    #endregion

    #region Drop-down coordination

    private void Activate(ActivationCause cause)
    {
        _ = cause;
        IsOpen = !IsOpen;
    }

    private void OnItemInvoked(object? sender, ItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        _list.SelectedIndex = eventArgs.Index;
        IsOpen = false;
    }

    private void OnSelectionChanged(object? sender, ListSelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        var failure = (ExceptionDispatchInfo?) null;
        CaptureFailure(
            () => NotifyPropertyChanged(nameof(SelectedIndex), ChangeImpact.Measure),
            ref failure);
        CaptureFailure(() => SelectionChanged?.Invoke(this, eventArgs), ref failure);
        failure?.Throw();
    }

    private void OnPopupClosing(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (ContainsFocused(_list))
        {
            _ = RequestFocus();
        }
    }

    private void OnPopupClosed(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        NotifyPropertyChanged(nameof(IsOpen), ChangeImpact.None);
    }

    #endregion

    #region Geometry

    private string SelectedText()
    {
        var index = _list.SelectedIndex;

        return index < 0 || index >= _list.Items.Count
            ? string.Empty
            : Convert.ToString(_list.Items[index], CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "ComboBox accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "ComboBox accumulation uses non-negative extents.");

        return (int) Math.Min(int.MaxValue, (long) left + right);
    }

    private static void CaptureFailure(System.Action action, ref ExceptionDispatchInfo? failure)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }

    private static bool ContainsFocused(Control control)
    {
        if (control.IsFocused)
        {
            return true;
        }

        for (var index = 0; index < control.OwnedControlCount; index++)
        {
            if (ContainsFocused(control.OwnedControlAt(index)))
            {
                return true;
            }
        }

        return false;
    }

    private Rect RootBounds(Rect fallback)
    {
        Control root = this;

        while (root.Parent is { } parent)
        {
            root = parent;
        }

        if (!ReferenceEquals(root, this) && root.Bounds.Width != 0 && root.Bounds.Height != 0)
        {
            return root.Bounds;
        }

        var viewport = LastMeasureConstraint;
        return new Rect(
            fallback.X,
            fallback.Y,
            viewport?.Width ?? fallback.Width,
            viewport?.Height ?? fallback.Height);
    }

    #endregion
}
