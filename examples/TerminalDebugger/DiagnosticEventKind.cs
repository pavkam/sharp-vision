// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Identifies one decoded diagnostic event family.</summary>
internal enum DiagnosticEventKind
{
    /// <summary>A keyboard transition.</summary>
    Key,

    /// <summary>A decoded Unicode text scalar.</summary>
    Text,

    /// <summary>A pointer transition or motion event.</summary>
    Pointer,

    /// <summary>A bracketed-paste payload.</summary>
    Paste,

    /// <summary>A terminal focus transition.</summary>
    Focus,

    /// <summary>A terminal resize.</summary>
    Resize,

    /// <summary>A clipboard protocol event.</summary>
    Clipboard,

    /// <summary>An application diagnostic.</summary>
    Diagnostic
}
