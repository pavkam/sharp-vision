// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Support;

/// <summary>Represents one copied parser callback for deterministic comparisons.</summary>
public sealed record Observation
{
    /// <summary>Initializes one validated copied parser observation.</summary>
    public Observation(string type, byte[] first, byte[] second, byte final, Diagnostic? diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        Type = type;
        First = first;
        Second = second;
        Final = final;
        Diagnostic = diagnostic;
    }

    /// <summary>Gets the callback type.</summary>
    public string Type { get; }

    /// <summary>Gets the copied first byte field.</summary>
    public byte[] First { get; }

    /// <summary>Gets the copied second byte field.</summary>
    public byte[] Second { get; }

    /// <summary>Gets the sequence final byte.</summary>
    public byte Final { get; }

    /// <summary>Gets the structured diagnostic, when present.</summary>
    public Diagnostic? Diagnostic { get; }
}
