// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Identifies the retained text control that represents a semantic owner's access-key caption.</summary>
internal interface IAccessKeyCaptionOwner
{
    /// <summary>Gets whether one direct owned control is this owner's semantic access-key caption.</summary>
    /// <param name="candidate">The non-null candidate control.</param>
    /// <returns>True when the candidate is the owner's current retained caption.</returns>
    public bool OwnsAccessKeyCaption(ControlBase candidate);
}
