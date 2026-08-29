// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Identifies a mutable control property that a retained owner may temporarily impose.</summary>
internal enum RetainedControlProperty
{
    /// <summary>The requested layout width.</summary>
    Width,

    /// <summary>The requested layout height.</summary>
    Height,

    /// <summary>The local visibility state.</summary>
    Visibility,

    /// <summary>The local keyboard-focus policy.</summary>
    IsFocusable,

    /// <summary>The local tab-traversal policy.</summary>
    IsTabStop,
}
