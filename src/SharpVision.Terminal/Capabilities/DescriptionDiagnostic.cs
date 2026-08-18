// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Records one structured, non-sensitive terminal-description diagnostic.</summary>
[PublicAPI]
public readonly record struct DescriptionDiagnostic
{
    /// <summary>Initializes one diagnostic without retaining capability bytes or environment values.</summary>
    /// <param name="code">The stable diagnostic category.</param>
    /// <param name="capability">The optional exact allowlisted capability name.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="code"/> is undefined.</exception>
    internal DescriptionDiagnostic(DescriptionDiagnosticCode code, string? capability = null)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(code, nameof(code), "The description diagnostic code is unknown.");

        Code = code;
        Capability = capability;
    }

    /// <summary>Gets the stable diagnostic category.</summary>
    public DescriptionDiagnosticCode Code { get; }

    /// <summary>Gets the exact allowlisted capability name, when applicable.</summary>
    public string? Capability { get; }
}
