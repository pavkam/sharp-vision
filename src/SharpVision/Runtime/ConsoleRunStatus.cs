// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Reports how an interactive console run completed.</summary>
[PublicAPI]
public enum ConsoleRunStatus
{
    /// <summary>Standard input or output was redirected, so no application started.</summary>
    Redirected = 0,

    /// <summary>The application started and shut down without a primary failure.</summary>
    Completed = 1,

    /// <summary>The caller or host requested shutdown, typically through Ctrl+C.</summary>
    Cancelled = 2,

    /// <summary>The application reported a primary runtime failure.</summary>
    Failed = 3,

    /// <summary>The interactive console has no terminal description suitable for full-screen use.</summary>
    UnsupportedTerminal = 4
}
