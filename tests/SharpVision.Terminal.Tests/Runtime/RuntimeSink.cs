using SharpVision.Terminal.Input;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Runtime;

using InputText = SharpVision.Terminal.Input.Text;

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>Records terminal session callbacks and injected text failures.</summary>
internal sealed class RuntimeSink: ISink
{
    /// <summary>Gets decoded text callbacks.</summary>
    internal List<InputText> Text { get; } = [];

    /// <summary>Gets committed resize callbacks.</summary>
    internal List<Dimensions> Resizes { get; } = [];

    /// <summary>Gets completion for the first resize callback.</summary>
    internal TaskCompletionSource ResizeReceived { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the closure callback count.</summary>
    internal int ClosedCount { get; private set; }

    /// <summary>Gets an optional exact text callback failure.</summary>
    internal Exception? TextFailure { get; init; }

    /// <summary>Gets reported runtime faults.</summary>
    internal List<Exception> Faults { get; } = [];

    /// <inheritdoc/>
    public void Input(in Stroke value) => _ = value;

    /// <inheritdoc/>
    public void Input(in InputText value)
    {
        if (TextFailure is not null)
        {
            throw TextFailure;
        }

        Text.Add(value);
    }

    /// <inheritdoc/>
    public void Input(in Pointer value) => _ = value;

    /// <inheritdoc/>
    public void Input(Paste value) => _ = value;

    /// <inheritdoc/>
    public void Input(in Focus value) => _ = value;

    /// <inheritdoc/>
    public void Input(in Diagnostic value) => _ = value;

    /// <inheritdoc/>
    public void Resize(in Dimensions value)
    {
        Resizes.Add(value);
        _ = ResizeReceived.TrySetResult();
    }

    /// <inheritdoc/>
    public void Closed() => ClosedCount++;

    /// <inheritdoc/>
    public void Fault(Exception exception) => Faults.Add(exception);
}
