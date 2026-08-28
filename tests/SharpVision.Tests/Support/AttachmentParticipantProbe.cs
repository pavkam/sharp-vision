// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Records attachment participant publication and optional failures.</summary>
internal sealed class AttachmentParticipantProbe: IControlAttachmentParticipant
{
    private readonly List<string>? _events;
    private readonly string _name;

    /// <summary>Initializes a participant that optionally records ordered lifecycle events.</summary>
    /// <param name="name">The participant name written to <paramref name="events"/>.</param>
    /// <param name="events">The optional shared ordered event sink.</param>
    internal AttachmentParticipantProbe(string name = "participant", List<string>? events = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        _events = events;
    }

    /// <summary>Gets the committed dispatchers observed in order.</summary>
    internal List<Dispatcher> Attachments { get; } = [];

    /// <summary>Gets the detach callback count.</summary>
    internal int DetachCalls { get; private set; }

    /// <summary>Gets the final disposal callback count.</summary>
    internal int DisposeCalls { get; private set; }

    /// <summary>Gets or sets whether attachment reports a synthetic failure.</summary>
    internal bool ThrowOnAttach { get; set; }

    /// <summary>Gets or sets whether final disposal reports a synthetic failure.</summary>
    internal bool ThrowOnDispose { get; set; }

    /// <inheritdoc/>
    public void OnOwnerAttached(Dispatcher dispatcher)
    {
        Attachments.Add(dispatcher);
        _events?.Add($"{_name}:attach");

        if (ThrowOnAttach)
        {
            throw new InvalidOperationException("Synthetic participant attachment failure.");
        }
    }

    /// <inheritdoc/>
    public void OnOwnerDetached()
    {
        DetachCalls++;
        _events?.Add($"{_name}:detach");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        DisposeCalls++;
        _events?.Add($"{_name}:dispose");

        if (ThrowOnDispose)
        {
            throw new InvalidOperationException("Synthetic participant disposal failure.");
        }
    }
}
