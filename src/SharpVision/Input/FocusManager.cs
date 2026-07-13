namespace SharpVision.Input;

using SharpVision.Controls;

/// <summary>Owns transactional keyboard focus within one attached control tree.</summary>
public sealed class FocusManager: IDisposable
{
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

    private bool IsDisposed { get; set; }

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
        var candidates = new List<(Control Control, int Order)>();
        var order = 0;
        Collect(Root, candidates, ref order);
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

    /// <summary>Severs manager ownership while the root is being disposed.</summary>
    internal void RootDisposed()
    {
        Root.SetFocusOwner(null);
        Changing = null;
        Lost = null;
        Gained = null;
        IsDisposed = true;
    }

    private bool Change(Control? control, bool cancellable)
    {
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
                CleanupPending = false;

                if (Focused is not null && !IsEligible(Focused))
                {
                    _ = Change(null, cancellable: false);
                }
            }
        }
    }

    private void Collect(
        Control control,
        List<(Control Control, int Order)> candidates,
        ref int order)
    {
        if (IsEligible(control))
        {
            candidates.Add((control, order));
        }

        order++;

        if (control is Container container)
        {
            for (var index = 0; index < container.NavigationCount; index++)
            {
                Collect(container.NavigationAt(index), candidates, ref order);
            }
        }
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
    }
}
