// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Identifies the structural purpose of controls held by an ownership slot.</summary>
internal enum OwnedControlRole
{
    /// <summary>Represents an ordinary child exposed by a general-purpose container.</summary>
    ContainerChild,

    /// <summary>Represents the single logical value displayed by a content-bearing control.</summary>
    Content,

    /// <summary>Represents the root of a composite control's private implementation tree.</summary>
    CompositionRoot,

    /// <summary>Represents one visual generated or supplied for an item.</summary>
    ItemVisual,

    /// <summary>Represents the internal owner that arranges item visuals.</summary>
    ItemHost,

    /// <summary>Represents private framework chrome or another implementation detail.</summary>
    FrameworkPart
}
