// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

/// <summary>Identifies a key or pointer transition.</summary>
[PublicAPI]
public enum Action
{
    /// <summary>The input became active.</summary>
    Press,

    /// <summary>The active input repeated.</summary>
    Repeat,

    /// <summary>The input became inactive.</summary>
    Release
}
