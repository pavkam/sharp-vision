// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Backends;

/// <summary>Represents the iTerm2 terminal backend.</summary>
internal sealed class ItermBackend: XtermBackend
{
    /// <summary>Gets the shared iTerm2 backend.</summary>
    public static new ItermBackend Instance { get; } = new();

    /// <summary>Initializes the iTerm2 backend.</summary>
    private ItermBackend()
        : base(TerminalBackendKind.Iterm2, "iTerm2")
    {
    }

    /// <summary>Adds the iTerm2 extension after inherited VT and xterm extensions.</summary>
    /// <param name="extensions">The mutable ordered collection receiving inherited and local descriptors.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is <see langword="null"/>.</exception>
    protected override void AddExtensions(ICollection<ProtocolExtension> extensions)
    {
        base.AddExtensions(extensions);
        extensions.Add(new ProtocolExtension(ProtocolExtensionKind.Iterm2));
    }
}
