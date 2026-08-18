// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>
/// Names the DEC private mode numbers this library queries and encodes, so the discovery query
/// side and the <see cref="ProtocolModes"/> encode side cannot drift apart from each other.
/// </summary>
internal static class DecPrivateMode
{
    /// <summary>Mode 25, text cursor visibility.</summary>
    public const int CursorVisible = 25;

    /// <summary>Mode 1004, terminal focus reporting.</summary>
    public const int FocusReporting = 1004;

    /// <summary>Mode 1006, SGR-encoded cell mouse tracking coordinates.</summary>
    public const int CellMouse = 1006;

    /// <summary>Mode 1016, SGR-encoded pixel mouse tracking coordinates.</summary>
    public const int PixelMouse = 1016;

    /// <summary>Mode 1049, the alternate screen buffer.</summary>
    public const int AlternateScreen = 1049;

    /// <summary>Mode 2004, bracketed paste.</summary>
    public const int BracketedPaste = 2004;

    /// <summary>Mode 2026, synchronized output.</summary>
    public const int SynchronizedOutput = 2026;

    /// <summary>Mode 5522, Kitty clipboard paste events.</summary>
    public const int ClipboardPasteEvents = 5522;
}
