// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Signals the terminal alert.</summary>
public interface IBell
{
    /// <summary>Requests an audible bell, ordered with frame output and never mid-frame.</summary>
    public void Ring();
}
