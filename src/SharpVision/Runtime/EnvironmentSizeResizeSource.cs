// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using JetBrains.Annotations;

/// <summary>Substitutes the <c>COLUMNS</c> and <c>LINES</c> environment variables for the first
/// dimensions an inner resize source reports.</summary>
/// <remarks>
/// Backs <see cref="ConsoleRunOptions.UseEnvironmentSizeOverrides"/>. Only the first observation is
/// replaced, from whichever entry point produces it; every later one passes through untouched,
/// because the environment describes the size the application should start at and a real resize
/// must always win over it afterwards.
/// </remarks>
internal sealed class EnvironmentSizeResizeSource: IResizeSource
{
    private readonly IResizeSource _inner;
    private readonly Size _cells;
    private int _consumed;

    private EnvironmentSizeResizeSource(IResizeSource inner, Size cells)
    {
        _inner = inner;
        _cells = cells;
    }

    /// <summary>Wraps <paramref name="inner"/> when the environment names a complete size.</summary>
    /// <param name="inner">The non-null source being decorated.</param>
    /// <param name="readEnvironment">The non-null environment-variable reader.</param>
    /// <returns>
    /// A decorated source, or <paramref name="inner"/> unchanged when no override applies - so an
    /// application that enabled the option in an environment that does not describe a size keeps
    /// exactly the behavior it had without it, with no added indirection.
    /// </returns>
    [MustUseReturnValue(
        "Wrap either decorates or returns the supplied source; discarding the result silently "
            + "keeps using the un-overridden source.")]
    public static IResizeSource Wrap(IResizeSource inner, Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(readEnvironment);

        // Both axes are required. A half-specified override is not an initial size, and pairing
        // one environment value with one measured axis would hand the application a geometry
        // neither the caller nor the terminal ever described.
        return ReadAxis(readEnvironment("COLUMNS")) is { } columns &&
               ReadAxis(readEnvironment("LINES")) is { } lines
            ? new EnvironmentSizeResizeSource(inner, new Size(columns, lines))
            : inner;
    }

    /// <inheritdoc/>
    public bool TryReadCurrent(out Dimensions value)
    {
        // The inner snapshot is taken either way, because sources such as ConsoleResizeSource use
        // it to record what they have already reported; skipping it would make their next
        // ReadAsync republish the real size as though it were a change.
        var observed = _inner.TryReadCurrent(out var current);

        if (Interlocked.Exchange(ref _consumed, 1) != 0)
        {
            value = current;
            return observed;
        }

        value = Override();

        // The override stands in for a missing snapshot as well, so a source with no synchronous
        // current size still unblocks startup whenever the environment names one.
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken)
    {
        var observed = await _inner.ReadAsync(cancellationToken).ConfigureAwait(false);

        return Interlocked.Exchange(ref _consumed, 1) == 0 ? Override() : observed;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _inner.DisposeAsync();

    private static int? ReadAxis(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
        parsed > 0
            ? parsed
            : null;

    // Pixel dimensions are deliberately dropped rather than carried over from the observation.
    // They describe the real window, and pairing them with overridden cells would let Dimensions
    // derive cell metrics for a cell size that does not exist.
    private Dimensions Override() => new(_cells);
}
