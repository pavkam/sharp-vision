// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Backends;

/// <summary>
/// Provides immutable terminal-family identity and lazily assembled protocol-extension metadata.
/// </summary>
internal abstract class TerminalBackend
{
    private readonly Lazy<ReadOnlyCollection<ProtocolExtension>> _extensions;

    /// <summary>Initializes one terminal backend with a known family and display name.</summary>
    /// <param name="kind">The terminal-emulator family.</param>
    /// <param name="name">The non-blank display name used in diagnostics.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is unknown.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank or consists only of whitespace.</exception>
    protected TerminalBackend(TerminalBackendKind kind, string name)
    {
        kind.ValidateDefined(nameof(kind), "The terminal backend kind is unknown.");

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The terminal backend name must not be blank.", nameof(name));
        }

        Kind = kind;
        Name = name;
        _extensions = new Lazy<ReadOnlyCollection<ProtocolExtension>>(AssembleExtensions);
    }

    /// <summary>Gets the terminal-emulator family.</summary>
    public TerminalBackendKind Kind { get; }

    /// <summary>Gets the stable display name used in diagnostics.</summary>
    public string Name { get; }

    /// <summary>Gets the immutable inherited-before-local protocol-extension descriptors.</summary>
    /// <exception cref="InvalidOperationException">The backend contributes one extension family more than once.</exception>
    public IReadOnlyList<ProtocolExtension> Extensions => _extensions.Value;

    /// <summary>Adds the standard VT protocol foundation to an effective backend composition.</summary>
    /// <param name="extensions">The mutable ordered collection receiving inherited and local descriptors.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is <see langword="null"/>.</exception>
    protected virtual void AddExtensions(ICollection<ProtocolExtension> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        extensions.Add(new ProtocolExtension(ProtocolExtensionKind.Vt));
    }

    private ReadOnlyCollection<ProtocolExtension> AssembleExtensions()
    {
        var extensions = new List<ProtocolExtension>();
        AddExtensions(extensions);

        var kinds = new HashSet<ProtocolExtensionKind>();
        foreach (var extension in extensions)
        {
            if (!kinds.Add(extension.Kind))
            {
                throw new InvalidOperationException(
                    $"Terminal backend '{Name}' contributes protocol extension '{extension.Kind}' more than once.");
            }
        }

        return Array.AsReadOnly(extensions.ToArray());
    }
}
