// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Captures one validated suggestion activation and the text it proposes to commit.</summary>
/// <remarks>
/// The transaction generation is independent of resolver generations so accepted text may start
/// a fresh resolution without suppressing the one post-close acceptance notification it caused.
/// </remarks>
internal readonly struct SuggestionInputAcceptanceTransaction
{
    /// <summary>Initializes one immutable acceptance transaction.</summary>
    /// <param name="activation">The non-default list and popup activation identity.</param>
    /// <param name="item">The borrowed suggestion item, which may be null.</param>
    /// <param name="acceptedText">The non-null text resolved before popup mutation begins.</param>
    /// <param name="generation">The nonzero owner-issued acceptance generation.</param>
    /// <exception cref="ArgumentException"><paramref name="activation"/> is the default identity.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="acceptedText"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="generation"/> is zero.</exception>
    internal SuggestionInputAcceptanceTransaction(
        PopupItemActivationIdentity activation,
        object? item,
        string acceptedText,
        ulong generation)
    {
        if (activation.ItemGeneration == 0 ||
            activation.PopupTransitionVersion == 0 ||
            activation.PopupSessionGeneration == 0)
        {
            throw new ArgumentException("The suggestion activation identity must not be default.", nameof(activation));
        }

        ArgumentNullException.ThrowIfNull(acceptedText);
        ArgumentOutOfRangeException.ThrowIfZero(generation);

        Activation = activation;
        Item = item;
        AcceptedText = acceptedText;
        Generation = generation;
    }

    /// <summary>Gets the exact list activation and popup session being accepted.</summary>
    internal PopupItemActivationIdentity Activation { get; }

    /// <summary>Gets the borrowed item whose row produced the activation.</summary>
    internal object? Item { get; }

    /// <summary>Gets the validated text projection evaluated before popup mutation.</summary>
    internal string AcceptedText { get; }

    /// <summary>Gets the owner-issued identity that separates acceptance from resolution.</summary>
    internal ulong Generation { get; }
}
