// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Runtime.ExceptionServices;

using SharpVision.Controls.Input;
using SharpVision.Terminal.Input;

/// <summary>Owns the Calendar, synchronization, and popup-session state shared by date-bearing
/// segmented inputs.</summary>
/// <typeparam name="T">The owning input's immutable temporal value type.</typeparam>
internal sealed class CalendarDropDownCoordinator<T>: IDisposable
    where T : struct
{
    private readonly Action _ensureSeeded;
    private readonly Func<T?> _getValue;
    private readonly Action<T?> _setValue;
    private readonly Func<T, DateOnly> _extractDate;
    private readonly Func<DateOnly, T?, T> _combineDate;
    private readonly Func<DateOnly> _getMinimumDate;
    private readonly Func<DateOnly> _getMaximumDate;
    private readonly Func<long> _getValueVersion;
    private readonly Func<long> _getBoundsVersion;
    private readonly Func<bool> _isOpen;
    private readonly Action _closePopup;
    private readonly Action _acceptAndClose;
    private DateOnly _openingActiveDate;
    private DateInterval? _openingSelection;
    private long _openingValueVersion;
    private long _openingBoundsVersion;
    private int _synchronizationDepth;
    private bool _disposed;

    /// <summary>Initializes a connected single-date Calendar and its typed conversion callbacks.</summary>
    /// <param name="culture">The initial Gregorian display culture.</param>
    /// <param name="ensureSeeded">Resolves the owner's lazy dispatcher clock seed before opening.</param>
    /// <param name="getValue">Reads the committed owner value without forcing a seed.</param>
    /// <param name="setValue">Commits a date combined into the owner's temporal type.</param>
    /// <param name="extractDate">Extracts the calendar date from a non-null owner value.</param>
    /// <param name="combineDate">Combines a selected date with the current value's non-date precision.</param>
    /// <param name="getMinimumDate">Projects the owner's lower bound into Calendar range.</param>
    /// <param name="getMaximumDate">Projects the owner's upper bound into Calendar range.</param>
    /// <param name="getValueVersion">Reads the current committed-value version.</param>
    /// <param name="getBoundsVersion">Reads the current bounds version.</param>
    /// <param name="isOpen">Reports whether the popup is open.</param>
    /// <param name="closePopup">Closes the popup without accepting its active date.</param>
    /// <param name="acceptAndClose">Accepts the active popup session and closes it.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public CalendarDropDownCoordinator(
        CultureInfo culture,
        Action ensureSeeded,
        Func<T?> getValue,
        Action<T?> setValue,
        Func<T, DateOnly> extractDate,
        Func<DateOnly, T?, T> combineDate,
        Func<DateOnly> getMinimumDate,
        Func<DateOnly> getMaximumDate,
        Func<long> getValueVersion,
        Func<long> getBoundsVersion,
        Func<bool> isOpen,
        Action closePopup,
        Action acceptAndClose)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(ensureSeeded);
        ArgumentNullException.ThrowIfNull(getValue);
        ArgumentNullException.ThrowIfNull(setValue);
        ArgumentNullException.ThrowIfNull(extractDate);
        ArgumentNullException.ThrowIfNull(combineDate);
        ArgumentNullException.ThrowIfNull(getMinimumDate);
        ArgumentNullException.ThrowIfNull(getMaximumDate);
        ArgumentNullException.ThrowIfNull(getValueVersion);
        ArgumentNullException.ThrowIfNull(getBoundsVersion);
        ArgumentNullException.ThrowIfNull(isOpen);
        ArgumentNullException.ThrowIfNull(closePopup);
        ArgumentNullException.ThrowIfNull(acceptAndClose);

        _ensureSeeded = ensureSeeded;
        _getValue = getValue;
        _setValue = setValue;
        _extractDate = extractDate;
        _combineDate = combineDate;
        _getMinimumDate = getMinimumDate;
        _getMaximumDate = getMaximumDate;
        _getValueVersion = getValueVersion;
        _getBoundsVersion = getBoundsVersion;
        _isOpen = isOpen;
        _closePopup = closePopup;
        _acceptAndClose = acceptAndClose;
        Calendar = new Calendar
        {
            Culture = culture,
            IsTabStop = false,
            SelectionMode = CalendarSelectionMode.Select
        };
        Calendar.DateActivated += OnDateActivated;
        SyncBounds();
    }

    /// <summary>Gets the owned Calendar mounted in the popup.</summary>
    public Calendar Calendar { get; }

    /// <summary>Gets whether a programmatic Calendar synchronization is in progress.</summary>
    public bool IsSynchronizing => _synchronizationDepth > 0;

    /// <summary>Synchronizes culture without treating resulting Calendar callbacks as user input.</summary>
    public void SyncCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        RunSynchronized(() => Calendar.Culture = culture);
    }

    /// <summary>Synchronizes both projected Calendar bounds.</summary>
    public void SyncBounds()
    {
        ExceptionDispatchInfo? failure = null;
        RunSynchronized(() =>
        {
            ExceptionAggregation.Capture(() => Calendar.MinimumDate = _getMinimumDate(), ref failure);
            ExceptionAggregation.Capture(() => Calendar.MaximumDate = _getMaximumDate(), ref failure);
        });
        failure?.Throw();
    }

    /// <summary>Pushes committed selection and displayed month into the Calendar.</summary>
    /// <param name="value">The committed owner value, or null.</param>
    public void SyncValue(T? value)
    {
        RunSynchronized(() =>
        {
            if (value is { } current)
            {
                var date = _extractDate(current);
                Calendar.DisplayMonth = new DateOnly(date.Year, date.Month, 1);
                Calendar.Selection = new DateInterval(date, date);
            }
            else
            {
                _ = Calendar.ClearSelection();
            }
        });
    }

    /// <summary>Resolves lazy state and refreshes Calendar presentation immediately before opening.</summary>
    public void BeforeOpen()
    {
        _ensureSeeded();
        SyncBounds();
        SyncValue(_getValue());
    }

    /// <summary>Captures the opening navigation and model versions for cancellation.</summary>
    public void BeginSession()
    {
        _openingActiveDate = Calendar.ActiveDate;
        _openingSelection = Calendar.Selection;
        _openingValueVersion = _getValueVersion();
        _openingBoundsVersion = _getBoundsVersion();
    }

    /// <summary>Handles popup navigation, cancellation, traversal, and acceptance keys.</summary>
    /// <param name="eventArgs">The routed key event.</param>
    /// <returns>True when popup navigation consumes the event.</returns>
    public bool HandleNavigationKey(KeyEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var stroke = eventArgs.Stroke;

        if (eventArgs.IsInitialKeyDown &&
            stroke.Code == Code.Escape &&
            stroke.Modifiers.IsActivationEligible())
        {
            eventArgs.IsHandled = true;
            _closePopup();
            return true;
        }

        if (eventArgs.IsInitialKeyDown &&
            stroke.Code == Code.Tab &&
            KeyboardModifierPolicy.IsTabTraversalEligible(stroke.Modifiers))
        {
            _closePopup();
            return false;
        }

        if (eventArgs.IsRepeat &&
            stroke.Code == Code.Down &&
            KeyboardModifierPolicy.MatchesCommand(stroke.Modifiers, Modifiers.Alt))
        {
            return true;
        }

        var accepts = eventArgs.IsInitialKeyDown &&
            stroke.Modifiers.IsActivationEligible() &&
            (stroke.Code == Code.Enter ||
             (stroke.Code == Code.Character && stroke.Character == new Rune(' ')));

        if (accepts)
        {
            eventArgs.IsHandled = true;
        }

        return Calendar.HandleNavigationKey(eventArgs) || accepts;
    }

    /// <summary>Restores the opening Calendar state when still current, otherwise resynchronizes
    /// the latest committed value and bounds.</summary>
    public void CancelSession()
    {
        if (_getBoundsVersion() == _openingBoundsVersion &&
            _getValueVersion() == _openingValueVersion &&
            IsOpeningStateValid())
        {
            RestoreOpeningState();
            return;
        }

        SyncBounds();
        SyncValue(_getValue());
    }

    /// <summary>Commits the Calendar's active date through the owner's type-preserving combiner.</summary>
    public void AcceptSession() =>
        _setValue(_combineDate(Calendar.ActiveDate, _getValue()));

    /// <summary>Detaches the Calendar activation callback. Repeated disposal is safe.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Calendar.DateActivated -= OnDateActivated;
    }

    private void OnDateActivated(DateOnly date)
    {
        _ = date;

        if (!IsSynchronizing && _isOpen())
        {
            _acceptAndClose();
        }
    }

    private void RestoreOpeningState()
    {
        RunSynchronized(() =>
        {
            Calendar.Selection = null;
            Calendar.Selection = new DateInterval(_openingActiveDate, _openingActiveDate);

            if (_openingSelection is null)
            {
                Calendar.Selection = null;
            }
            else if (_openingSelection.Value.Start != _openingActiveDate)
            {
                Calendar.Selection = _openingSelection;
            }
        });
    }

    [Pure]
    private bool IsOpeningStateValid() =>
        IsWithinBounds(_openingActiveDate) &&
        (_openingSelection is not { } selection ||
         (IsWithinBounds(selection.Start) && IsWithinBounds(selection.End)));

    [Pure]
    private bool IsWithinBounds(DateOnly date) =>
        date >= Calendar.MinimumDate && date <= Calendar.MaximumDate;

    private void RunSynchronized(Action action)
    {
        _synchronizationDepth++;

        try
        {
            action();
        }
        finally
        {
            _synchronizationDepth--;
        }
    }
}
