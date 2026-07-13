namespace SharpVision.Terminal.Tests.Support;

using System.Runtime.InteropServices;

/// <summary>Mirrors the native Unix pseudoterminal window-size structure.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WindowSize
{
    /// <summary>Gets or sets terminal rows.</summary>
    internal ushort Rows;

    /// <summary>Gets or sets terminal columns.</summary>
    internal ushort Columns;

    /// <summary>Gets or sets optional pixel width.</summary>
    internal ushort PixelWidth;

    /// <summary>Gets or sets optional pixel height.</summary>
    internal ushort PixelHeight;
}
