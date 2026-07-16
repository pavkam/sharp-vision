// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Runtime.ExceptionServices;


/// <summary>Owns transactional keyboard focus within one attached control tree.</summary>
public sealed class FocusManager: IDisposable
{
    #region Construction and state

    /// <summary>Initializes focus ownership for one attached root.</summary>
    /// <param name="root">The non-null attached tree root.</param>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The root is detached or already belongs to a focus manager.
    /// </exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The root is disposed.</exception>
    public FocusManager(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        root.VerifyMutable();

        if (root.Dispatcher is null)
        {
            throw new ArgumentException("The focus root must be attached.", nameof(root));
        }

        if (root.FocusOwner is not null)
        {
            throw new ArgumentException("The root already belongs to a focus manager.", nameof(root));
        }

        Root = root;
        root.SetFocusOwner(this);
    }

    /// <summary>Raised before an explicit focus request commits.</summary>
    public event EventHandler<FocusChangingEventArgs>? Changing;

    /// <summary>Raised after the manager commits away from a previous control.</summary>
    public event EventHandler<FocusChangedEventArgs>? Lost;

    /// <summary>Raised after the manager commits to a current control.</summary>
    public event EventHandler<FocusChangedEventArgs>? Gained;

    /// <summary>Gets the owned attached tree root.</summary>
    public Control Root { get; }

    /// <summary>Gets the currently focused control, or null.</summary>
    public Control? Focused { get; private set; }

    private bool IsChanging { get; set; }

    private bool CleanupPending { get; set; }

    private List<Control>? EligibilityNotificationsPending { get; set; }

    private bool IsDisposed { get; set; }

    #endregion

    #region Focus operations

    /// <summary>Requests focus for one member, or releases focus with null.</summary>
    /// <param name="control">The requested member, or null.</param>
    /// <returns>True when focus is committed or already matches; false when ineligible or cancelled.</returns>
    /// <exception cref="ArgumentException">The control is not a member of this tree.</exception>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher or focus is reentered.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    public bool Focus(Control? control)
    {
        VerifyAccess();
        return ValidateTarget(control) && Change(control, cancellable: true);
    }

    /// <summary>Moves focus through tab index then tree order with wrapping.</summary>
    /// <param name="reverse">Whether to traverse backward.</param>
    /// <returns>True when an eligible target exists and accepts focus.</returns>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The manager is disposed.</exception>
    public bool MoveNext(bool reverse = false)
    {
        VerifyAccess();
        var scope = FindScope(Focused);
        var candidates = new List<(Control Control, int Order)>();
        var order = 0;

        Collect(scope, candidates, ref order, scope);
        candidates.Sort(static (left, right) =>
        {
            var tab = left.Control.TabIndex.CompareTo(right.Control.TabIndex);
            return tab != 0 ? tab : left.Order.CompareTo(right.Order);
        });

        if (candidates.Count == 0)
        {
            return false;
        }

        var current = candidates.FindIndex(item => ReferenceEquals(item.Control, Focused));
        var next = reverse
            ? (current <= 0 ? candidates.Count - 1 : current - 1)
            : (current < 0 || current == candidates.Count - 1 ? 0 : current + 1);
        return Focus(candidates[next].Control);
    }

    #endregion

    #region Cleanup

    /// <summary>Releases focus ownership and all manager references.</summary>
    /// <exception cref="InvalidOperationException">The caller is off-dispatcher.</exception>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        Root.VerifyMutable();
        Focused?.SetFocused(false);
        Focused = null;
        Root.SetFocusOwner(null);
        Changing = null;
        Lost = null;
        Gained = null;
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases focus when a subtree becomes invalid.</summary>
    internal void Unavailable(Control subtree)
    {
        ArgumentNullException.ThrowIfNull(subtree);
        Debug.Assert(IsMember(subtree), "Unavailable subtrees belong to the focus root.");

        if (Focused is null || !IsWithin(Focused, subtree))
        {
            return;
        }

        if (IsChanging)
        {
            CleanupPending = true;
            return;
        }

        _ = Change(null, cancellable: false);
    }

    /// <summary>Releases focus when the focused control loses its own eligibility.</summary>
    /// <param name="control">The non-null owned control whose eligibility changed.</param>
    /// <returns>Whether cleanup completed synchronously and notification may publish.</returns>
    internal bool Ineligible(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        Debug.Assert(IsMember(control), "Ineligible controls belong to the focus root.");

        if (!ReferenceEquals(Focused, control))
        {
            return true;
        }

        if (IsChanging)
        {
            CleanupPending = true;
            (EligibilityNotificationsPending ??= []).Add(control);
            return false;
        }

        _ = Change(null, cancellable: false);
        return true;
    }

    /// <summary>Severs manager ownership while the root is being disposed.</summary>
    internal void RootDisposed()
    {
        Debug.Assert(ReferenceEquals(Root.FocusOwner, this), "Root disposal severs this manager's ownership.");

        Root.SetFocusOwner(null);
        Changing = null;
        Lost = null;
        Gained = null;
        IsDisposed = true;
    }

    #endregion

    #region Focus transaction

    private bool Change(Control? control, bool cancellable)
    {
        Debug.Assert(control is null || IsMember(control), "Focus transactions target only owned controls.");

        if (IsChanging)
        {
            throw new InvalidOperationException("Focus cannot be changed reentrantly.");
        }

        if (ReferenceEquals(Focused, control))
        {
            return true;
        }

        IsChanging = true;

        try
        {
            var preview = new FocusChangingEventArgs(Focused, control);

            if (cancellable)
            {
                Changing?.Invoke(this, preview);
            }

            // Preview handlers may detach, hide, disable, or dispose the target.
            // Revalidate after notification and before committing any focus flag.
            if (preview.Cancel || (control is not null && !IsEligible(control)))
            {
                return false;
            }

            var previous = Focused;
            Focused = control;
            previous?.SetFocused(false);
            control?.SetFocused(true);
            var changed = new FocusChangedEventArgs(previous, control);

            if (previous is not null)
            {
                Lost?.Invoke(this, changed);
            }

            if (control is not null)
            {
                Gained?.Invoke(this, changed);
            }

            return true;
        }
        finally
        {
            IsChanging = false;

            if (CleanupPending)
            {
                // Cleanup requested during preview/notification cannot recurse
                // until the outer transaction has released its reentrancy guard.
                CleanupPending = false;

                if (Focused is not null && !IsEligible(Focused))
                {
                    _ = Change(null, cancellable: false);
                }
            }

            PublishEligibilityNotifications();
        }
    }

    private void PublishEligibilityNotifications()
    {
        if (EligibilityNotificationsPending is not { } pending)
        {
            return;
        }

        EligibilityNotificationsPending = null;
        var failure = (ExceptionDispatchInfo?) null;

        foreach (var control in pending)
        {
            try
            {
                control.PublishDeferredCanFocusChange();
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        failure?.Throw();
    }

    #endregion

    #region Target resolution

    private void Collect(
        Control control,
        List<(Control Control, int Order)> candidates,
        ref int order,
        Control scope)
    {
        Debug.Assert(control is not null, "Focus traversal visits a concrete control.");
        Debug.Assert(candidates is not null, "Focus traversal accumulates into an owned candidate list.");
        Debug.Assert(order >= 0, "Focus traversal order is non-negative.");

        if (!ReferenceEquals(control, scope) && IsEligible(control) && control.IsTabStop)
        {
            candidates.Add((control, order));
        }

        order++;

        if (!ReferenceEquals(control, scope) && control.TabNavigation != TabNavigation.Continue)
        {
            CollectFirstEligible(control, candidates, order);
            return;
        }

        var count = control.NavigationCount;

        for (var index = 0; index < count; index++)
        {
            Collect(control.NavigationAt(index), candidates, ref order, scope);
        }
    }

    private void CollectFirstEligible(Control childScope, List<(Control Control, int Order)> candidates, int order)
    {
        Debug.Assert(childScope is not null, "Child scope entry requires a concrete scope root.");
        Debug.Assert(childScope.TabNavigation != TabNavigation.Continue, "Child scope entry targets a non-Continue scope.");

        var inner = new List<(Control Control, int Order)>();
        var innerOrder = 0;
        Collect(childScope, inner, ref innerOrder, childScope);

        if (inner.Count == 0)
        {
            return;
        }

        inner.Sort(static (left, right) =>
        {
            var tab = left.Control.TabIndex.CompareTo(right.Control.TabIndex);
            return tab != 0 ? tab : left.Order.CompareTo(right.Order);
        });
        candidates.Add((inner[0].Control, order));
    }

    private Control FindScope(Control? focused)
    {
        for (var current = focused; current is not null; current = current.Parent)
        {
            if (current.TabNavigation != TabNavigation.Continue)
            {
                return current;
            }
        }

        return Root;
    }

    private bool IsEligible(Control control) =>
        IsMember(control) && !control.IsDisposed && control.Dispatcher is not null &&
        control.CanFocus && control.EffectiveIsVisible && control.EffectiveIsEnabled;

    private bool ValidateTarget(Control? control)
        => control switch
        {
            null => true,
            _ when !IsMember(control) => throw new ArgumentException(
                "The focus target does not belong to this tree.",
                nameof(control)),
            _ => IsEligible(control),
        };

    private bool IsMember(Control control)
    {
        Debug.Assert(control is not null, "Focus membership requires a control instance.");

        for (var current = control; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, Root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithin(Control control, Control subtree)
    {
        Debug.Assert(control is not null, "Focus containment requires a control instance.");
        Debug.Assert(subtree is not null, "Focus containment requires a subtree root.");

        for (var current = control; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, subtree))
            {
                return true;
            }
        }

        return false;
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        Root.VerifyMutable();
        Debug.Assert(ReferenceEquals(Root.FocusOwner, this), "A live manager remains registered on its root.");
    }

    #endregion
}
