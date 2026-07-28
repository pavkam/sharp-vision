// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Creates one detached owned control for a ListView item.</summary>
/// <param name="item">The borrowed item value, which may be null.</param>
/// <returns>A non-null detached undisposed control transferred to the ListView on success.</returns>
[PublicAPI]
public delegate Control ItemTemplate(object? item);
