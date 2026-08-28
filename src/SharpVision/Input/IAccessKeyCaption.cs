// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Marks a display control whose access key is represented by retained text content.</summary>
/// <remarks>A semantic parent may implement <see cref="IAccessKeyCaptionOwner"/> to own this
/// caption's mnemonic policy and suppress duplicate dispatch to the retained display control.</remarks>
internal interface IAccessKeyCaption
{
    /// <summary>Gets the caption text whose mnemonic participates in access-key routing.</summary>
    public string? Text { get; }
}
