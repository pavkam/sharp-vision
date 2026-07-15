// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

using SharpVision.Runtime;

/// <summary>Defines the detached application root control and screen startup hooks.</summary>
public abstract class Screen: CompositeControl
{
    #region Construction

    /// <summary>Initializes a detached screen root.</summary>
    protected Screen()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    #endregion

    #region Application binding

    /// <summary>Gets the running application after <see cref="Attach"/> and before disposal.</summary>
    protected Application? Application { get; private set; }

    /// <summary>Binds the constructed application before interactive startup begins.</summary>
    /// <param name="application">The non-null detached application.</param>
    /// <exception cref="ArgumentNullException"><paramref name="application"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="application"/> does not own this screen as its root, or this screen is already
    /// bound to an application.
    /// </exception>
    /// <exception cref="InvalidOperationException">The screen has no initialized composition root.</exception>
    /// <exception cref="ObjectDisposedException">The screen is disposed.</exception>
    internal void Attach(Application application)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(application);

        if (Application is not null)
        {
            throw new ArgumentException("The screen is already bound to an application.", nameof(application));
        }

        if (!ReferenceEquals(application.Root, this))
        {
            throw new ArgumentException("The application must own this screen as its root.", nameof(application));
        }

        // Validate the complete retained composition before publishing the application binding.
        // CompositeControl rejects an uninitialized root here, not during the first frame.
        ValidateAttachment();
        Application = application;

        try
        {
            OnAttach(application);
            application.Started += OnApplicationStartedCore;
        }
        catch
        {
            application.Started -= OnApplicationStartedCore;
            Application = null;
            throw;
        }
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
        if (reason != ReleaseReason.Disposed)
        {
            base.OnUnavailable(reason);
            return;
        }

        var application = Application;
        application?.Started -= OnApplicationStartedCore;
        var failure = (ExceptionDispatchInfo?) null;
        CaptureFailure(OnDispose, ref failure);
        Application = null;
        CaptureFailure(() => base.OnUnavailable(reason), ref failure);
        failure?.Throw();
    }

    private void OnApplicationStartedCore(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is Application application && ReferenceEquals(application, Application))
        {
            OnStarted(application);
        }
    }

    private static void CaptureFailure(Action action, ref ExceptionDispatchInfo? failure)
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

    #endregion
}
