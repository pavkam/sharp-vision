// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Windows;

/// <summary>Defines one immutable built-in Windows VT program source.</summary>
internal sealed class WindowsVtProgramSpec
{
    /// <summary>Initializes one validated built-in program specification.</summary>
    /// <param name="name">The non-blank ordinal program identifier.</param>
    /// <param name="source">The non-empty raw terminal source represented as immutable text.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An argument is empty or whitespace-only where prohibited.</exception>
    public WindowsVtProgramSpec(string name, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrEmpty(source);

        Name = name;
        Source = source;
    }

    /// <summary>Gets the exact ordinal program identifier.</summary>
    public string Name { get; }

    /// <summary>Gets the immutable raw terminal source text.</summary>
    public string Source { get; }
}
