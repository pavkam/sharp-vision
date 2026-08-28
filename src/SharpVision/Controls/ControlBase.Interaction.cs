// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

/// <content>Stores the committed local interaction facts owned by each control.</content>
public abstract partial class ControlBase
{
    // Focus and input managers decide transitions. ControlBase stores only their committed local
    // facts so it remains the single publication and rendering authority without allocating a
    // second state holder for every control.
    /// <summary>Gets whether this control currently owns keyboard focus.</summary>
    public bool IsFocused { get; private set; }

    /// <summary>Gets whether the pointer is over this control or one of its descendants.</summary>
    public bool IsPointerOver { get; private set; }

    /// <summary>Gets whether the pointer directly targets this control.</summary>
    public bool IsPointerDirectlyOver { get; private set; }

    /// <summary>Gets whether an active pointer press began on this control.</summary>
    public bool IsPressed { get; private set; }

    private bool IsSelectedFact { get; set; }

    private bool IsCurrentFact { get; set; }
    private List<IControlLifecycleParticipant>? _lifecycleParticipants;

    /// <summary>Registers one control-owned interaction participant for lifecycle cancellation.</summary>
    /// <param name="participant">The non-null participant retained until this control is disposed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="participant"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The participant is already registered, or an
    /// attached control is mutated off its dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    internal void RegisterLifecycleParticipant(IControlLifecycleParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        VerifyMutable();
        _lifecycleParticipants ??= [];

        if (_lifecycleParticipants.Exists(candidate => ReferenceEquals(candidate, participant)))
        {
            throw new InvalidOperationException("The interaction lifecycle participant is already registered.");
        }

        _lifecycleParticipants.Add(participant);
    }

    private void NotifyLifecycleFocusChanged(bool focused)
    {
        ExceptionDispatchInfo? failure = null;

        foreach (var participant in SnapshotLifecycleParticipants())
        {
            ExceptionAggregation.Capture(() => participant.FocusChanged(focused), ref failure);

            if (IsDisposed)
            {
                break;
            }
        }

        failure?.Throw();
    }

    private void NotifyLifecycleCaptureLost(PointerCaptureLossReason reason)
    {
        ExceptionDispatchInfo? failure = null;

        foreach (var participant in SnapshotLifecycleParticipants())
        {
            ExceptionAggregation.Capture(() => participant.CaptureLost(reason), ref failure);

            if (IsDisposed)
            {
                break;
            }
        }

        failure?.Throw();
    }

    private void NotifyLifecycleUnavailable(ReleaseReason reason)
    {
        ExceptionDispatchInfo? failure = null;

        foreach (var participant in SnapshotLifecycleParticipants())
        {
            ExceptionAggregation.Capture(() => participant.Unavailable(reason), ref failure);

            if (IsDisposed)
            {
                break;
            }
        }

        failure?.Throw();
    }

    private IControlLifecycleParticipant[] SnapshotLifecycleParticipants() =>
        _lifecycleParticipants?.ToArray() ?? [];

    private void ClearLifecycleParticipants()
    {
        _lifecycleParticipants?.Clear();
        _lifecycleParticipants = null;
    }

    private bool CommitFocusedFact(bool value)
    {
        if (IsFocused == value)
        {
            return false;
        }

        IsFocused = value;
        return true;
    }

    private bool CommitPointerOverFacts(bool value, bool directlyOver, out bool wasOver)
    {
        wasOver = IsPointerOver;

        if (wasOver == value && IsPointerDirectlyOver == directlyOver)
        {
            return false;
        }

        IsPointerOver = value;
        IsPointerDirectlyOver = directlyOver;
        return true;
    }

    private bool CommitPressedFact(bool value)
    {
        if (IsPressed == value)
        {
            return false;
        }

        IsPressed = value;
        return true;
    }

    private bool CommitSelectedFact(bool value)
    {
        if (IsSelectedFact == value)
        {
            return false;
        }

        IsSelectedFact = value;
        return true;
    }

    private bool CommitCurrentFact(bool value)
    {
        if (IsCurrentFact == value)
        {
            return false;
        }

        IsCurrentFact = value;
        return true;
    }
}
