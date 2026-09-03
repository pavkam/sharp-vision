// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>An <see cref="InputBase"/> derivative that composes an owned popup, press activation,
/// and segment editing together over a plain calendar-like container - mirroring
/// <see cref="DateInput"/>'s full capability combination.</summary>
internal sealed class PopupCalendarInputProbe: InputBase
{
    private readonly SegmentFieldBehavior _segments;

    /// <summary>Initializes a probe whose popup wraps one focusable content child and whose field
    /// exposes one editable segment.</summary>
    internal PopupCalendarInputProbe()
    {
        Content = new ProbeContainer();
        Item = new ProbeControl { IsFocusable = true };
        Content.Children.Add(Item);
        Popup = EnablePopup(Content, focusOnOpen: true);
        EnablePressActivation();
        _segments = EnableSegmentEditing(BuildSegments, ApplyDigit, Increment, Clear);
    }

    /// <summary>Gets the popup's owned container content.</summary>
    internal ProbeContainer Content { get; }

    /// <summary>Gets the focusable child inside <see cref="Content"/>.</summary>
    internal ProbeControl Item { get; }

    /// <summary>Gets the constructed, owned popup.</summary>
    internal Popup Popup { get; }

    /// <summary>Gets the current backing value, clamped to 0 through 23.</summary>
    internal int Value { get; private set; }

    /// <summary>Gets completed activation causes in commit order.</summary>
    internal List<ActivationCause> Activations { get; } = [];

    /// <summary>Gets or sets whether the owned popup is open.</summary>
    internal new bool IsOpen
    {
        get => base.IsOpen;
        set => base.IsOpen = value;
    }

    /// <summary>Increments the active segment through the protected seam.</summary>
    internal bool IncrementSegment(int delta) => _segments.Increment(delta);

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        Activations.Add(cause);
        IsOpen = !IsOpen;
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }

    private SegmentDescriptor[] BuildSegments() =>
        [new SegmentDescriptor(Value.ToString("D2", CultureInfo.InvariantCulture), TemporalSegmentKind.Hour, 2, 23)];

    private bool ApplyDigit(SegmentDescriptor segment, int digit)
    {
        _ = segment;
        var clamped = Math.Clamp(digit, 0, 23);

        if (clamped == Value)
        {
            return false;
        }

        Value = clamped;
        return true;
    }

    private bool Increment(SegmentDescriptor segment, int delta)
    {
        _ = segment;
        var clamped = Math.Clamp(Value + delta, 0, 23);

        if (clamped == Value)
        {
            return false;
        }

        Value = clamped;
        return true;
    }

    private bool Clear(SegmentDescriptor segment)
    {
        _ = segment;

        if (Value == 0)
        {
            return false;
        }

        Value = 0;
        return true;
    }
}
