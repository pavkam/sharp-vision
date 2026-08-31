// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Describes how strongly the current session has exercised one terminal feature.</summary>
internal enum VerificationState
{
    /// <summary>The feature has not been exercised.</summary>
    NotRun,

    /// <summary>A matching passive event has been observed.</summary>
    Observed,

    /// <summary>An automatic comparison passed.</summary>
    Passed,

    /// <summary>An automatic comparison, request, or selected route failed.</summary>
    Failed
}
