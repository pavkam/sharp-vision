// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Input;
using SharpVision.Terminal.Runtime;


/// <summary>Records terminal session callbacks and injected text failures.</summary>
internal sealed class RuntimeSink: ISink
{
    /// <summary>Gets decoded text callbacks.</summary>
    internal List<InputText> Text { get; } = [];

    /// <summary>Gets committed resize callbacks.</summary>
    internal List<Dimensions> Resizes { get; } = [];

    /// <summary>Gets immutable capability profiles in publication order.</summary>
    internal List<TerminalCapabilities> Profiles { get; } = [];

    /// <summary>Gets recognized terminal responses.</summary>
    internal List<Response> Responses { get; } = [];

    /// <summary>Gets owned unregistered terminal strings.</summary>
    internal List<ProtocolSequence> Sequences { get; } = [];

    /// <summary>Gets callback families in delivery order.</summary>
    internal List<string> Order { get; } = [];

    /// <summary>Gets completion for the first resize callback.</summary>
    internal TaskCompletionSource ResizeReceived { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets completion for the first capability profile.</summary>
    internal TaskCompletionSource ProfileReceived { get; } =
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
        Order.Add("text");
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
    public void Response(in Response value)
    {
        Responses.Add(value);
        Order.Add("response");
    }

    /// <inheritdoc/>
    public void Sequence(ProtocolSequence value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Sequences.Add(value);
        Order.Add("sequence");
    }

    /// <inheritdoc/>
    public void Profile(TerminalCapabilities value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Profiles.Add(value);
        Order.Add("profile");
        _ = ProfileReceived.TrySetResult();
    }

    /// <inheritdoc/>
    public void Resize(in Dimensions value)
    {
        Resizes.Add(value);
        Order.Add("resize");
        _ = ResizeReceived.TrySetResult();
    }

    /// <inheritdoc/>
    public void Closed()
    {
        ClosedCount++;
        Order.Add("closed");
    }

    /// <inheritdoc/>
    public void Fault(Exception exception) => Faults.Add(exception);
}
