// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Describes one child an <see cref="ITreeViewChildSource"/> reports for an expanding item.</summary>
[PublicAPI]
public sealed class TreeViewChildDescription
{
    /// <summary>Initializes an immutable child description.</summary>
    /// <param name="key">The non-null key identifying this child, stable across reloads of the same parent.</param>
    /// <param name="header">The non-null display text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="header"/> is null.</exception>
    public TreeViewChildDescription(object key, string header)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(header);
        Key = key;
        Header = header;
    }

    /// <summary>Gets the key identifying this child. Must stay stable across reloads of the same
    /// parent so a repeated key reuses the same materialized instance instead of a fresh one.</summary>
    public object Key { get; }

    /// <summary>Gets the display text.</summary>
    public string Header { get; }

    /// <summary>Gets or initializes whether the materialized child displays and responds to a check mark.</summary>
    public bool IsCheckable { get; init; }

    /// <summary>
    /// Gets or initializes the initial check state applied when this child is first materialized.
    /// Consulted only when <see cref="IsCheckable"/> is true.
    /// </summary>
    public bool? InitialCheckState { get; init; }

    /// <summary>
    /// Gets or initializes whether the materialized child may itself have children. Defaults to
    /// <see cref="TreeViewChildPresence.MayHaveChildren"/> - a source must opt in to
    /// <see cref="TreeViewChildPresence.Leaf"/> rather than have it inferred from an empty answer.
    /// </summary>
    public TreeViewChildPresence Presence { get; init; } = TreeViewChildPresence.MayHaveChildren;
}
