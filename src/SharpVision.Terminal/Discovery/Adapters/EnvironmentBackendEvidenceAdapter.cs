// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery.Adapters;

using Backends;

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

        _ = environment.TryGetValue("TERM", out var term);
        _ = environment.TryGetValue("TERM_PROGRAM", out var program);
        _evidence = Recognize(term, program);
    }

    /// <inheritdoc/>
    public bool TryAdapt(out BackendEvidence evidence)
    {
        evidence = _evidence.GetValueOrDefault();
        return _evidence.HasValue;
    }

    private static BackendEvidence? Recognize(string? term, string? program)
    {
        return string.Equals(program, "iTerm.app", StringComparison.OrdinalIgnoreCase)
            ? new BackendEvidence(TerminalBackendKind.Iterm2, BackendEvidenceOrigin.Environment)
            : string.IsNullOrEmpty(term) || IsMultiplexerTerminal(term)
                ? null
                : Contains(term, "kitty")
                    ? new BackendEvidence(TerminalBackendKind.Kitty, BackendEvidenceOrigin.Environment)
                    : Contains(term, "xterm")
                        ? new BackendEvidence(TerminalBackendKind.Xterm, BackendEvidenceOrigin.Environment)
                        : null;
    }

    private static bool IsMultiplexerTerminal(string term) =>
        term.StartsWith("screen-", StringComparison.OrdinalIgnoreCase) ||
        term.StartsWith("tmux-", StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string value, string fragment) =>
        value.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}
