// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Reports one committed Pager page transition.</summary>
[PublicAPI]
public sealed class PageChangedEventArgs: EventArgs
{
    /// <summary>Initializes one immutable page transition.</summary>
    /// <param name="previousPageIndex">The zero-based page index before the transition, or -1 for an empty range.</param>
    /// <param name="currentPageIndex">The committed zero-based page index, or -1 for an empty range.</param>
    /// <param name="cause">The input or programmatic cause that committed the transition.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is below -1, or <paramref name="cause"/> is unknown.</exception>
    /// <exception cref="ArgumentException">Both indices are equal and therefore do not describe a transition.</exception>
    public PageChangedEventArgs(
        int previousPageIndex,
        int currentPageIndex,
        ActivationCause cause)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(previousPageIndex, -1);
        ArgumentOutOfRangeException.ThrowIfLessThan(currentPageIndex, -1);
        ArgumentOutOfRangeException.ThrowIfNotDefined(cause);

        if (previousPageIndex == currentPageIndex)
        {
            throw new ArgumentException("A page transition requires different previous and current indices.", nameof(currentPageIndex));
        }

        PreviousPageIndex = previousPageIndex;
        CurrentPageIndex = currentPageIndex;
        Cause = cause;
    }

    /// <summary>Gets the zero-based page index before the transition, or -1 for an empty range.</summary>
    public int PreviousPageIndex { get; }

    /// <summary>Gets the committed zero-based page index, or -1 for an empty range.</summary>
    public int CurrentPageIndex { get; }

    /// <summary>Gets the input or programmatic cause that committed the transition.</summary>
    public ActivationCause Cause { get; }
}
