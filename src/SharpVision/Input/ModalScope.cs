// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Runtime.ExceptionServices;

/// <summary>Owns one disposable modal-plane lifetime and its insertion-ordered roots.</summary>
[PublicAPI]
public sealed class ModalScope: IDisposable
{
    #region Construction and state

    private readonly ModalityManager _manager;
    private readonly List<Control> _roots;

    /// <summary>Initializes one inactive scope before its manager commits it to the stack.</summary>
    /// <param name="manager">The non-null manager that owns the lifetime.</param>
    /// <param name="root">The validated primary plane root.</param>
    /// <param name="outsideInteraction">The validated outside-interaction policy.</param>
    /// <param name="previousFocus">The focus target observed before entry, or null.</param>
    /// <param name="previousCapture">The capture target observed before entry, or null.</param>
    internal ModalScope(
        ModalityManager manager,
        Control root,
        OutsideInteraction outsideInteraction,
        Control? previousFocus,
        Control? previousCapture)
    {
        Debug.Assert(manager is not null, "A modal scope requires an owning manager.");
        Debug.Assert(root is not null, "A modal scope requires a primary root.");
        Debug.Assert(Enum.IsDefined(outsideInteraction), "The manager validates outside interaction before construction.");
        _manager = manager;
        _roots = [root];
        Root = root;
        OutsideInteraction = outsideInteraction;
        PreviousFocus = previousFocus;
        PreviousCapture = previousCapture;
    }

    /// <summary>Raised when qualifying outside interaction asks the active owner to dismiss.</summary>
    public event EventHandler? DismissRequested;

    /// <summary>Raised exactly once after this scope commits inactive.</summary>
    public event EventHandler? Exited;

    /// <summary>Gets the primary root whose loss ends this scope and every younger scope.</summary>
    public Control Root { get; }

    /// <summary>Gets the policy applied to qualifying interaction outside every plane root.</summary>
    public OutsideInteraction OutsideInteraction { get; }

    /// <summary>Gets whether this scope remains interaction-active and has not committed exit.</summary>
    public bool IsActive { get; internal set; }

    /// <summary>Gets the focus target observed immediately before this scope entered, or null.</summary>
    internal Control? PreviousFocus { get; }

    /// <summary>Gets the capture target observed immediately before this scope entered, or null.</summary>
    internal Control? PreviousCapture { get; }

    /// <summary>Gets the number of roots in deterministic inclusion order.</summary>
    internal int RootCount => _roots.Count;

    #endregion

    #region Plane membership and lifetime

    /// <summary>Adds one validated disjoint root to this modal plane.</summary>
    /// <param name="root">The non-null attached and available application-tree root.</param>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The root is foreign, unavailable, overlaps another plane root, or belongs to another active plane.
    /// </exception>
    /// <exception cref="InvalidOperationException">The scope is inactive or the caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The root or owning manager is disposed.</exception>
    public void Include(Control root) => _manager.Include(this, root);

    /// <summary>Ends this scope and every younger scope; repeated calls are harmless.</summary>
    /// <remarks>
    /// Every affected scope commits inactive before a reentrant call returns. Focus restoration and
    /// <see cref="Exited"/> publication may wait for an enclosing focus transaction. Shutdown or
    /// unavailability may strengthen the remaining unwind by suppressing restoration.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="Exception">A pointer-reconciliation, focus, or exit callback fails after complete unwind.</exception>
    public void Dispose()
    {
        _manager.Exit(this);
        GC.SuppressFinalize(this);
    }

    /// <summary>Gets one included root by its valid zero-based insertion position.</summary>
    /// <param name="index">The valid zero-based position.</param>
    /// <returns>The retained plane root.</returns>
    internal Control RootAt(int index) => _roots[index];

    /// <summary>Appends one already validated root without publishing callbacks.</summary>
    /// <param name="root">The validated root.</param>
    internal void AddRoot(Control root)
    {
        Debug.Assert(root is not null, "A modal plane includes concrete roots.");
        _roots.Add(root);
    }

    /// <summary>Returns the plane root containing one target, or null when the target is outside.</summary>
    /// <param name="control">The non-null target.</param>
    /// <returns>The first insertion-ordered containing root, or null.</returns>
    internal Control? BoundaryFor(Control control)
    {
        Debug.Assert(control is not null, "Modal membership requires a concrete control.");

        foreach (var root in _roots)
        {
            if (ModalityManager.IsWithin(control, root))
            {
                return root;
            }
        }

        return null;
    }

    /// <summary>Removes secondary roots contained by one unavailable subtree.</summary>
    /// <param name="subtree">The non-null unavailable subtree.</param>
    internal void RemoveSecondaryRootsWithin(Control subtree)
    {
        Debug.Assert(subtree is not null, "Unavailable cleanup requires a concrete subtree.");

        for (var index = _roots.Count - 1; index > 0; index--)
        {
            if (ModalityManager.IsWithin(_roots[index], subtree))
            {
                _roots.RemoveAt(index);
            }
        }
    }

    /// <summary>Publishes one dismissal request when this active scope opted into dismissal.</summary>
    internal void PublishDismissRequested()
    {
        if (IsActive && OutsideInteraction == OutsideInteraction.Dismiss)
        {
            DismissRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Publishes the single committed exit and releases retained event handlers.</summary>
    internal void PublishExited()
    {
        Debug.Assert(!IsActive, "Exit publication follows the inactive-state commit.");
        var handlers = Exited?.GetInvocationList();
        ExceptionDispatchInfo? failure = null;

        try
        {
            if (handlers is not null)
            {
                foreach (var value in handlers)
                {
                    try
                    {
                        ((EventHandler) value).Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception exception)
                    {
                        failure ??= ExceptionDispatchInfo.Capture(exception);
                    }
                }
            }
        }
        finally
        {
            DismissRequested = null;
            Exited = null;
        }

        failure?.Throw();
    }

    #endregion
}
