// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Ncurses;

/// <summary>Classifies one copied <c>tigetstr</c> result.</summary>
internal enum NativeStringStatus
{
    /// <summary>The canonical string capability is absent or cancelled.</summary>
    Absent,

    /// <summary>The canonical identifier has a different native type.</summary>
    WrongType,

    /// <summary>The value was copied into bounded managed ownership.</summary>
    Present,

    /// <summary>The native value did not terminate within the configured byte limit.</summary>
    OverLimit
}
