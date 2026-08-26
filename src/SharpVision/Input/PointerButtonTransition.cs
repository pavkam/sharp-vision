// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>Classifies pointer button transitions shared by press, drag, and selection gestures.</summary>
internal static class PointerButtonTransition
{
    /// <summary>Returns whether the input completes a primary-button gesture.</summary>
    /// <param name="pointer">The immutable pointer transition.</param>
    /// <returns>
    /// True for an explicit primary release or the buttonless release emitted by protocols that
    /// cannot identify the released button; otherwise false.
    /// </returns>
    [Pure]
    internal static bool IsPrimaryRelease(Pointer pointer) =>
        pointer.Action == PointerAction.Release &&
        (pointer.Buttons == Buttons.None || (pointer.Buttons & Buttons.Primary) != 0);
}
