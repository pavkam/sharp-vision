// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Mirrors the native terminal window-size structure.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WindowSize
{
    /// <summary>Gets or sets terminal rows.</summary>
    public ushort Rows;

    /// <summary>Gets or sets terminal columns.</summary>
    public ushort Columns;

    /// <summary>Gets or sets optional pixel width.</summary>
    public ushort PixelWidth;

    /// <summary>Gets or sets optional pixel height.</summary>
    public ushort PixelHeight;
}
