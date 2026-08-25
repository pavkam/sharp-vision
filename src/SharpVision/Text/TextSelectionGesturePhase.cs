// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Identifies one control-wide primary-pointer text-selection phase.</summary>
internal enum TextSelectionGesturePhase
{
    /// <summary>No primary-pointer selection transaction is active.</summary>
    Idle,

    /// <summary>A semantic-content press remains eligible to complete as a click.</summary>
    Potential,

    /// <summary>The drag threshold was crossed and the selection owner holds capture.</summary>
    Selecting
}
