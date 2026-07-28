// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Marks a display control whose access key is represented by its retained text content.</summary>
internal interface IAccessKeyCaption
{
    /// <summary>Gets the caption text whose mnemonic participates in access-key routing.</summary>
    public string? Text { get; }
}
