using SharpVision.Terminal.Input;

using InputDiagnostic = SharpVision.Terminal.Protocols.Diagnostic;
using InputText = SharpVision.Terminal.Input.Text;

namespace SharpVision.Terminal.Tests.Input;

/// <summary>Counts every decoded terminal input value.</summary>
internal sealed class CountingInputSink: IInputSink
{
    /// <summary>Gets the total callback count.</summary>
    internal int Count { get; private set; }

    /// <inheritdoc/>
    public void Input(in Stroke value) => Count++;

    /// <inheritdoc/>
    public void Input(in InputText value) => Count++;

    /// <inheritdoc/>
    public void Input(in Pointer value) => Count++;

    /// <inheritdoc/>
    public void Input(Paste value) => Count++;

    /// <inheritdoc/>
    public void Input(in Focus value) => Count++;

    /// <inheritdoc/>
    public void Input(in InputDiagnostic value) => Count++;
}
