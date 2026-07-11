namespace SharpVision.Terminal.Tests.Clipboard;

/// <summary>Provides deterministic Kitty transaction time.</summary>
internal sealed class ManualTimeProvider: TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Advances the current time by one test-controlled duration.</summary>
    /// <param name="duration">The duration to add.</param>
    internal void Advance(TimeSpan duration) => _now += duration;
}
