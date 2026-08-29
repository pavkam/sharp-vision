// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Identifies diagnostic families that may be promoted to exceptions at safe boundaries.</summary>
[Flags]
[PublicAPI]
public enum DiagnosticPromotion
{
    /// <summary>Reports diagnostics without promoting them.</summary>
    None = 0,

    /// <summary>Promotes malformed, truncated, or bounded-recovery input diagnostics.</summary>
    MalformedInput = 1 << 0,

    /// <summary>Promotes an explicitly requested terminal feature that is unavailable.</summary>
    UnsupportedFeature = 1 << 1,

    /// <summary>Promotes replies that conflict with an active or completed transaction.</summary>
    InconsistentReply = 1 << 2,

    /// <summary>Promotes use of a documented safe fallback.</summary>
    Fallback = 1 << 3,

    /// <summary>Promotes a failure observed after bounded cleanup completes.</summary>
    CleanupFailure = 1 << 4,

    /// <summary>Promotes every diagnostic family.</summary>
    All = MalformedInput | UnsupportedFeature | InconsistentReply | Fallback | CleanupFailure
}
