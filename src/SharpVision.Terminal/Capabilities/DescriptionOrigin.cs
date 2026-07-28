// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies the trusted source of terminal-description metadata.</summary>
[PublicAPI]
public enum DescriptionOrigin
{
    /// <summary>The library supplied a built-in terminal description.</summary>
    BuiltIn,

    /// <summary>A validated terminal-description database supplied the metadata.</summary>
    Database,

    /// <summary>The caller supplied a complete explicit terminal description.</summary>
    Explicit
}
