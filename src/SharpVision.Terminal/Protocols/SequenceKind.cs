// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>
/// Identifies an ECMA-48 sequence family without retaining its payload.
/// </summary>
[PublicAPI]
public enum SequenceKind
{
    /// <summary>
    /// No sequence family is available.
    /// </summary>
    None,

    /// <summary>
    /// An escape sequence introduced by ESC.
    /// </summary>
    Escape,

    /// <summary>
    /// A control sequence introduced by CSI.
    /// </summary>
    Csi,

    /// <summary>
    /// An operating system command introduced by OSC.
    /// </summary>
    Osc,

    /// <summary>
    /// A device control string introduced by DCS.
    /// </summary>
    Dcs,

    /// <summary>
    /// An application program command introduced by APC.
    /// </summary>
    Apc,

    /// <summary>
    /// A privacy message introduced by PM.
    /// </summary>
    Pm,

    /// <summary>
    /// A start-of-string payload introduced by SOS.
    /// </summary>
    Sos
}
