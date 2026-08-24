// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Text;

/// <summary>Defines a reusable component that owns one permanent private implementation root.</summary>
/// <remarks>
/// A concrete constructor creates its retained implementation tree and transfers one detached root
/// through <see cref="InitializeContent"/>. The root is not publicly replaceable and participates
/// in the shared ownership, layout, rendering, input, context, and disposal infrastructure.
/// </remarks>
[PublicAPI]
public abstract class CompositeControlBase: ControlBase, ISelectableTextSource
{
    private readonly OwnedControlSlot _contentSlot;
    private bool _initializationConsumed;

    /// <summary>Initializes an empty component awaiting constructor-time content initialization.</summary>
    protected CompositeControlBase()
    {
        _contentSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.CompositionRoot,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: null,
                InvalidationImpact.Measure),
            capacity: 1);
    }

    /// <summary>Gets the committed private implementation root.</summary>
    /// <exception cref="InvalidOperationException">
    /// The root has not been initialized or was disposed directly.
    /// </exception>
    protected ControlBase Content => GetContent();

    /// <summary>Creates one aggregate snapshot from the permanent private implementation root.</summary>
    /// <returns>An owned, non-authoritative projection of visible semantic descendants.</returns>
    /// <exception cref="InvalidOperationException">
    /// The component is attached off-dispatcher, or its root is uninitialized or was disposed directly.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The component has been disposed.</exception>
    public SelectableTextSnapshot GetSelectableTextSnapshot()
    {
        VerifyMutable();
        return SelectableTextAggregation.Create(this);
    }

    /// <inheritdoc/>
    internal override bool AddSelectableTextChildren(List<ControlBase> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        children.Add(GetContent());
        return true;
    }

    /// <summary>Transfers one detached implementation root to this component permanently.</summary>
    /// <param name="content">The non-null detached root of the retained implementation tree.</param>
    /// <remarks>
    /// Candidate validation failures before ownership commit do not consume initialization. Once
    /// the edge commits, initialization remains consumed even when a lifecycle or ownership
    /// callback throws. Disposing the committed root does not permit later reinitialization.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="content"/> is attached, already owned, or would create an ownership cycle.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The component is attached and accessed off-dispatcher, ownership publication is active, or
    /// initialization was already committed.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The component or content is disposed.</exception>
    protected void InitializeContent(ControlBase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        VerifyMutable();

        if (_initializationConsumed || _contentSlot.Count != 0)
        {
            throw new InvalidOperationException(
                "A composite control's composition root can be initialized only once.");
        }

        try
        {
            _contentSlot.Add(content);
        }
        finally
        {
            // The registry publishes callbacks after structural commit. A callback may throw even
            // though this edge is now permanent, so the committed slot—not method completion—is
            // authoritative for whether initialization has been consumed.
            _initializationConsumed |= _contentSlot.Count != 0;
        }
    }

    /// <inheritdoc/>
    internal sealed override void ValidateAttachment()
    {
        _ = GetContent();
        base.ValidateAttachment();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var content = GetContent();
        var desired = MeasureChild(content, constraint);

        return content.Visibility == Visibility.Collapsed
            ? default
            : new Size(
                desired.Width.SaturatingAdd(content.Margin.Horizontal),
                desired.Height.SaturatingAdd(content.Margin.Vertical));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        ArrangeChild(GetContent(), bounds, ResolvedAxes.Both);

    [Pure]
    private ControlBase GetContent() => _contentSlot.Count == 0
        ? throw new InvalidOperationException(
            "A composite control requires one committed composition root.")
        : _contentSlot[0];

}
