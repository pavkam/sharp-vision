// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Threading;

/// <summary>Provides one finite dispatcher work item.</summary>
internal abstract class Work
{
    /// <summary>Executes the work item on the owning dispatcher thread.</summary>
    internal abstract void Execute();

    /// <summary>Cancels the work item before execution.</summary>
    internal virtual void Cancel()
    {
    }
}
