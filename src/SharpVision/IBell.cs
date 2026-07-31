// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

/// <summary>Signals the terminal alert.</summary>
[PublicAPI]
public interface IBell
{
    /// <summary>Gets whether the active description supplies an exact alert program.</summary>
    public bool IsSupported { get; }

    /// <summary>Requests an audible bell, ordered with frame output and never mid-frame.</summary>
    /// <remarks>An unsupported alert is a byte-quiet no-op.</remarks>
    public void Ring();
}
