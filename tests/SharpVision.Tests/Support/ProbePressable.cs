// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Records completed activations from the shared press behavior.</summary>
internal sealed class ProbePressable: InputBase
{
    /// <summary>Initializes an empty pressable probe with press activation, a caption, and a command
    /// composed together, mirroring the deleted PressableBase's fixed capability bundle.</summary>
    internal ProbePressable()
    {
        EnablePressActivation();
        EnableCaption();
        EnableCommand();
    }

    /// <summary>Gets completed activation causes in commit order.</summary>
    internal List<ActivationCause> Activations { get; } = [];

    /// <summary>Gets implicit capture-cancellation reasons after the shared behavior has reset.</summary>
    internal List<PointerCaptureLossReason> CaptureCancellations { get; } = [];

    /// <summary>Gets whether capture was still reported inside the latest cancellation callback.</summary>
    internal bool HadCaptureDuringCancellation { get; private set; }

    /// <summary>Gets whether pressed state remained inside the latest cancellation callback.</summary>
    internal bool WasPressedDuringCancellation { get; private set; }

    /// <summary>Attempts to enable the caption capability a second time.</summary>
    internal void EnableCaptionAgain() => EnableCaption();

    /// <summary>Attempts to enable the command capability a second time.</summary>
    internal void EnableCommandAgain() => EnableCommand();

    /// <summary>Captures the current command binding, runs <paramref name="publish"/> (which may
    /// reentrantly rebind <see cref="InputBase.Command"/>), then executes the binding captured
    /// before that callback ran - exercising the protected capture-then-execute pattern the same
    /// way <c>CheckBox</c>, <c>RadioButton</c>, and <c>MenuItem</c> use it around their own
    /// activation callback.</summary>
    /// <param name="publish">Work simulating an activation callback published between capture and execution.</param>
    internal void CaptureThenPublishThenExecute(Action publish)
    {
        var binding = CaptureCommand();
        publish();
        ExecuteCommandIfAny(binding);
    }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        var command = Command;
        var parameter = CommandParameter;

        if (command is not null && !command.CanExecute(parameter))
        {
            return;
        }

        Activations.Add(cause);
        command?.Execute(parameter);
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        HadCaptureDuringCancellation = HasPointerCapture;
        WasPressedDuringCancellation = IsPressed;
        CaptureCancellations.Add(reason);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }
}
