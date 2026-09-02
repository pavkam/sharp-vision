// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Supplies the control-specific commands and handled policy used when a
/// <see cref="SegmentFieldBehavior"/> classifies routed keyboard input.</summary>
internal sealed class SegmentFieldKeyOptions
{
    /// <summary>Initializes routed key options for one temporal field.</summary>
    /// <param name="resolveStepDelta">Returns the signed step delta admitted for a key, or null when it is not a step command.</param>
    /// <param name="clearValue">Attempts to clear the whole nullable value.</param>
    /// <param name="handleCharacterCommand">Optionally handles a non-digit character command such as AM/PM selection.</param>
    /// <param name="handlePopupCommand">Optionally opens or otherwise handles a popup command before inline editing.</param>
    /// <param name="handleRecognizedWithoutChange">Whether an admitted inline command is handled even when it cannot change the value or active segment. Only <see cref="Controls.Input.DateInput"/> opts in; <see cref="Controls.Input.TimeInput"/> and <see cref="Controls.Input.DateTimeInput"/> deliberately keep the default so a no-op edit at an already-empty or already-clamped field bubbles to ancestors there, matching their pre-existing behavior on every recognized key, not only Delete.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resolveStepDelta"/> or <paramref name="clearValue"/> is null.</exception>
    public SegmentFieldKeyOptions(
        Func<KeyEventArgs, int?> resolveStepDelta,
        Func<bool> clearValue,
        Func<Rune, bool>? handleCharacterCommand = null,
        Func<KeyEventArgs, bool?>? handlePopupCommand = null,
        bool handleRecognizedWithoutChange = false)
    {
        ArgumentNullException.ThrowIfNull(resolveStepDelta);
        ArgumentNullException.ThrowIfNull(clearValue);

        ResolveStepDelta = resolveStepDelta;
        ClearValue = clearValue;
        HandleCharacterCommand = handleCharacterCommand;
        HandlePopupCommand = handlePopupCommand;
        HandleRecognizedWithoutChange = handleRecognizedWithoutChange;
    }

    /// <summary>Gets the step-command classifier.</summary>
    public Func<KeyEventArgs, int?> ResolveStepDelta { get; }

    /// <summary>Gets the whole-value clear command.</summary>
    public Func<bool> ClearValue { get; }

    /// <summary>Gets the optional non-digit character command.</summary>
    public Func<Rune, bool>? HandleCharacterCommand { get; }

    /// <summary>Gets the optional popup command.</summary>
    public Func<KeyEventArgs, bool?>? HandlePopupCommand { get; }

    /// <summary>Gets whether admitted commands are handled without an observable transition.</summary>
    public bool HandleRecognizedWithoutChange { get; }
}
