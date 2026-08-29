// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Notifications;

using SharpVision.Controls.Layout;

/// <summary>Coordinates live Toast ownership and inward stacking for one presentation host.</summary>
internal sealed class ToastCoordinator
{
    private const int _spacing = 1;
    private static readonly ConditionalWeakTable<ControlBase, ToastCoordinator> _coordinators = [];

    private readonly PresentationHost _host;
    private readonly List<Toast> _toasts = [];

    private ToastCoordinator(PresentationHost host) => _host = host;

    /// <summary>Resolves a host, mounts one Toast, and returns its stable coordinator.</summary>
    internal static ToastCoordinator Present(ControlBase owner, Toast toast)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(toast);
        owner.Dispatcher?.VerifyAccess();

        var host = PresentationHost.Resolve(owner) ??
            throw new ArgumentException("The owner must be attached beneath a presentation host.", nameof(owner));
        var coordinator = _coordinators.GetValue(host.Owner, _ => new ToastCoordinator(host));
        coordinator.Add(toast);
        return coordinator;
    }

    /// <summary>Returns the final edge-aligned slot for one registered Toast.</summary>
    internal Rect Constrain(Toast toast, Rect contentBounds)
    {
        ArgumentNullException.ThrowIfNull(toast);
        var width = Math.Min(toast.DesiredSize.Width, contentBounds.Width);
        var height = Math.Min(toast.DesiredSize.Height, contentBounds.Height);
        var inward = 0;

        for (var index = _toasts.Count - 1; index >= 0; index--)
        {
            var candidate = _toasts[index];
            if (ReferenceEquals(candidate, toast))
            {
                break;
            }

            if (candidate.Position == toast.Position)
            {
                inward = inward.SaturatingAdd(Math.Min(candidate.DesiredSize.Height, contentBounds.Height));
                inward = inward.SaturatingAdd(_spacing);
            }
        }

        var x = toast.Position switch
        {
            ToastPosition.TopLeft or ToastPosition.BottomLeft => contentBounds.X,
            ToastPosition.TopCenter or ToastPosition.BottomCenter =>
                contentBounds.X.SaturatingAdd(Math.Max(0, (contentBounds.Width - width) / 2)),
            ToastPosition.TopRight or ToastPosition.BottomRight => contentBounds.Right - width,
            _ => throw new UnreachableException()
        };
        var y = toast.Position switch
        {
            ToastPosition.TopLeft or ToastPosition.TopCenter or ToastPosition.TopRight =>
                Math.Min(contentBounds.Bottom - height, contentBounds.Y.SaturatingAdd(inward)),
            ToastPosition.BottomLeft or ToastPosition.BottomCenter or ToastPosition.BottomRight =>
                Math.Max(contentBounds.Y, contentBounds.Bottom - inward - height),
            _ => throw new UnreachableException()
        };

        return toast.ProjectAnimation(new Rect(x, y, width, height), contentBounds);
    }

    /// <summary>Removes one Toast from both coordinator and retained host ownership.</summary>
    internal void Remove(Toast toast)
    {
        ArgumentNullException.ThrowIfNull(toast);
        Forget(toast);

        if (_host.Owns(toast))
        {
            _ = _host.Remove(toast);
        }
    }

    /// <summary>Forgets one externally detached Toast without mutating retained ownership again.</summary>
    internal void Forget(Toast toast)
    {
        ArgumentNullException.ThrowIfNull(toast);
        _ = _toasts.Remove(toast);

        foreach (var remaining in _toasts)
        {
            remaining.Invalidate(Invalidation.Measure);
        }
    }

    private void Add(Toast toast)
    {
        Debug.Assert(!_toasts.Contains(toast), "An open Toast is registered only once.");
        _host.Add(toast);

        try
        {
            _toasts.Add(toast);
            Overlay.SetZIndex(toast, int.MaxValue);
        }
        catch
        {
            if (_host.Owns(toast))
            {
                _ = _host.Remove(toast);
            }

            throw;
        }
    }
}
