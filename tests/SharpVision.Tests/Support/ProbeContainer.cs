// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

#pragma warning disable IDE0001 // Keep the terminal drawing alias explicit after retiring layout Canvas.
#pragma warning restore IDE0001

/// <summary>Provides a concrete parent for shared control infrastructure tests.</summary>
internal sealed class ProbeContainer: Container
{
    /// <summary>Initializes a probe with an optional child capacity and an optional owned popup.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    /// <param name="enablePopup">Whether the constructor also enables an owned popup through the
    /// protected <see cref="ControlBase.EnablePopup"/> seam, the way a third-party panel deriving
    /// from <see cref="Container"/> would - proving that popup slot keeps painting, hit testing,
    /// and tab-navigating alongside <see cref="Container.Children"/> instead of only the latter.</param>
    internal ProbeContainer(int capacity = int.MaxValue, bool enablePopup = false) : base(capacity)
    {
        EnableChromeAuthoring();

        if (enablePopup)
        {
            PopupContent = new ProbeControl(new Size(4, 1)) { Content = "P".AsMemory(), IsFocusable = true };
            Popup = EnablePopup(PopupContent, focusOnOpen: false, popupTabNavigation: TabNavigation.Continue);
        }
    }

    /// <summary>Gets the owned popup content when constructed with <c>enablePopup: true</c>, or null.</summary>
    internal ControlBase? PopupContent { get; }

    /// <summary>Gets the constructed, owned popup when constructed with <c>enablePopup: true</c>, or null.</summary>
    internal Popup? Popup { get; }

    /// <summary>Gets or sets whether rendering clips owned descendants.</summary>
    internal bool ClipChildren { get; set; } = true;

    /// <summary>Gets how often rendering requested this container's own visual bounds.</summary>
    internal int VisualBoundsReads { get; private set; }

    /// <summary>Gets or sets work invoked from inside the next OnRenderAdornment pass.</summary>
    internal Action<ProbeContainer>? RenderingAdornment { get; set; }

    /// <summary>Gets or sets work invoked from inside each arrange callback.</summary>
    internal Action<ProbeContainer>? Arranging { get; set; }

    /// <inheritdoc/>
    protected override void OnRenderAdornment(TerminalCanvas canvas)
    {
        _ = canvas;
        RenderingAdornment?.Invoke(this);
    }

    /// <summary>Gets the number of OnChildrenChanged invocations.</summary>
    internal int ChildrenChangedCalls { get; private set; }

    /// <summary>Gets or sets work invoked from inside the next OnChildrenChanged pass.</summary>
    internal Action<ProbeContainer>? ChildrenChanging { get; set; }

    /// <inheritdoc/>
    protected override void OnChildrenChanged()
    {
        ChildrenChangedCalls++;
        ChildrenChanging?.Invoke(this);
    }

    /// <inheritdoc/>
    protected override bool ClipsChildren => ClipChildren;

    /// <summary>Gets or sets whether <see cref="GetChildOrder"/> reverses the identity
    /// permutation, exercising a third-party <see cref="Container.GetChildOrder"/> override
    /// without a dedicated probe type.</summary>
    internal bool ReverseChildOrder { get; set; }

    /// <inheritdoc/>
    protected override void GetChildOrder(Span<int> indices)
    {
        if (!ReverseChildOrder)
        {
            base.GetChildOrder(indices);
            return;
        }

        for (var index = 0; index < indices.Length; index++)
        {
            indices[index] = indices.Length - index - 1;
        }
    }

    /// <inheritdoc/>
    protected override Rect VisualBounds
    {
        get
        {
            VisualBoundsReads++;
            return base.VisualBounds;
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return default;
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        _ = bounds;
        Arranging?.Invoke(this);
    }

    /// <summary>Measures one candidate through the protected direct-child seam.</summary>
    /// <param name="child">The candidate child.</param>
    /// <param name="constraint">The child constraint.</param>
    /// <returns>The committed child desired size.</returns>
    internal Size MeasureOwned(ControlBase child, Constraint constraint) =>
        MeasureChild(child, constraint);

    /// <summary>Measures one candidate through the protected direct-child seam with explicit
    /// relative request and limit bases.</summary>
    /// <param name="child">The candidate child.</param>
    /// <param name="constraint">The child constraint.</param>
    /// <param name="widthRequestBase">The containing width a relative width request resolves against.</param>
    /// <param name="heightRequestBase">The containing height a relative height request resolves against.</param>
    /// <param name="widthLimitBase">The containing width a relative width limit resolves against.</param>
    /// <param name="heightLimitBase">The containing height a relative height limit resolves against.</param>
    /// <returns>The committed child desired size.</returns>
    internal Size MeasureOwnedWithBases(
        ControlBase child,
        Constraint constraint,
        int? widthRequestBase,
        int? heightRequestBase,
        int? widthLimitBase,
        int? heightLimitBase) =>
        MeasureChild(child, constraint, widthRequestBase, heightRequestBase, widthLimitBase, heightLimitBase);

    /// <summary>Arranges one candidate through the protected direct-child seam.</summary>
    /// <param name="child">The candidate child.</param>
    /// <param name="slot">The assigned outer slot.</param>
    /// <param name="resolvedAxes">Axes already resolved by this parent.</param>
    internal void ArrangeOwned(ControlBase child, Rect slot, ResolvedAxes resolvedAxes) =>
        ArrangeChild(child, slot, resolvedAxes);

    /// <summary>Gets the descendants reported through the protected
    /// <see cref="ControlBase.OnDescendantFocused"/> ancestor hook, in call order.</summary>
    internal List<ControlBase> DescendantFocusedCalls { get; } = [];

    /// <inheritdoc/>
    protected internal override void OnDescendantFocused(ControlBase descendant)
    {
        base.OnDescendantFocused(descendant);
        DescendantFocusedCalls.Add(descendant);
    }

    /// <summary>Gets the descendants reported through the protected
    /// <see cref="ControlBase.OnDescendantAccessKey"/> ancestor hook, in call order.</summary>
    internal List<ControlBase> DescendantAccessKeyCalls { get; } = [];

    /// <summary>Gets or sets whether this probe claims every descendant access key reported to
    /// it, instead of deferring to the inherited default.</summary>
    internal bool ClaimsDescendantAccessKey { get; set; }

    /// <inheritdoc/>
    protected internal override bool? OnDescendantAccessKey(ControlBase descendant, Rune key)
    {
        DescendantAccessKeyCalls.Add(descendant);
        return ClaimsDescendantAccessKey ? true : base.OnDescendantAccessKey(descendant, key);
    }

    /// <summary>Gets the descendants reported through the protected
    /// <see cref="ControlBase.OnDescendantDisposalRequested"/> ancestor hook, in call order.</summary>
    internal List<ControlBase> DescendantDisposalRequests { get; } = [];

    /// <summary>Gets or sets whether this probe claims every descendant disposal request
    /// reported to it, instead of deferring to the inherited default.</summary>
    internal bool ClaimsDescendantDisposal { get; set; }

    /// <inheritdoc/>
    protected internal override bool OnDescendantDisposalRequested(ControlBase descendant)
    {
        DescendantDisposalRequests.Add(descendant);
        return ClaimsDescendantDisposal || base.OnDescendantDisposalRequested(descendant);
    }
}
