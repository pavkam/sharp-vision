// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>
/// Names the C0/C1 control bytes this library recognizes, so the parse and encode sides of the
/// same byte cannot silently drift apart (see #93).
/// </summary>
internal static class ControlBytes
{
    /// <summary>BEL, terminates a bell-accepted OSC string.</summary>
    public const byte Bell = 0x07;

    /// <summary>CAN, aborts an in-progress sequence.</summary>
    public const byte Cancel = 0x18;

    /// <summary>ESC, the 7-bit escape introducer.</summary>
    public const byte Escape = 0x1b;

    /// <summary>SUB, aborts an in-progress sequence like <see cref="Cancel"/>.</summary>
    public const byte Substitute = 0x1a;

    /// <summary>SS3, the 8-bit single-shift-three introducer.</summary>
    public const byte EightBitSs3 = 0x8f;

    /// <summary>DCS, the 8-bit device-control-string introducer.</summary>
    public const byte EightBitDcs = 0x90;

    /// <summary>SOS, the 8-bit start-of-string introducer.</summary>
    public const byte EightBitSos = 0x98;

    /// <summary>CSI, the 8-bit control-sequence introducer.</summary>
    public const byte EightBitCsi = 0x9b;

    /// <summary>ST, the 8-bit string terminator.</summary>
    public const byte EightBitSt = 0x9c;

    /// <summary>OSC, the 8-bit operating-system-command introducer.</summary>
    public const byte EightBitOsc = 0x9d;

    /// <summary>PM, the 8-bit privacy-message introducer.</summary>
    public const byte EightBitPm = 0x9e;

    /// <summary>APC, the 8-bit application-program-command introducer.</summary>
    public const byte EightBitApc = 0x9f;
}
