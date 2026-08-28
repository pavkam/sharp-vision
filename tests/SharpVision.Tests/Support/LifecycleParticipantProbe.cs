// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Records interaction lifecycle notifications with optional reentrant work and failures.</summary>
internal sealed class LifecycleParticipantProbe: IControlLifecycleParticipant
{
    private readonly List<string> _events;
    private readonly string _name;

    /// <summary>Initializes one named participant writing to a shared ordered event sink.</summary>
    /// <param name="name">The non-empty event prefix.</param>
    /// <param name="events">The non-null ordered event sink.</param>
    internal LifecycleParticipantProbe(string name, List<string> events)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(events);
        _name = name;
        _events = events;
    }

    /// <summary>Gets or sets work invoked after a focus event is recorded.</summary>
    internal Action? FocusAction { get; set; }

    /// <summary>Gets or sets whether focus notification throws a synthetic failure.</summary>
    internal bool ThrowOnFocus { get; set; }

    /// <summary>Gets or sets work invoked after an unavailability event is recorded.</summary>
    internal Action? UnavailableAction { get; set; }

    /// <inheritdoc/>
    public void FocusChanged(bool focused)
    {
        _events.Add($"{_name}:focus:{focused}");
        FocusAction?.Invoke();

        if (ThrowOnFocus)
        {
            throw new InvalidOperationException($"{_name} focus failed.");
        }
    }

    /// <inheritdoc/>
    public void CaptureLost(PointerCaptureLossReason reason) =>
        _events.Add($"{_name}:capture:{reason}");

    /// <inheritdoc/>
    public void Unavailable(ReleaseReason reason)
    {
        _events.Add($"{_name}:unavailable:{reason}");
        UnavailableAction?.Invoke();
    }
}
