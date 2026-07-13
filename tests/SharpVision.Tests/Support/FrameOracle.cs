// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using System.Text;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;

/// <summary>Reads exact semantic frame graphemes for control-render assertions.</summary>
internal static class FrameOracle
{
    /// <summary>Copies and decodes the complete grapheme owning one cell.</summary>
    /// <param name="frame">The non-null source frame.</param>
    /// <param name="point">The in-bounds lead or continuation cell.</param>
    /// <returns>The exact stored UTF-16 grapheme, or an empty string for blank.</returns>
    internal static string Get(Frame frame, Point point)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var length = frame.GetGraphemeByteCount(point);

        if (length == 0)
        {
            return string.Empty;
        }

        if (length <= 256)
        {
            Span<byte> buffer = stackalloc byte[length];
            _ = frame.CopyGrapheme(point, buffer);
            return Encoding.UTF8.GetString(buffer);
        }

        var rented = new byte[length];
        _ = frame.CopyGrapheme(point, rented);
        return Encoding.UTF8.GetString(rented);
    }
}
