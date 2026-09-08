// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes the protected composite authoring contract for behavioral tests.</summary>
internal sealed class ProbeCompositeControl: CompositeControlBase
{
    /// <summary>Initializes an uninitialized probe.</summary>
    internal ProbeCompositeControl()
    {
    }

    /// <summary>Initializes a probe with one private composition root.</summary>
    /// <param name="content">The non-null detached composition root.</param>
    internal ProbeCompositeControl(ControlBase content) => InitializeContent(content);

    /// <summary>Gets the committed private composition root through the protected getter.</summary>
    internal ControlBase ExposedContent => Content;

    /// <summary>Attempts the protected one-shot initialization.</summary>
    /// <param name="content">The candidate private composition root.</param>
    internal void Initialize(ControlBase content) => InitializeContent(content);

    /// <summary>Requests one phase on a retained descendant through the protected owner seam.</summary>
    /// <param name="descendant">The candidate retained descendant.</param>
    /// <param name="impact">The earliest affected phase.</param>
    internal void InvalidateDescendant(ControlBase descendant, InvalidationImpact impact) =>
        InvalidateRetainedDescendant(descendant, impact);

    /// <summary>Requests a typed retained-part property bridge through the protected owner seam.</summary>
    /// <typeparam name="T">The forwarded property value type.</typeparam>
    /// <param name="source">The candidate retained source control.</param>
    /// <param name="sourcePropertyName">The non-empty source property name.</param>
    /// <param name="ownerPropertyName">The non-empty owner property name.</param>
    /// <param name="get">Reads the current source value.</param>
    /// <param name="set">Optionally writes the source value.</param>
    /// <param name="ownerImpact">
    /// The owner-side earliest phase invalidated on a published change. Exists so a test can prove
    /// this parameter reaches <see cref="ControlBase.Invalidate(InvalidationImpact)"/> whether the
    /// bridge's own <see cref="RetainedPartProperty{T}.Value"/> setter or the source's own
    /// transition publishes the change.
    /// </param>
    internal RetainedPartProperty<T> RegisterProbeRetainedPartProperty<T>(
        ControlBase source,
        string sourcePropertyName,
        string ownerPropertyName,
        Func<T> get,
        Action<T>? set = null,
        InvalidationImpact ownerImpact = InvalidationImpact.None) =>
        RegisterRetainedPartProperty(source, sourcePropertyName, ownerPropertyName, get, set, ownerImpact: ownerImpact);
}
