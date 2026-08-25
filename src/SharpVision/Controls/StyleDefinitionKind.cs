// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Identifies the slot role a style definition is valid to initialize.</summary>
internal enum StyleDefinitionKind
{
    /// <summary>Owns a primary Style slot and the control's semantic appearance.</summary>
    Control,

    /// <summary>Owns one named secondary style slot.</summary>
    Part,

    /// <summary>Owns a primary-named aggregate slot without owning semantic appearance.</summary>
    Aggregate
}
