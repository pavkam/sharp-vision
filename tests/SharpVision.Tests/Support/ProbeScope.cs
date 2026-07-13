// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


/// <summary>Provides a third-party-style container that cascades its style to descendants.</summary>
internal sealed class ProbeScope: Container, IStyleScope
{
    /// <summary>Initializes a probe scope with an optional child capacity.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    internal ProbeScope(int capacity = int.MaxValue) : base(capacity)
    {
    }
}
