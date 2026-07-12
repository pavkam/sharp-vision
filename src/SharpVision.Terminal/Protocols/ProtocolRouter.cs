using SharpVision.Terminal.Input;

namespace SharpVision.Terminal.Protocols;

/// <summary>Routes one terminal byte stream into typed input and protocol events.</summary>
public sealed class ProtocolRouter: IDisposable
{
    private readonly Decoder _decoder;

    #region Construction

    /// <summary>Initializes a router with bounded decoder policy.</summary>
    /// <param name="sink">The non-null synchronous protocol sink.</param>
    /// <param name="options">Finite input policy, or null for defaults.</param>
    /// <param name="timeProvider">The Escape deadline clock, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is null.</exception>
    public ProtocolRouter(
        IProtocolSink sink,
        Options? options = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _decoder = new Decoder(sink, options, timeProvider);
    }

    #endregion

    #region Routing and lifetime

    /// <summary>Routes one borrowed transport fragment synchronously.</summary>
    /// <param name="input">The borrowed transport bytes.</param>
    public void Route(ReadOnlySpan<byte> input) => _decoder.Decode(input);

    /// <summary>Expires a pending lone Escape when its deadline elapsed.</summary>
    /// <returns>Whether an Escape key was emitted.</returns>
    public bool ExpireEscape() => _decoder.ExpireEscape();

    /// <summary>Completes pending input and protocol framing once.</summary>
    public void Complete() => _decoder.Complete();

    /// <summary>Releases parser and input-decoder storage.</summary>
    public void Dispose() => _decoder.Dispose();

    /// <summary>Updates ordered pixel-to-cell inference.</summary>
    /// <param name="value">Positive cell metrics, or null.</param>
    internal void SetCellMetrics(Geometry.Metrics? value) =>
        _decoder.SetCellMetrics(value);

    #endregion
}
