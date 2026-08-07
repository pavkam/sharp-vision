// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Selects the growth direction of a fractional block bar.</summary>
[PublicAPI]
public enum BarDirection
{
    /// <summary>The bar grows from its origin cell toward smaller rows.</summary>
    Up,

    /// <summary>The bar grows from its origin cell toward larger rows.</summary>
    Down,

    /// <summary>The bar grows from its origin cell toward smaller columns.</summary>
    Left,

    /// <summary>The bar grows from its origin cell toward larger columns.</summary>
    Right
}
