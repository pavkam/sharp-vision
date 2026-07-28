// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Backends;

/// <summary>Represents the conservative VT terminal backend.</summary>
internal class VtBackend: TerminalBackend
{
    /// <summary>Gets the shared conservative VT backend.</summary>
    public static VtBackend Instance { get; } = new();

    /// <summary>Initializes the standard VT backend.</summary>
    public VtBackend()
        : this(TerminalBackendKind.Vt, "VT")
    {
    }

    /// <summary>Initializes a derived backend that retains the VT protocol foundation.</summary>
    /// <param name="kind">The known derived terminal-emulator family.</param>
    /// <param name="name">The non-blank derived backend display name.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is unknown.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank or consists only of whitespace.</exception>
    protected VtBackend(TerminalBackendKind kind, string name)
        : base(kind, name)
    {
    }
}
