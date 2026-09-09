// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Exposes one status bar's constrained item collection.</summary>
[PublicAPI]
public sealed class StatusBarItemCollection: ItemCollection<StatusBarItem>
{
    private readonly StatusBar _owner;

    /// <summary>Initializes a typed view over one non-null status bar.</summary>
    /// <param name="owner">The owning status bar.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal StatusBarItemCollection(StatusBar owner)
        : base(owner) =>
        _owner = owner;

    /// <summary>Moves one owned item to a different position, preserving its identity.</summary>
    /// <remarks>
    /// The status bar keeps its own range validation, with a status-bar-specific exception
    /// message, rather than the base class's generic accessor-level validation.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the current items.
    /// </exception>
    /// <exception cref="InvalidOperationException">The attached bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The bar is disposed.</exception>
    public override void Move(int oldIndex, int newIndex) => _owner.MoveItem(oldIndex, newIndex);
}
