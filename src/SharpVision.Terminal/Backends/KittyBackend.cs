// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Backends;

/// <summary>Represents the Kitty terminal backend.</summary>
internal sealed class KittyBackend: XtermBackend
{
    /// <summary>Gets the shared Kitty backend.</summary>
    public static new KittyBackend Instance { get; } = new();

    /// <summary>Initializes the Kitty backend.</summary>
    private KittyBackend()
        : base(TerminalBackendKind.Kitty, "Kitty")
    {
    }

    /// <summary>Adds the Kitty extension after inherited VT and xterm extensions.</summary>
    /// <param name="extensions">The mutable ordered collection receiving inherited and local descriptors.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is <see langword="null"/>.</exception>
    protected override void AddExtensions(ICollection<ProtocolExtension> extensions)
    {
        base.AddExtensions(extensions);
        extensions.Add(new ProtocolExtension(ProtocolExtensionKind.Kitty));
    }
}
