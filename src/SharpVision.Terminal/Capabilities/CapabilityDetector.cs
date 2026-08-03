// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

using Discovery;
using Discovery.Adapters;

/// <summary>
/// Combines conservative defaults, validated description evidence, environment
/// hints, queries, and explicit overrides.
/// </summary>
[PublicAPI]
public static class CapabilityDetector
{
    /// <summary>
    /// Produces a new immutable profile without reading process-global state.
    /// </summary>
    /// <param name="environment">Caller-supplied environment values.</param>
    /// <param name="queries">Optional bounded query results.</param>
    /// <param name="overrides">Optional explicit caller overrides.</param>
    /// <returns>A newly published immutable capability profile.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="environment"/> is <see langword="null"/>.
    /// </exception>
    public static TerminalCapabilities Detect(
        IReadOnlyDictionary<string, string?> environment,
        QueryResults? queries = null,
        CapabilityOverrides? overrides = null) => Detect(
        TerminalCapabilities.Conservative,
        environment,
        queries,
        overrides);

    /// <summary>Combines one resolved description baseline with later environment, query, and override evidence.</summary>
    /// <param name="baseline">The non-null description-derived semantic baseline.</param>
    /// <param name="environment">Caller-supplied terminal environment values.</param>
    /// <param name="queries">Optional bounded query results.</param>
    /// <param name="overrides">Optional explicit final overrides.</param>
    /// <returns>A newly published immutable capability profile.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="baseline"/> or <paramref name="environment"/> is null.</exception>
    internal static TerminalCapabilities Detect(
        TerminalCapabilities baseline,
        IReadOnlyDictionary<string, string?> environment,
        QueryResults? queries = null,
        CapabilityOverrides? overrides = null)
    {
        var context = new DiscoveryContext(baseline, environment, queries, overrides);

        return DiscoveryPipeline.Default.Detect(context);
    }

    /// <summary>
    /// Applies finite semantic support only when a validated database
    /// description retains each feature's exact non-empty backing program.
    /// </summary>
    /// <param name="capabilities">The non-null semantic capabilities to refine.</param>
    /// <param name="description">The non-null validated description metadata.</param>
    /// <param name="programs">The non-null owned compiled programs.</param>
    /// <returns>The original or refined immutable capabilities.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="capabilities"/>, <paramref name="description"/>, or
    /// <paramref name="programs"/> is <see langword="null"/>.
    /// </exception>
    internal static TerminalCapabilities ApplyDescriptionEvidence(
        TerminalCapabilities capabilities,
        Description description,
        Programs programs) => capabilities.Apply(description, programs);
}
