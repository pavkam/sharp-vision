// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Diagnostics;
using SharpVision.Terminal.Input;

/// <summary>Records terminal session callbacks and injected text failures.</summary>
internal sealed class RuntimeSink: ISink
{
    /// <summary>Gets decoded text callbacks.</summary>
    internal List<TerminalText> Text { get; } = [];

    /// <summary>Gets decoded key callbacks.</summary>
    internal List<Stroke> Strokes { get; } = [];

    /// <summary>Gets committed resize callbacks.</summary>
    internal List<Dimensions> Resizes { get; } = [];

    /// <summary>Gets immutable capability profiles in publication order.</summary>
    internal List<TerminalCapabilities> Profiles { get; } = [];

    /// <summary>Gets immutable terminal diagnostic snapshots in publication order.</summary>
    internal List<TerminalDiagnostics> DiagnosticSnapshots { get; } = [];

    /// <summary>Gets recognized terminal responses.</summary>
    internal List<XtermCapabilitiesResponse> Responses { get; } = [];

    /// <summary>Gets recognized terminal color responses.</summary>
    internal List<PaletteResponse> PaletteResponses { get; } = [];

    /// <summary>Gets recognized terminal metrics responses.</summary>
    internal List<MetricsResponse> MetricsResponses { get; } = [];

    /// <summary>Gets owned unregistered terminal strings.</summary>
    internal List<ProtocolSequence> Sequences { get; } = [];

    /// <summary>Gets callback families in delivery order.</summary>
    internal List<string> Order { get; } = [];

    /// <summary>Gets completion for the first resize callback.</summary>
    /// <summary>Completes when the first key stroke arrives.</summary>
    internal TaskCompletionSource StrokeReceived { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal TaskCompletionSource ResizeReceived { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets completion for the first capability profile.</summary>
    internal TaskCompletionSource ProfileReceived { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets completion for the first reported runtime fault.</summary>
    internal TaskCompletionSource<Exception> FaultReceived { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the closure callback count.</summary>
    internal int ClosedCount { get; private set; }

    /// <summary>Gets an optional exact text callback failure.</summary>
    internal Exception? TextFailure { get; init; }

    /// <summary>Gets an optional exact resize callback failure.</summary>
    internal Exception? ResizeFailure { get; init; }

    /// <summary>Gets an optional exact fault notification failure.</summary>
    internal Exception? FaultFailure { get; init; }

    /// <summary>Gets an optional hook invoked synchronously before a resize is recorded, so a
    /// caller can capture state exactly at the point of delivery without an async continuation
    /// race.</summary>
    internal Action? OnResize { get; init; }

    /// <summary>Gets an optional hook invoked synchronously before closure is recorded, so a
    /// caller can observe state - or call back into the session - from inside the run's own
    /// callback dispatch.</summary>
    internal Action? OnClosed { get; init; }

    /// <summary>Gets reported runtime faults.</summary>
    internal List<Exception> Faults { get; } = [];

    /// <summary>Gets redacted protocol diagnostics in delivery order.</summary>
    internal List<Diagnostic> Diagnostics { get; } = [];

    /// <inheritdoc/>
    public void Input(in Stroke value)
    {
        Strokes.Add(value);
        _ = StrokeReceived.TrySetResult();
    }

    /// <inheritdoc/>
    public void Input(in TerminalText value)
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
    public void Input(in TerminalFocus value) => _ = value;

    /// <inheritdoc/>
    public void Input(in Diagnostic value) => Diagnostics.Add(value);

    /// <inheritdoc/>
    public void Response(in XtermCapabilitiesResponse value)
    {
        Responses.Add(value);
        Order.Add("response");
    }

    /// <inheritdoc/>
    public void Response(in PaletteResponse value)
    {
        PaletteResponses.Add(value);
        Order.Add("palette-response");
    }

    /// <inheritdoc/>
    public void Response(in MetricsResponse value)
    {
        MetricsResponses.Add(value);
        Order.Add("metrics-response");
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
    void ISink.Diagnostics(TerminalDiagnostics value)
    {
        ArgumentNullException.ThrowIfNull(value);
        DiagnosticSnapshots.Add(value);
        Order.Add("diagnostics");
    }

    /// <inheritdoc/>
    public void Resize(in Dimensions value)
    {
        if (ResizeFailure is not null)
        {
            throw ResizeFailure;
        }

        OnResize?.Invoke();
        Resizes.Add(value);
        Order.Add("resize");
        _ = ResizeReceived.TrySetResult();
    }

    /// <inheritdoc/>
    public void Closed()
    {
        OnClosed?.Invoke();
        ClosedCount++;
        Order.Add("closed");
    }

    /// <inheritdoc/>
    public void Fault(Exception exception)
    {
        // Recorded before the injected failure (unlike TextFailure/ResizeFailure above) so a test
        // can still observe which exception the run tried to notify even when the notification
        // callback itself is made to fail.
        Faults.Add(exception);
        _ = FaultReceived.TrySetResult(exception);

        if (FaultFailure is not null)
        {
            throw FaultFailure;
        }
    }
}
