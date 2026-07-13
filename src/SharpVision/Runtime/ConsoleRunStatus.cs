// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Reports how an interactive console run completed.</summary>
public enum ConsoleRunStatus
{
    /// <summary>Standard input or output was redirected, so no application started.</summary>
    Redirected,

    /// <summary>The application started and shut down without a primary failure.</summary>
    Completed,

    /// <summary>The caller or host requested shutdown, typically through Ctrl+C.</summary>
    Cancelled,

    /// <summary>The application reported a primary runtime failure.</summary>
    Failed,
}
