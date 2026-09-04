// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Accepts the application-owned clipboard publication route for context-menu commands.</summary>
/// <remarks>
/// Controls that ship a default context menu with copy or cut commands implement this contract so
/// <see cref="Application"/> can wire the host clipboard writer without reaching into control
/// internals. A replacement <see cref="ControlBase.ContextMenu"/> may omit the contract;
/// copy and cut then fall back to each control's own publication path.
/// </remarks>
[PublicAPI]
public interface IClipboardWriterContextMenu
{
    /// <summary>Gets or sets the application callback that publishes one owned non-null text value.</summary>
    public Action<string>? ClipboardWriter { get; set; }
}
