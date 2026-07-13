// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>Identifies a protocol recovery or safe-degradation condition.</summary>
public enum DiagnosticCode
{
    /// <summary>A sequence violated its byte grammar.</summary>
    Malformed,

    /// <summary>CAN or SUB cancelled an active sequence.</summary>
    Cancelled,

    /// <summary>Input ended while a sequence was incomplete.</summary>
    Truncated,

    /// <summary>Parameter storage reached its configured limit.</summary>
    ParameterLimit,

    /// <summary>Intermediate-byte storage reached its configured limit.</summary>
    IntermediateLimit,

    /// <summary>String payload storage reached its configured limit.</summary>
    StringLimit,

    /// <summary>Base64 input was invalid.</summary>
    InvalidBase64,

    /// <summary>Protocol metadata was malformed or exceeded its limit.</summary>
    InvalidMetadata,

    /// <summary>A transaction received a packet that was invalid for its state.</summary>
    UnexpectedPacket,

    /// <summary>A completed query received another matching response.</summary>
    DuplicateResponse,

    /// <summary>A response arrived after its query deadline.</summary>
    LateResponse,

    /// <summary>The configured concurrent-query limit was reached.</summary>
    QueryLimit,

    /// <summary>A requested optional protocol is unavailable.</summary>
    Unsupported,
}
