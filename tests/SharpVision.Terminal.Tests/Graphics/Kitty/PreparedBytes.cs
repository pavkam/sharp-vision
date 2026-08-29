// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Kitty;

/// <summary>Owns copied phase bytes from one prepared backend transaction.</summary>
internal readonly struct PreparedBytes
{
    /// <summary>Initializes copied prepared bytes.</summary>
    internal PreparedBytes(byte[] uploads, byte[] cellPreludes, byte[] placements, byte[] removals)
    {
        Uploads = uploads;
        CellPreludes = cellPreludes;
        Placements = placements;
        Removals = removals;
    }

    /// <summary>Gets upload bytes.</summary>
    internal byte[] Uploads { get; }

    /// <summary>Gets virtual-placement bytes that precede projected frame cells.</summary>
    internal byte[] CellPreludes { get; }

    /// <summary>Gets placement bytes.</summary>
    internal byte[] Placements { get; }

    /// <summary>Gets removal bytes.</summary>
    internal byte[] Removals { get; }
}
