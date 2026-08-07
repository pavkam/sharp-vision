// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Backends;

/// <summary>Represents the xterm-compatible terminal backend.</summary>
internal class XtermBackend: VtBackend
{
    /// <summary>Gets the shared xterm-compatible backend.</summary>
    public static new XtermBackend Instance { get; } = new();

    /// <summary>Initializes the xterm-compatible backend.</summary>
    public XtermBackend()
        : this(TerminalBackendKind.Xterm, "xterm")
    {
    }

    /// <summary>Initializes a derived backend that retains xterm protocol extensions.</summary>
    /// <param name="kind">The known derived terminal-emulator family.</param>
    /// <param name="name">The non-blank derived backend display name.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is unknown.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank or consists only of whitespace.</exception>
    protected XtermBackend(TerminalBackendKind kind, string name)
        : base(kind, name)
    {
    }

    /// <summary>Adds the xterm extension after the inherited VT foundation.</summary>
    /// <param name="extensions">The mutable ordered collection receiving inherited and local descriptors.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is <see langword="null"/>.</exception>
    protected override void AddExtensions(ICollection<ProtocolExtension> extensions)
    {
        base.AddExtensions(extensions);
        extensions.Add(new ProtocolExtension(ProtocolExtensionKind.Xterm));
    }
}
