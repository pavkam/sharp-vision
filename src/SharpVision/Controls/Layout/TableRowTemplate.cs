// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Builds one detached <see cref="TableRow"/> for a progressively loaded item.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="item">The loaded item.</param>
/// <returns>A non-null detached row with exactly one cell per owning table column.</returns>
/// <remarks>Invoked only on the owning table's dispatcher thread.</remarks>
[PublicAPI]
public delegate TableRow TableRowTemplate<in T>(T item);
