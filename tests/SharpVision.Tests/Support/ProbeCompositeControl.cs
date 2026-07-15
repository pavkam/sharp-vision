// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes the protected composite authoring contract for behavioral tests.</summary>
internal sealed class ProbeCompositeControl: CompositeControl
{
    /// <summary>Initializes an uninitialized probe.</summary>
    internal ProbeCompositeControl()
    {
    }

    /// <summary>Initializes a probe with one private composition root.</summary>
    /// <param name="content">The non-null detached composition root.</param>
    internal ProbeCompositeControl(Control content) => InitializeContent(content);

    /// <summary>Gets the committed private composition root through the protected getter.</summary>
    internal Control ExposedContent => Content;

    /// <summary>Attempts the protected one-shot initialization.</summary>
    /// <param name="content">The candidate private composition root.</param>
    internal void Initialize(Control content) => InitializeContent(content);
}
