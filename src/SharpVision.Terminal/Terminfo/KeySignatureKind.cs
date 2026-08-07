// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

/// <summary>Identifies one parser-level terminal key signature family.</summary>
internal enum KeySignatureKind
{
    /// <summary>One C0 or DEL control byte.</summary>
    Control,

    /// <summary>One ECMA-48 Escape sequence.</summary>
    Escape,

    /// <summary>One ECMA-48 CSI sequence.</summary>
    Csi,

    /// <summary>One VT SS3 key sequence.</summary>
    Ss3
}
