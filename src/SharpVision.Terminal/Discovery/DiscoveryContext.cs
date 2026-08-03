// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery;

using Capabilities;

using QueryResults = Capabilities.QueryResults;

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
        CapabilityOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(environment);

        Baseline = baseline;
        _environment = EnvironmentSnapshot.Create(environment);
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
    public CapabilityOverrides? Overrides { get; }
}
