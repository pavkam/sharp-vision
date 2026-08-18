// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Answers one <see cref="TableDataRequest"/> with the items loaded at exactly the requested start.</summary>
/// <typeparam name="T">The item type.</typeparam>
[PublicAPI]
public sealed class TableDataResult<T>
{
    /// <summary>Gets the items loaded starting at exactly the requested
    /// <see cref="TableDataRequest.StartIndex"/>, with no gap.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Gets whether no further items exist beyond the end of <see cref="Items"/>.</summary>
    public bool IsEndOfData { get; init; }
}
