// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace ProcessMonitor;

/// <summary>Identifies which semantic color one status-bar message should render with.</summary>
internal enum StatusSeverity
{
    /// <summary>A neutral, informational message.</summary>
    Info,

    /// <summary>A de-emphasized, low-priority message.</summary>
    Muted,

    /// <summary>A message confirming a successful action.</summary>
    Success,

    /// <summary>A message calling out something the user should notice.</summary>
    Warning,

    /// <summary>A message reporting a failure.</summary>
    Error
}
