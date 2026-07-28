// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Backends;

/// <summary>Publishes one immutable terminal-backend selection and its redacted ordered evidence.</summary>
internal sealed class BackendResolution
{
    private readonly ReadOnlyCollection<BackendEvidence> _evidence;

    /// <summary>Initializes an immutable terminal-backend resolution from owned snapshots.</summary>
    /// <param name="backend">The selected non-null canonical backend.</param>
    /// <param name="evidence">The non-null ordered evidence to copy before publication.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="backend"/> or <paramref name="evidence"/> is <see langword="null"/>.
    /// </exception>
    public BackendResolution(TerminalBackend backend, IEnumerable<BackendEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(evidence);

        var snapshot = evidence.ToArray();

        Backend = backend;
        _evidence = Array.AsReadOnly(snapshot);
    }

    /// <summary>Gets the selected canonical terminal backend.</summary>
    public TerminalBackend Backend { get; }

    /// <summary>Gets the immutable ordered redacted identity evidence.</summary>
    public IReadOnlyList<BackendEvidence> Evidence => _evidence;
}
