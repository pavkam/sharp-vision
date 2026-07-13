// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Runtime;

/// <summary>Defines the detached application root control and screen startup hooks.</summary>
public abstract class Screen: Container
{
    #region Construction

    /// <summary>Initializes a detached screen root.</summary>
    protected Screen()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    #endregion

    #region Layout

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        int width = 0;
        int height = 0;

        foreach (Control child in Children)
        {
            child.Measure(constraint);

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            width = Math.Max(width, child.DesiredSize.Width + child.Margin.Horizontal);
            height = Math.Max(height, child.DesiredSize.Height + child.Margin.Vertical);
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        foreach (Control child in Children)
        {
            child.Arrange(bounds);
        }
    }

    #endregion

    #region Application binding

    /// <summary>Gets the running application after <see cref="Attach"/> and before disposal.</summary>
    protected Application? Application { get; private set; }

    /// <summary>Binds the constructed application before interactive startup begins.</summary>
    /// <param name="application">The non-null detached application.</param>
    /// <exception cref="ArgumentNullException"><paramref name="application"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The screen is disposed.</exception>
    internal void Attach(Application application)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(application);
        Application = application;
        OnAttach(application);
        application.Started += OnApplicationStartedCore;
    }

    /// <summary>Applies screen-specific configuration before the first frame.</summary>
    /// <param name="application">The non-null running application.</param>
    protected virtual void OnAttach(Application application) =>
        ArgumentNullException.ThrowIfNull(application);

    /// <summary>Runs after the first committed frame or suspended startup.</summary>
    /// <param name="application">The non-null running application.</param>
    protected virtual void OnStarted(Application application) =>
        ArgumentNullException.ThrowIfNull(application);

    /// <summary>Releases screen-owned state when the control tree is disposed.</summary>
    protected virtual void OnDispose()
    {
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        if (reason == ReleaseReason.Disposed)
        {
            Application?.Started -= OnApplicationStartedCore;
            OnDispose();
            Application = null;
        }

        base.OnUnavailable(reason);
    }

    private void OnApplicationStartedCore(object? sender, EventArgs eventArgs)
    {
        if (sender is Application application)
        {
            OnStarted(application);
        }
    }

    #endregion
}
