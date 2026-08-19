// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>Gates keyboard activation strokes against incidental modifiers.</summary>
internal static class ActivationModifiers
{
    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Used only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private const Modifiers _allowedModifiers = Modifiers.Shift | Modifiers.CapsLock | Modifiers.NumLock;

    extension(Modifiers modifiers)
    {
        /// <summary>Gets whether a keyboard stroke carrying these modifiers may activate a control.</summary>
        /// <returns>True when no modifier beyond Shift and the lock keys is held.</returns>
        [Pure]
        public bool IsActivationEligible() => (modifiers & ~_allowedModifiers) == 0;
    }
}
