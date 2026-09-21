// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery.Adapters;

using Backends;

using Capabilities;

/// <summary>Recognizes terminal identity from an owned terminal-description name.</summary>
internal sealed class DescriptionBackendEvidenceAdapter: IBackendEvidenceAdapter
{
    private readonly BackendEvidence? _evidence;

    /// <summary>Snapshots recognized evidence from one non-null terminal description, narrowed by
    /// a caller-supplied environment snapshot.</summary>
    /// <param name="description">The non-null owned terminal description to inspect.</param>
    /// <param name="environment">The non-null caller-supplied environment snapshot, used only to
    /// detect an active multiplexer.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="description"/> or <paramref name="environment"/> is <see langword="null"/>.
    /// </exception>
    public DescriptionBackendEvidenceAdapter(Description description, IReadOnlyDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(environment);

        // Reuse the same authoritative multiplexer detection Policy.Detect already applies
        // elsewhere in this discovery pass, matching EnvironmentBackendEvidenceAdapter's own gate.
        var isMultiplexer = MultiplexingPolicy.Detect(environment).Kind != MultiplexerKind.None;
        _evidence = Recognize(description.Name, isMultiplexer);
    }

    /// <inheritdoc/>
    public bool TryAdapt(out BackendEvidence evidence)
    {
        evidence = _evidence.GetValueOrDefault();
        return _evidence.HasValue;
    }

    private static BackendEvidence? Recognize(string name, bool isMultiplexer)
    {
        // A description name is derived from the pane's own TERM (see ConsoleConnection), which a
        // multiplexer rewrites to its own terminfo entry or leaves at an outer-terminal value a
        // tmux.conf/.screenrc commonly preserves (e.g. `set -g default-terminal "xterm-kitty"`).
        // Either way TERM inside a multiplexer no longer identifies the outer terminal, so all
        // three candidates are suppressed here. Unlike EnvironmentBackendEvidenceAdapter, a
        // description carries no TERM_PROGRAM-style independent signal for iTerm2, so iTerm2 gets
        // no carve-out and is suppressed along with Kitty and xterm.
        return !isMultiplexer && Contains(name, "kitty")
            ? new BackendEvidence(TerminalBackendKind.Kitty, BackendEvidenceOrigin.Description)
            : !isMultiplexer && Contains(name, "iterm2")
                ? new BackendEvidence(TerminalBackendKind.Iterm2, BackendEvidenceOrigin.Description)
                : !isMultiplexer && TerminalNames.IsXtermFamily(name)
                    ? new BackendEvidence(TerminalBackendKind.Xterm, BackendEvidenceOrigin.Description)
                    : null;
    }

    private static bool Contains(string value, string fragment) =>
        value.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}
