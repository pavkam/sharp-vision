// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Xterm;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Encodes xterm modifyOtherKeys query, set, and restoration commands.</summary>
[PublicAPI]
public static class XtermModifyOtherKeys
{
    /// <summary>Queries the current xterm modifyOtherKeys value.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void Query(ProtocolWriter writer) => writer.Csi("?4"u8, [], (byte) 'm');

    /// <summary>Sets xterm modifyOtherKeys to level zero through three.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="level">The level from zero through three.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is outside zero through three.</exception>
    public static void Set(ProtocolWriter writer, [ValueRange(0, 3)] int level)
    {
        if (level is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "The modifyOtherKeys level must be zero through three.");
        }

        Span<byte> parameters = [(byte) '>', (byte) '4', (byte) ';', (byte) ('0' + level)];
        writer.Csi(parameters, [], (byte) 'm');
    }

    /// <summary>Restores xterm's configured initial modifyOtherKeys value.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    public static void Restore(ProtocolWriter writer) => writer.Csi(">4"u8, [], (byte) 'm');
}
