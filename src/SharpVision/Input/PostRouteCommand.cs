// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Describes a command applied once after routing has completed.</summary>
[PublicAPI]
public enum PostRouteCommand
{
    /// <summary>Does not request additional processing.</summary>
    None,

    /// <summary>Requests forward Tab traversal from the route's stable anchor.</summary>
    TabNext,

    /// <summary>Requests backward Tab traversal from the route's stable anchor.</summary>
    TabPrevious
}
