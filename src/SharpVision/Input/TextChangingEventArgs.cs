// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.ComponentModel;

using SharpVision.Text;

/// <summary>Provides a cancellable immutable proposed text-edit snapshot.</summary>
public sealed class TextChangingEventArgs: CancelEventArgs
{
    /// <summary>Initializes a proposal that has already passed edit-model validation.</summary>
    /// <param name="proposal">The valid proposed text and selection.</param>
    /// <exception cref="ArgumentException">The proposal is a default invalid snapshot.</exception>
    public TextChangingEventArgs(EditResult proposal)
    {
        if (proposal.Text is null)
        {
            throw new ArgumentException("Proposal must contain owned text.", nameof(proposal));
        }

        Proposal = proposal;
    }

    /// <summary>Gets the immutable proposed state.</summary>
    public EditResult Proposal { get; }
}
