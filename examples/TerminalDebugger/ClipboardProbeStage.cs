// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Identifies the current explicit clipboard round-trip phase.</summary>
internal enum ClipboardProbeStage
{
    /// <summary>No clipboard probe is active.</summary>
    Idle,

    /// <summary>The probe is reading existing clipboard text for later restoration.</summary>
    ReadingOriginal,

    /// <summary>The probe is waiting for an acknowledged Kitty write.</summary>
    WritingMarker,

    /// <summary>The probe is reading back its unique marker.</summary>
    ReadingMarker,

    /// <summary>The probe is restoring previously read clipboard text.</summary>
    Restoring
}
