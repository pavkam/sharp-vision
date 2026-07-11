using System.Runtime.InteropServices;

namespace SharpVision.Terminal.Runtime;

/// <summary>Mirrors the native terminal window-size structure.</summary>
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
