// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery;

using Capabilities;

using QueryResults = Capabilities.Queries;

/// <summary>
/// Owns immutable caller evidence for one capability-discovery pass without
/// reading process-global environment state.
/// </summary>
internal sealed class DiscoveryContext
{
    private readonly ReadOnlyDictionary<string, string?> _environment;

    /// <summary>Initializes one owned snapshot of capability-discovery evidence.</summary>
    /// <param name="baseline">The non-null semantic baseline established before discovery.</param>
    /// <param name="environment">The non-null caller-supplied environment to copy with ordinal keys.</param>
    /// <param name="queries">The optional bounded query results.</param>
    /// <param name="overrides">The optional explicit final settings.</param>
    /// <exception cref="ArgumentNullException"><paramref name="baseline"/> or <paramref name="environment"/> is null.</exception>
    public DiscoveryContext(
        TerminalCapabilities baseline,
        IReadOnlyDictionary<string, string?> environment,
        QueryResults? queries = null,
        Settings? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(environment);

        var snapshot = new Dictionary<string, string?>(environment.Count, StringComparer.Ordinal);

        foreach (var pair in environment)
        {
            snapshot.Add(pair.Key, pair.Value);
        }

        // Preserve the caller dictionary's lookup semantics for the exact
        // allowlisted keys consumed by discovery adapters and
        // Multiplexing.Policy. The published snapshot remains ordinal and does
        // not retain the mutable caller dictionary.
        if (environment.TryGetValue("TERM", out var term))
        {
            snapshot["TERM"] = term;
        }

        if (environment.TryGetValue("COLORTERM", out var colorTerm))
        {
            snapshot["COLORTERM"] = colorTerm;
        }

        if (environment.TryGetValue("TERM_PROGRAM", out var program))
        {
            snapshot["TERM_PROGRAM"] = program;
        }

        if (environment.TryGetValue("TMUX", out var tmux))
        {
            snapshot["TMUX"] = tmux;
        }

        if (environment.TryGetValue("SSH_CONNECTION", out var sshConnection))
        {
            snapshot["SSH_CONNECTION"] = sshConnection;
        }

        if (environment.TryGetValue("SSH_TTY", out var sshTty))
        {
            snapshot["SSH_TTY"] = sshTty;
        }

        Baseline = baseline;
        _environment = new ReadOnlyDictionary<string, string?>(snapshot);
        Queries = queries;
        Overrides = overrides;
    }

    /// <summary>Gets the immutable baseline supplied by the caller or description projection.</summary>
    public TerminalCapabilities Baseline { get; }

    /// <summary>Gets the owned ordinal snapshot of caller-supplied environment evidence.</summary>
    public IReadOnlyDictionary<string, string?> Environment => _environment;

    /// <summary>Gets optional bounded query evidence.</summary>
    public QueryResults? Queries { get; }

    /// <summary>Gets optional explicit caller settings.</summary>
    public Settings? Overrides { get; }
}
