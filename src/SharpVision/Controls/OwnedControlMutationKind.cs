// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Identifies the normalized structural operation represented by one committed owned-control change.</summary>
internal enum OwnedControlMutationKind
{
    /// <summary>One control entered an existing ordered snapshot.</summary>
    Insert,

    /// <summary>One control left an ordered snapshot without disposal.</summary>
    Remove,

    /// <summary>One control replaced another at an ordered position.</summary>
    Replace,

    /// <summary>One retained control changed position without changing ownership.</summary>
    Move,

    /// <summary>Every control left a previously non-empty snapshot.</summary>
    Clear,

    /// <summary>A compound snapshot change cannot be represented by a narrower operation.</summary>
    Reset,

    /// <summary>One directly disposing child removed itself from its exact slot.</summary>
    DirectDisposal,
}
