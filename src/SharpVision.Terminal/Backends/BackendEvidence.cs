// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Backends;

/// <summary>Records redacted typed evidence for one recognized terminal-backend family.</summary>
internal readonly record struct BackendEvidence
{
    /// <summary>Initializes one redacted backend-evidence value.</summary>
    /// <param name="kind">The recognized terminal-backend family.</param>
    /// <param name="origin">The trusted boundary that recognized <paramref name="kind"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind"/> or <paramref name="origin"/> is undefined.
    /// </exception>
    public BackendEvidence(TerminalBackendKind kind, BackendEvidenceOrigin origin)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(kind, nameof(kind), "The terminal backend kind is unknown.");

        ArgumentOutOfRangeException.ThrowIfNotDefined(origin, nameof(origin), "The backend evidence origin is unknown.");

        Kind = kind;
        Origin = origin;
    }

    /// <summary>Gets the recognized terminal-backend family.</summary>
    public TerminalBackendKind Kind { get; }

    /// <summary>Gets the trusted boundary that recognized the family.</summary>
    public BackendEvidenceOrigin Origin { get; }
}
