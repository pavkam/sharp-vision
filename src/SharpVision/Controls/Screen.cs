// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

using Layout;

using SharpVision.Surfaces;

/// <summary>Defines the detached application root control and screen startup hooks.</summary>
[PublicAPI]
public abstract class Screen: CompositeControl
{
    private readonly Overlay _presentation;
    private readonly OwnedControlSlot _presentationSlot;

    #region Construction

    /// <summary>Initializes a detached stretch-aligned screen whose appearance resolves from the active theme.</summary>
    protected Screen()
    {
        _presentationSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "presentation",
                InvalidationImpact.Measure),
            capacity: 1);
        _presentation = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ClipToBounds = true,
            HitTestsOwnBounds = false,
            Face = new Face(
                Color.Default,
                Color.Transparent,
                TerminalAttributes.None,
                Underline.None,
                Color.Default)
        };
        _presentationSlot.Add(_presentation);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    /// <summary>Adds one detached temporary surface to the private application presentation plane.</summary>
    /// <param name="surface">The non-null detached floating surface.</param>
    internal void AddPresentation(FloatingSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _presentation.Children.Add(surface);
    }

    /// <summary>Removes one temporary surface from the private application presentation plane.</summary>
    /// <param name="surface">The non-null owned surface.</param>
    /// <returns>True when the identical surface was removed.</returns>
    internal bool RemovePresentation(FloatingSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        return _presentation.Children.Remove(surface);
    }

    /// <summary>Returns whether the private presentation Overlay directly owns a control.</summary>
    /// <param name="control">The non-null candidate.</param>
    /// <returns>True when the identical control is directly presented.</returns>
    internal bool OwnsPresentation(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return ReferenceEquals(control.Parent, _presentation);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var content = Content;
        var desired = MeasureChild(content, constraint);
        var width = content.Visibility == Visibility.Collapsed
            ? 0
            : LayoutMath.SaturatingAdd(desired.Width, content.Margin.Horizontal);
        var height = content.Visibility == Visibility.Collapsed
            ? 0
            : LayoutMath.SaturatingAdd(desired.Height, content.Margin.Vertical);

        desired = MeasureChild(_presentation, constraint);

        if (_presentation.Visibility != Visibility.Collapsed)
        {
            width = Math.Max(
                width,
                LayoutMath.SaturatingAdd(desired.Width, _presentation.Margin.Horizontal));
            height = Math.Max(
                height,
                LayoutMath.SaturatingAdd(desired.Height, _presentation.Margin.Vertical));
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        ArrangeChild(Content, bounds);
        ArrangeChild(_presentation, bounds);
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
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(OnDispose, ref failure);
        Application = null;
        ExceptionAggregation.Capture(() => base.OnUnavailable(reason), ref failure);
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

    #endregion
}
