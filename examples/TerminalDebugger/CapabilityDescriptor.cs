// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Defines presentation metadata for one terminal protocol capability.</summary>
internal readonly record struct CapabilityDescriptor
{
    /// <summary>Initializes one validated capability descriptor.</summary>
    /// <param name="protocol">The represented terminal protocol.</param>
    /// <param name="group">The non-empty dashboard group.</param>
    /// <param name="label">The non-empty display label.</param>
    /// <param name="explanation">The non-empty plain-language explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="protocol"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A text value is empty.</exception>
    internal CapabilityDescriptor(
        TerminalProtocol protocol,
        string group,
        string label,
        string explanation)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(protocol);
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);

        Protocol = protocol;
        Group = group;
        Label = label;
        Explanation = explanation;
    }

    /// <summary>Gets the represented terminal protocol.</summary>
    internal TerminalProtocol Protocol { get; }

    /// <summary>Gets the dashboard group.</summary>
    internal string Group { get; }

    /// <summary>Gets the concise display label.</summary>
    internal string Label { get; }

    /// <summary>Gets the plain-language protocol explanation.</summary>
    internal string Explanation { get; }
}
