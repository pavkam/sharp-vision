// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DataBinding;

/// <summary>Identifies the endpoint currently being authored by a binding.</summary>
internal enum BindingDirection
{
    /// <summary>No endpoint is being authored.</summary>
    None,

    /// <summary>The source is authoring the target.</summary>
    SourceToTarget,

    /// <summary>The target is authoring the source.</summary>
    TargetToSource,
}
