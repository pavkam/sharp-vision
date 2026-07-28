// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty;

/// <summary>Identifies how a direct Kitty enhancement command applies flags.</summary>
[PublicAPI]
public enum KittyEnhancementMode
{
    /// <summary>Replace all flags with the supplied set.</summary>
    Replace = 1,

    /// <summary>Set supplied flags and leave other flags unchanged.</summary>
    Set = 2,

    /// <summary>Clear supplied flags and leave other flags unchanged.</summary>
    Clear = 3
}
