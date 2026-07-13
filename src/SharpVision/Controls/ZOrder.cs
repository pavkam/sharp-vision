// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Stores one weakly attached overlay z-order value.</summary>
internal sealed class ZOrder
{
    /// <summary>Gets or sets the signed layer order.</summary>
    internal int Value { get; set; }
}
