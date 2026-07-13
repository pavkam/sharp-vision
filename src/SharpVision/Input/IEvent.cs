// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Provides the non-generic routing strategy contract.</summary>
internal interface IEvent
{
    /// <summary>Gets the ancestry traversal strategy.</summary>
    public Strategy Strategy { get; }
}
