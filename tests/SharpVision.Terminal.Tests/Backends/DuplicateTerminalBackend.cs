// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Backends;

using SharpVision.Terminal.Backends;

/// <summary>Contributes a duplicate VT family to prove backend composition rejects duplicate ownership.</summary>
internal sealed class DuplicateTerminalBackend: VtBackend
{
    /// <summary>Initializes the intentionally invalid backend composition.</summary>
    internal DuplicateTerminalBackend()
        : base(TerminalBackendKind.Vt, "Duplicate VT")
    {
    }

    /// <summary>Initializes an intentionally invalid backend identity for constructor-validation tests.</summary>
    /// <param name="kind">The deliberately supplied terminal-emulator family.</param>
    /// <param name="name">The deliberately supplied backend display name.</param>
    internal DuplicateTerminalBackend(TerminalBackendKind kind, string name)
        : base(kind, name)
    {
    }

    /// <summary>Adds a second VT family after the inherited baseline contribution.</summary>
    /// <param name="extensions">The mutable ordered collection receiving inherited and local descriptors.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is <see langword="null"/>.</exception>
    protected override void AddExtensions(ICollection<ProtocolExtension> extensions)
    {
        base.AddExtensions(extensions);
        extensions.Add(new ProtocolExtension(ProtocolExtensionKind.Vt));
    }
}
