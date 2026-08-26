// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using System.ComponentModel;
using System.Runtime.ExceptionServices;

using Controls.Layout;

using Windows;

/// <summary>Owns the one active Window within an Application control tree.</summary>
internal sealed class WindowActivationManager: IDisposable
{
    private const int _maxHistory = 8;

    private readonly ControlBase _root;
    private readonly List<Window> _history = [];
    private readonly object _historyOwner = new();
    private bool _hasPendingActivation;
    private bool _isActivating;
    private bool _isDisposed;
    private Window? _pendingActivation;

    /// <summary>Initializes activation ownership for one attached application root.</summary>
    /// <param name="root">The non-null attached application root.</param>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="root"/> is disposed.</exception>
    internal WindowActivationManager(ControlBase root)
    {
        ArgumentNullException.ThrowIfNull(root);
        root.VerifyMutable();
        _root = root;
    }

    /// <summary>Gets the active available Window, or null.</summary>
    internal Window? ActiveWindow { get; private set; }

    /// <summary>Activates the nearest available Window ancestor of one target, or clears activation.</summary>
    /// <param name="target">The target control, or null.</param>
    /// <returns>The active Window, which also bounds pointer focus, or null.</returns>
    internal Window? Activate(ControlBase? target)
    {
        VerifyAccess();
        SetActive(FindWindow(target));
        return ActiveWindow;
    }

    /// <summary>Clears activation and releases active-Window observations.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _root.VerifyMutable();
        SetActive(null);
        _history.Clear();
        _isDisposed = true;
    }

    private Window? FindWindow(ControlBase? target)
    {
        Window? nearest = null;

        for (var current = target; current is not null; current = current.Parent)
        {
            nearest ??= current as Window;

            if (ReferenceEquals(current, _root))
            {
                return Available(nearest) ? nearest : null;
            }
        }

        return null;
    }

    private static bool Available(Window? window) =>
        window is not null &&
        !window.IsDisposed &&
        window.Dispatcher is not null &&
        window.EffectiveIsVisible &&
        window.EffectiveIsEnabled;

    private void SetActive(Window? value)
    {
        if (_isActivating)
        {
            _pendingActivation = value;
            _hasPendingActivation = true;
            return;
        }

        _isActivating = true;
        ExceptionDispatchInfo? failure = null;

        try
        {
            var requested = value;

            while (true)
            {
                _hasPendingActivation = false;
                CommitActive(requested, ref failure);

                if (_hasPendingActivation)
                {
                    requested = _pendingActivation;
                    continue;
                }

                break;
            }
        }
        finally
        {
            _pendingActivation = null;
            _hasPendingActivation = false;
            _isActivating = false;
        }

        failure?.Throw();
    }

    private void CommitActive(Window? value, ref ExceptionDispatchInfo? failure)
    {
        if (ReferenceEquals(ActiveWindow, value))
        {
            if (value is not null && !value.IsActive)
            {
                ExceptionAggregation.Capture(() => value.SetActive(true), ref failure);

                if (!_hasPendingActivation && Available(value) && FindWindow(value) is not null)
                {
                    ExceptionAggregation.Capture(() => Raise(value), ref failure);
                }
            }

            return;
        }

        var previous = ActiveWindow;

        if (previous is not null)
        {
            Unsubscribe(previous);
        }

        ActiveWindow = value;

        if (value is not null)
        {
            Subscribe(value);
            value.ActivationHistoryOwner = _historyOwner;
            _ = _history.RemoveAll(candidate => candidate.IsDisposed || !IsReachableFromRoot(candidate));
            _ = _history.Remove(value);
            _history.Insert(0, value);

            if (_history.Count > _maxHistory)
            {
                _history.RemoveRange(_maxHistory, _history.Count - _maxHistory);
            }

        }

        if (previous is not null)
        {
            ExceptionAggregation.Capture(() => previous.SetActive(false), ref failure);
        }

        if (_hasPendingActivation || !ReferenceEquals(ActiveWindow, value) || value is null)
        {
            return;
        }

        ExceptionAggregation.Capture(() => value.SetActive(true), ref failure);

        if (!_hasPendingActivation &&
            ReferenceEquals(ActiveWindow, value) &&
            Available(value) &&
            FindWindow(value) is not null)
        {
            ExceptionAggregation.Capture(() => Raise(value), ref failure);
        }
        else if (!_hasPendingActivation)
        {
            _pendingActivation = FindNextAvailable();
            _hasPendingActivation = true;
        }
    }

    // Popups, flyouts, and submenus render through a structurally separate,
    // always-on-top pass (see ControlBase.RenderOwnedPopupDescendants) and never
    // share this z-index space, so raising a Window here can never bury one -
    // this only ever reorders the activated Window among its sibling Windows,
    // by design, given how window/popup z-index coexistence is defined.
    private static void Raise(Window window)
    {
        if (window.Parent is not Overlay overlay)
        {
            return;
        }

        var currentZ = Overlay.GetZIndex(window);
        var maxSiblingZ = currentZ;
        var needsRaise = false;

        foreach (var sibling in overlay.Children)
        {
            if (ReferenceEquals(sibling, window) || sibling is not Window)
            {
                continue;
            }

            var siblingZ = Overlay.GetZIndex(sibling);
            needsRaise |= siblingZ >= currentZ;
            maxSiblingZ = Math.Max(maxSiblingZ, siblingZ);
        }

        if (needsRaise)
        {
            if (maxSiblingZ == int.MaxValue)
            {
                RenormalizeAndRaise(overlay, window);
            }
            else
            {
                Overlay.SetZIndex(window, maxSiblingZ + 1);
            }
        }
    }

    private static void RenormalizeAndRaise(Overlay overlay, Window window)
    {
        var ordered = overlay.Children
            .Where(static child => child is Window)
            .Where(child => !ReferenceEquals(child, window))
            .OrderBy(Overlay.GetZIndex)
            .ToArray();

        for (var index = 0; index < ordered.Length; index++)
        {
            Overlay.SetZIndex(ordered[index], index);
        }

        Overlay.SetZIndex(window, ordered.Length);
    }

    // Walks recency order for the next window this manager previously activated that is
    // still available and still attached under this manager's own root, so activation loss
    // (hide, close, detach) falls back the way every desktop windowing model does instead of
    // leaving the application with no active window.
    private Window? FindNextAvailable()
    {
        for (var index = 0; index < _history.Count; index++)
        {
            var candidate = _history[index];

            if (candidate.IsDisposed)
            {
                _history.RemoveAt(index);
                index--;
                continue;
            }

            if (Available(candidate) && IsReachableFromRoot(candidate))
            {
                return candidate;
            }
        }

        return FindAvailableInOwnedTree();
    }

    // History is deliberately bounded, so it can rank recent candidates but cannot be the source
    // of truth for whether an eligible Window still exists. The retained tree is that source.
    private Window? FindAvailableInOwnedTree()
    {
        var pending = new Stack<ControlBase>();
        pending.Push(_root);

        while (pending.TryPop(out var current))
        {
            if (current is Window candidate &&
                ReferenceEquals(candidate.ActivationHistoryOwner, _historyOwner) &&
                Available(candidate))
            {
                return candidate;
            }

            for (var index = current.OwnedControlCount - 1; index >= 0; index--)
            {
                pending.Push(current.OwnedControlAt(index));
            }
        }

        return null;
    }

    private bool IsReachableFromRoot(Window window)
    {
        for (ControlBase? current = window; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, _root))
            {
                return true;
            }
        }

        return false;
    }

    private void Subscribe(Window window)
    {
        window.PropertyChanged += OnActiveWindowPropertyChanged;
        window.ParentChanged += OnActiveWindowAvailabilityChanged;
    }

    private void Unsubscribe(Window window)
    {
        window.PropertyChanged -= OnActiveWindowPropertyChanged;
        window.ParentChanged -= OnActiveWindowAvailabilityChanged;
    }

    private void OnActiveWindowPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName is nameof(ControlBase.EffectiveIsVisible) or nameof(ControlBase.EffectiveIsEnabled))
        {
            OnActiveWindowAvailabilityChanged(sender, eventArgs);
        }
    }

    private void OnActiveWindowAvailabilityChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!Available(ActiveWindow) || FindWindow(ActiveWindow) is null)
        {
            SetActive(FindNextAvailable());
        }
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _root.VerifyMutable();
    }
}
