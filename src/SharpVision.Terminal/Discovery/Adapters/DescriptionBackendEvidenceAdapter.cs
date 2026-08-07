// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery.Adapters;

using Backends;

using Capabilities;

/// <summary>Recognizes terminal identity from an owned terminal-description name.</summary>
internal sealed class DescriptionBackendEvidenceAdapter: IBackendEvidenceAdapter
{
    private readonly BackendEvidence? _evidence;

    /// <summary>Snapshots recognized evidence from one non-null terminal description.</summary>
    /// <param name="description">The non-null owned terminal description to inspect.</param>
    /// <exception cref="ArgumentNullException"><paramref name="description"/> is <see langword="null"/>.</exception>
    public DescriptionBackendEvidenceAdapter(Description description)
    {
        ArgumentNullException.ThrowIfNull(description);

        _evidence = Recognize(description.Name);
    }

    /// <inheritdoc/>
    public bool TryAdapt(out BackendEvidence evidence)
    {
        evidence = _evidence.GetValueOrDefault();
        return _evidence.HasValue;
    }

    private static BackendEvidence? Recognize(string name)
    {
        return Contains(name, "kitty")
            ? new BackendEvidence(TerminalBackendKind.Kitty, BackendEvidenceOrigin.Description)
            : Contains(name, "iterm2")
                ? new BackendEvidence(TerminalBackendKind.Iterm2, BackendEvidenceOrigin.Description)
                : Contains(name, "xterm")
                    ? new BackendEvidence(TerminalBackendKind.Xterm, BackendEvidenceOrigin.Description)
                    : null;
    }

    private static bool Contains(string value, string fragment) =>
        value.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}
