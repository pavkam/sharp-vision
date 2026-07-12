using SharpVision.Terminal.Input;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Runtime;

using TerminalCapabilities = SharpVision.Terminal.Capabilities.Capabilities;

namespace SharpVision.Terminal.Tests.Transport;

/// <summary>Records one expected pseudoterminal resize and runtime faults.</summary>
internal sealed class RuntimeSink: ISink
{
    private readonly Dimensions _expected;

    /// <summary>Initializes a sink for one expected resize.</summary>
    /// <param name="expected">The validated expected dimensions.</param>
    internal RuntimeSink(Dimensions expected) => _expected = expected;

    /// <summary>Gets completion for the expected resize.</summary>
    internal TaskCompletionSource<Dimensions> Expected { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets reported runtime faults.</summary>
    internal List<Exception> Faults { get; } = [];

    /// <inheritdoc/>
    public void Input(in Stroke value) => _ = value;

    /// <inheritdoc/>
    public void Input(in Text value) => _ = value;

    /// <inheritdoc/>
    public void Input(in Pointer value) => _ = value;

    /// <inheritdoc/>
    public void Input(Paste value) => _ = value;

    /// <inheritdoc/>
    public void Input(in Focus value) => _ = value;

    /// <inheritdoc/>
    public void Input(in Diagnostic value) => _ = value;

    /// <inheritdoc/>
    public void Response(in Response value) => _ = value;

    /// <inheritdoc/>
    public void Sequence(ProtocolSequence value) => ArgumentNullException.ThrowIfNull(value);

    /// <inheritdoc/>
    public void Profile(TerminalCapabilities value) => ArgumentNullException.ThrowIfNull(value);

    /// <inheritdoc/>
    public void Resize(in Dimensions value)
    {
        if (value == _expected)
        {
            _ = Expected.TrySetResult(value);
        }
    }

    /// <inheritdoc/>
    public void Closed()
    {
    }

    /// <inheritdoc/>
    public void Fault(Exception exception) => Faults.Add(exception);
}
