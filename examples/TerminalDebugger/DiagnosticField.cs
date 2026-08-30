// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Stores one name/value pair in a structured diagnostic record.</summary>
internal readonly record struct DiagnosticField
{
    /// <summary>Initializes one diagnostic field.</summary>
    /// <param name="name">The non-empty field label.</param>
    /// <param name="value">The non-null formatted value.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal DiagnosticField(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        Name = name;
        Value = value;
    }

    /// <summary>Gets the field label.</summary>
    internal string Name { get; }

    /// <summary>Gets the formatted value.</summary>
    internal string Value { get; }
}
