// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Identifies the current primary-pointer selection arbitration phase.</summary>
internal enum DocumentSelectionGesturePhase
{
    /// <summary>No primary-pointer selection transaction is active.</summary>
    Idle,

    /// <summary>A content press remains eligible to complete as an ordinary click.</summary>
    Potential,

    /// <summary>The drag threshold was crossed and the document owns pointer capture.</summary>
    Selecting
}
