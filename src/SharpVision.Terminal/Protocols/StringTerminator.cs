// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>
/// Identifies the delimiter that completed a terminal string.
/// </summary>
public enum StringTerminator
{
    /// <summary>
    /// The C0 BEL byte completed an OSC string.
    /// </summary>
    Bell,

    /// <summary>
    /// The two-byte ESC backslash string terminator completed the string.
    /// </summary>
    EscapeBackslash,

    /// <summary>
    /// The single-byte C1 ST control completed the string.
    /// </summary>
    EightBit,
}
