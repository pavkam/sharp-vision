// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>Describes the delimiter following one CSI parameter field.</summary>
public enum ParameterSeparator
{
    /// <summary>The field is the final field.</summary>
    None,

    /// <summary>A semicolon begins the next independent parameter.</summary>
    Semicolon,

    /// <summary>A colon begins the next subparameter.</summary>
    Colon,
}
