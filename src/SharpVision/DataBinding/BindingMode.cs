// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DataBinding;

/// <summary>Identifies which endpoint initializes and receives committed binding values.</summary>
[PublicAPI]
public enum BindingMode
{
    /// <summary>Copies source values to the target and ignores target changes.</summary>
    OneWay,

    /// <summary>Copies source values to the target and committed target values back to the source.</summary>
    TwoWay,

    /// <summary>Copies target values to the source and ignores source changes.</summary>
    OneWayToSource,
}
