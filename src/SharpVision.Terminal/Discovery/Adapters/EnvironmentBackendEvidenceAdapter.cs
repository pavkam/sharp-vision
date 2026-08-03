// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery.Adapters;

using Backends;

using Capabilities;

using MultiplexingKind = Multiplexing.MultiplexerKind;
using MultiplexingPolicy = Multiplexing.MultiplexingPolicy;

/// <summary>Recognizes terminal identity from a caller-supplied environment snapshot.</summary>
internal sealed class EnvironmentBackendEvidenceAdapter: IBackendEvidenceAdapter
{
    private readonly BackendEvidence? _evidence;

    /// <summary>Snapshots recognized evidence from TERM and TERM_PROGRAM only.</summary>
    /// <param name="environment">The non-null caller-supplied environment snapshot.</param>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is <see langword="null"/>.</exception>
    public EnvironmentBackendEvidenceAdapter(IReadOnlyDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        _ = environment.TryGetValue(EnvironmentNames.Term, out var term);
        _ = environment.TryGetValue(EnvironmentNames.TermProgram, out var program);

        // Reuse the same authoritative multiplexer detection Policy.Detect already applies
        // elsewhere in this discovery pass (TMUX first, then the TERM prefixes), rather than a
        // second, narrower TERM-only check that disagrees with it on a TMUX=... session whose
        // TERM is left at "xterm-256color" — a common tmux.conf configuration (see #140).
        var isMultiplexer = MultiplexingPolicy.Detect(environment).Kind != MultiplexingKind.None;
        _evidence = Recognize(term, program, isMultiplexer);
    }

    /// <inheritdoc/>
    public bool TryAdapt(out BackendEvidence evidence)
    {
        evidence = _evidence.GetValueOrDefault();
        return _evidence.HasValue;
    }

    private static BackendEvidence? Recognize(string? term, string? program, bool isMultiplexer)
    {
        return string.Equals(program, "iTerm.app", StringComparison.OrdinalIgnoreCase)
            ? new BackendEvidence(TerminalBackendKind.Iterm2, BackendEvidenceOrigin.Environment)
            : string.IsNullOrEmpty(term) || isMultiplexer
                ? null
                : Contains(term, "kitty")
                    ? new BackendEvidence(TerminalBackendKind.Kitty, BackendEvidenceOrigin.Environment)
                    : Contains(term, "xterm")
                        ? new BackendEvidence(TerminalBackendKind.Xterm, BackendEvidenceOrigin.Environment)
                        : null;
    }

    private static bool Contains(string value, string fragment) =>
        value.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}
