// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty;

/// <summary>Identifies Kitty image transmission media.</summary>
[PublicAPI]
public enum Medium
{
    /// <summary>Base64 data carried directly inside APC commands.</summary>
    Direct,

    /// <summary>Terminal-directed ordinary file access; intentionally unsupported.</summary>
    File,

    /// <summary>Terminal-directed temporary-file access; intentionally unsupported.</summary>
    TemporaryFile,

    /// <summary>Terminal-directed named shared memory; intentionally unsupported.</summary>
    SharedMemory
}
