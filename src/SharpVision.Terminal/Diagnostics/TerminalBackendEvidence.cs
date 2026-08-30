// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Diagnostics;

/// <summary>Records one typed, redacted backend-identity observation.</summary>
[PublicAPI]
public readonly record struct TerminalBackendEvidence
{
    /// <summary>Initializes one backend-identity observation.</summary>
    /// <param name="family">The recognized terminal family.</param>
    /// <param name="source">The source that recognized the family.</param>
    /// <exception cref="ArgumentOutOfRangeException">An enum value is undefined.</exception>
    public TerminalBackendEvidence(
        TerminalBackendFamily family,
        TerminalBackendEvidenceSource source)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(family, nameof(family), "The terminal backend family is unknown.");
        ArgumentOutOfRangeException.ThrowIfNotDefined(source, nameof(source), "The backend evidence source is unknown.");
        Family = family;
        Source = source;
    }

    /// <summary>Gets the recognized terminal family.</summary>
    public TerminalBackendFamily Family { get; }

    /// <summary>Gets the redacted source that recognized the family.</summary>
    public TerminalBackendEvidenceSource Source { get; }
}
