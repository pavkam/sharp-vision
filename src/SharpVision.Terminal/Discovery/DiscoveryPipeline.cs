// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>Owns the finite, ordered, side-effect-free semantic evidence pipeline.</summary>
internal sealed class DiscoveryPipeline
{
    private readonly IDiscoveryStrategy[] _strategies;

    /// <summary>Gets the standard environment, query, and explicit-override pipeline.</summary>
    public static DiscoveryPipeline Default { get; } = new(
    [
        new EnvironmentDiscoveryStrategy(),
        new QueryDiscoveryStrategy(),
        new OverrideDiscoveryStrategy()
    ]);

    /// <summary>Initializes and validates one complete fixed-phase strategy set.</summary>
    /// <param name="strategies">The non-null strategies containing exactly one strategy for each defined phase.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strategies"/> or an entry is null.</exception>
    /// <exception cref="ArgumentException">A phase is undefined, duplicated, or missing.</exception>
    public DiscoveryPipeline(IReadOnlyList<IDiscoveryStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        var phases = new bool[Enum.GetValues<DiscoveryPhase>().Length];
        _strategies = new IDiscoveryStrategy[strategies.Count];

        for (var index = 0; index < strategies.Count; index++)
        {
            var strategy = strategies[index] ?? throw new ArgumentNullException(nameof(strategies));
            var phase = strategy.Phase;

            if (!Enum.IsDefined(phase))
            {
                throw new ArgumentException("Every discovery strategy must declare a defined phase.", nameof(strategies));
            }

            var phaseIndex = (int) phase;

            if (phases[phaseIndex])
            {
                throw new ArgumentException("Each discovery phase must have exactly one strategy.", nameof(strategies));
            }

            phases[phaseIndex] = true;
            _strategies[index] = strategy;
        }

        if (phases.Any(phase => !phase))
        {
            throw new ArgumentException("Each discovery phase must have exactly one strategy.", nameof(strategies));
        }

        Array.Sort(_strategies, static (left, right) => left.Phase.CompareTo(right.Phase));
    }

    /// <summary>Folds caller evidence through the validated fixed-phase strategy order.</summary>
    /// <param name="context">The non-null immutable discovery context.</param>
    /// <returns>The resulting immutable capability snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    [Pure]
    [MustUseReturnValue]
    public TerminalCapabilities Detect(DiscoveryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var capabilities = context.Baseline;

        foreach (var strategy in _strategies)
        {
            capabilities = strategy.Apply(capabilities, context);
        }

        return capabilities;
    }
}
