// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>An <see cref="InputBase"/> derivative that uses only the shared
/// <see cref="InputBase.TryGetStepDelta"/> translation, with no press activation, segment editing,
/// or popup enabled - proving the step-key helper is a free-standing seam, not tied to any other
/// capability.</summary>
internal sealed class StepOnlyInputProbe: InputBase
{
    /// <summary>Gets every delta translated from an Up/Down key seen by <see cref="OnEvent"/>.</summary>
    internal List<int> Deltas { get; } = [];

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);

        if (eventArgs is KeyEventArgs key && TryGetStepDelta(key, out var delta))
        {
            Deltas.Add(delta);
            eventArgs.IsHandled = true;
        }
    }
}
