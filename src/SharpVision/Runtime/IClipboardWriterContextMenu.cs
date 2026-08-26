// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Accepts the application-owned clipboard publication route for context-menu commands.</summary>
internal interface IClipboardWriterContextMenu
{
    /// <summary>Gets or sets the application callback that publishes one owned non-null text value.</summary>
    internal Action<string>? ClipboardWriter { get; set; }
}
