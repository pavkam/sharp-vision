// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

/// <summary>Describes one incremental longest-match trie result.</summary>
internal enum KeySequenceMatchStatus
{
    /// <summary>The retained bytes remain a possible key prefix.</summary>
    Pending,

    /// <summary>A longest described key completed.</summary>
    Match,

    /// <summary>No described key completed and retained bytes must be replayed.</summary>
    Replay
}
