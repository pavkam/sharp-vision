// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Binds one ListView activation to the popup session it began within.</summary>
internal readonly struct PopupItemActivationIdentity
{
    /// <summary>Initializes an immutable item and popup identity.</summary>
    /// <param name="itemGeneration">The ListView-owned activation generation.</param>
    /// <param name="itemIndex">The non-negative activated item index.</param>
    /// <param name="popupTransitionVersion">The popup request version at activation start.</param>
    /// <param name="popupSessionGeneration">The popup session identity at activation start.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="itemIndex"/> is negative.</exception>
    internal PopupItemActivationIdentity(
        ulong itemGeneration,
        int itemIndex,
        ulong popupTransitionVersion,
        ulong popupSessionGeneration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemIndex);
        ItemGeneration = itemGeneration;
        ItemIndex = itemIndex;
        PopupTransitionVersion = popupTransitionVersion;
        PopupSessionGeneration = popupSessionGeneration;
    }

    /// <summary>Gets the ListView-owned activation generation.</summary>
    internal ulong ItemGeneration { get; }

    /// <summary>Gets the activated item index.</summary>
    internal int ItemIndex { get; }

    /// <summary>Gets the popup request version captured before activation callbacks.</summary>
    internal ulong PopupTransitionVersion { get; }

    /// <summary>Gets the popup session identity captured before activation callbacks.</summary>
    internal ulong PopupSessionGeneration { get; }
}
