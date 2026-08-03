// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using Controls.Layout;

using Windows;

/// <summary>Owns the one active Window within an Application control tree.</summary>
internal sealed class WindowActivationManager: IDisposable
{
    private const int _maxHistory = 8;

    private readonly ControlBase _root;
    private readonly List<Window> _history = [];
    private bool _isDisposed;

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
                return IsAvailable(nearest) ? nearest : null;
            }
        }

        return null;
    }

    private static bool IsAvailable(Window? window) =>
        window is not null &&
        !window.IsDisposed &&
        window.Dispatcher is not null &&
        window.EffectiveIsVisible &&
        window.EffectiveIsEnabled;

    private void SetActive(Window? value)
    {
        if (ReferenceEquals(ActiveWindow, value))
        {
            return;
        }

        if (ActiveWindow is { } previous)
        {
            Unsubscribe(previous);
            previous.SetActive(false);
        }

        ActiveWindow = value;

        if (value is not null)
        {
            _ = _history.Remove(value);
            _history.Insert(0, value);

            if (_history.Count > _maxHistory)
            {
                _history.RemoveRange(_maxHistory, _history.Count - _maxHistory);
            }

            value.SetActive(true);
            Subscribe(value);
            Raise(value);
        }
    }

    // Popups, flyouts, and submenus render through a structurally separate,
    // always-on-top pass (see ControlBase.RenderOwnedPopupDescendants) and never
    // share this z-index space, so raising a Window here can never bury one -
    // this only ever reorders the activated Window among its sibling Windows
    // (see #224's maintainer decision on window/popup z-index coexistence).
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
            Overlay.SetZIndex(window, maxSiblingZ == int.MaxValue ? int.MaxValue : maxSiblingZ + 1);
        }
    }

    // Walks recency order for the next window this manager previously activated that is
    // still available and still attached under this manager's own root, so activation loss
    // (hide, close, detach) falls back the way every desktop windowing model does instead of
    // leaving the application with no active window (see #224).
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

            if (IsAvailable(candidate) && IsReachableFromRoot(candidate))
            {
                return candidate;
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
        window.VisibilityChanged += OnActiveWindowAvailabilityChanged;
        window.IsEnabledChanged += OnActiveWindowAvailabilityChanged;
        window.ParentChanged += OnActiveWindowAvailabilityChanged;
    }

    private void Unsubscribe(Window window)
    {
        window.VisibilityChanged -= OnActiveWindowAvailabilityChanged;
        window.IsEnabledChanged -= OnActiveWindowAvailabilityChanged;
        window.ParentChanged -= OnActiveWindowAvailabilityChanged;
    }

    private void OnActiveWindowAvailabilityChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!IsAvailable(ActiveWindow) || FindWindow(ActiveWindow) is null)
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
