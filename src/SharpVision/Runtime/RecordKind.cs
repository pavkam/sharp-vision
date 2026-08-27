// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Identifies one copied terminal input queue record.</summary>
internal enum RecordKind
{
    /// <summary>A key stroke.</summary>
    Key,

    /// <summary>A text Rune.</summary>
    Text,

    /// <summary>A pointer value.</summary>
    Pointer,

    /// <summary>An owned paste.</summary>
    Paste,

    /// <summary>A terminal focus report.</summary>
    Focus,

    /// <summary>A terminal protocol diagnostic.</summary>
    Diagnostic,

    /// <summary>A typed terminal protocol response.</summary>
    Response,

    /// <summary>A typed terminal palette response.</summary>
    PaletteResponse,

    /// <summary>A typed terminal metrics response.</summary>
    MetricsResponse,

    /// <summary>A typed terminal status-string response.</summary>
    StatusResponse,

    /// <summary>A typed terminal capability-string response.</summary>
    CapabilityResponse,

    /// <summary>A decoded Kitty graphics acknowledgement.</summary>
    KittyGraphicsResponse,

    /// <summary>A decoded OSC 52 clipboard reply.</summary>
    ClipboardReply,

    /// <summary>A decoded Kitty OSC 5522 clipboard packet.</summary>
    KittyClipboardPacket,

    /// <summary>Graphics placements that fell back to ordinary cells during one committed frame.</summary>
    GraphicsDiagnostic,

    /// <summary>An orderly input closure.</summary>
    Closed,

    /// <summary>An input fault.</summary>
    Fault
}
