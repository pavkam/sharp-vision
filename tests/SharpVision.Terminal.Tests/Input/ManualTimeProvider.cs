namespace SharpVision.Terminal.Tests.Input;

/// <summary>Provides deterministic Escape-expiration time.</summary>
internal sealed class ManualTimeProvider: TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>Advances the current time by one test-controlled duration.</summary>
    /// <param name="value">The duration to add.</param>
    internal void Advance(TimeSpan value) => _utcNow += value;
}
