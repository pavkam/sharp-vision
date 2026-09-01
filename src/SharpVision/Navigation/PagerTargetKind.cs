// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Identifies one semantic target in a committed Pager layout.</summary>
internal enum PagerTargetKind
{
    /// <summary>Moves to the first page.</summary>
    First,

    /// <summary>Moves to the previous page.</summary>
    Previous,

    /// <summary>Moves to one numbered page.</summary>
    Number,

    /// <summary>Marks a numeric gap without accepting input.</summary>
    Omitted,

    /// <summary>Moves to the next page.</summary>
    Next,

    /// <summary>Moves to the last page.</summary>
    Last
}
