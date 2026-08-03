// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

#pragma warning disable IDE0001 // Keep the terminal drawing alias explicit after retiring layout Canvas.
using TerminalCanvas = Terminal.Rendering.TerminalCanvas;
#pragma warning restore IDE0001

/// <summary>Provides a recording leaf for shared control infrastructure tests.</summary>
internal sealed class ProbeControl: ChromeProbe
{
    private readonly Size _intrinsic;
    private int _kernelValue;

    /// <summary>Initializes a probe with one validated intrinsic size.</summary>
    /// <param name="intrinsic">The non-negative intrinsic content size.</param>
    internal ProbeControl(Size intrinsic = default) => _intrinsic = intrinsic;

    /// <summary>Gets constraints received by the content measure extension point.</summary>
    internal List<Constraint> MeasureConstraints { get; } = [];

    /// <summary>Gets rectangles received by the content arrange extension point.</summary>
    internal List<Rect> ArrangeBounds { get; } = [];

    /// <summary>Gets or sets work invoked from inside the next measure pass.</summary>
    internal Action<ProbeControl>? Measuring { get; set; }

    /// <summary>Gets the natural content extent captured by the base measure.</summary>
    internal Size ExposedContentExtent => ContentExtent;

    /// <summary>Gets or sets work invoked from inside the next arrange pass.</summary>
    internal Action<ProbeControl>? Arranging { get; set; }

    /// <summary>Gets or sets borrowed text drawn by the render extension point.</summary>
    internal ReadOnlyMemory<char> Content { get; set; }

    /// <summary>Gets the number of render extension-point invocations.</summary>
    internal int RenderCalls { get; private set; }

    /// <summary>Gets or sets work invoked from inside the next render pass.</summary>
    internal Action<ProbeControl>? Rendering { get; set; }

    /// <summary>Gets the number of OnRenderAdornment invocations.</summary>
    internal int AdornmentRenderCalls { get; private set; }

    /// <summary>Gets or sets work invoked from inside the next OnRenderAdornment pass.</summary>
    internal Action<ProbeControl>? RenderingAdornment { get; set; }

    /// <summary>Gets the number of completed attachment callbacks.</summary>
    internal int AttachedCalls { get; private set; }

    /// <summary>Gets whether attachment state was committed before the latest callback.</summary>
    internal bool AttachedStateWasCommitted { get; private set; }

    /// <summary>Gets the number of completed detachment callbacks.</summary>
    internal int DetachedCalls { get; private set; }

    /// <summary>Gets whether detachment state was committed before the latest callback.</summary>
    internal bool DetachedStateWasCommitted { get; private set; }

    /// <summary>Gets the number of disposal callbacks.</summary>
    internal int DisposingCalls { get; private set; }

    /// <summary>Gets or sets whether the disposal callback throws a deterministic failure.</summary>
    internal bool ThrowOnDisposing { get; set; }

    /// <summary>Gets the number of implicit pointer-capture cancellation callbacks.</summary>
    internal int PointerCaptureCancellationCalls { get; private set; }

    /// <summary>Gets the latest implicit pointer-capture cancellation reason.</summary>
    internal PointerCaptureLossReason? PointerCaptureCancellationReason { get; private set; }

    /// <summary>Gets whether pointer state was clear before the latest cancellation callback.</summary>
    internal bool PointerStateWasClearDuringCancellation { get; private set; }

    /// <summary>Gets or sets whether the capture-cancellation hook throws a deterministic failure.</summary>
    internal bool ThrowOnPointerCaptureCancellation { get; set; }

    /// <summary>Gets or sets whether the cancellation hook attempts to reacquire capture.</summary>
    internal bool RecaptureDuringPointerCancellation { get; set; }

    /// <summary>Gets the result of the latest capture request made by the cancellation hook.</summary>
    internal bool? RecaptureResult { get; private set; }

    /// <summary>Gets or sets whether clearing pressed state attempts to reacquire capture.</summary>
    internal bool RecaptureWhenPressedClears { get; set; }

    /// <summary>Gets the result of the latest capture request made while pressed state cleared.</summary>
    internal bool? PressedClearRecaptureResult { get; private set; }

    /// <summary>Requests keyboard focus through the protected consumer seam.</summary>
    /// <returns>Whether focus was acquired or already owned.</returns>
    internal bool RequestProbeFocus() => RequestFocus();

    /// <summary>Requests pointer capture through the protected consumer seam.</summary>
    /// <returns>Whether capture was acquired or already owned.</returns>
    internal bool CaptureProbePointer() => CapturePointer();

    /// <summary>Gets whether this probe owns pointer capture.</summary>
    internal bool ProbeHasPointerCapture => HasPointerCapture;

    /// <summary>Gets the value most recently committed through the protected property kernel.</summary>
    internal int KernelValue => _kernelValue;

    /// <summary>Gets or sets a forced pressed fact independent of real pointer/keyboard press
    /// tracking, exercising the protected IsPressedState seam (see #185).</summary>
    internal bool ForcedPressedState { get; set; }

    /// <inheritdoc/>
    protected override bool IsPressedState => ForcedPressedState || base.IsPressedState;

    /// <summary>Gets the local appearance-state flags through the protected resolution seam.</summary>
    internal VisualState ProbeAppearanceState => GetAppearanceState();

    /// <summary>Releases pointer capture only when this probe owns it.</summary>
    internal void ReleaseProbePointer() => ReleasePointerCapture();

    /// <summary>Commits one value through the protected property kernel.</summary>
    /// <param name="value">The replacement value.</param>
    /// <param name="impact">The earliest affected UI phase.</param>
    /// <param name="propertyName">The property name to publish.</param>
    /// <returns>Whether a changed value was committed.</returns>
    internal bool SetKernelValue(
        int value,
        InvalidationImpact impact,
        string? propertyName = nameof(KernelValue)) =>
        SetProperty(ref _kernelValue, value, impact, propertyName);

    /// <summary>Publishes one notification through the protected property kernel.</summary>
    /// <param name="propertyName">The property name to publish.</param>
    /// <param name="impact">The earliest affected UI phase.</param>
    internal void NotifyKernelProperty(string propertyName, InvalidationImpact impact) =>
        NotifyPropertyChanged(propertyName, impact);

    /// <summary>Requests one UI phase through the protected invalidation kernel.</summary>
    /// <param name="impact">The earliest affected UI phase.</param>
    internal void InvalidateKernel(InvalidationImpact impact) => Invalidate(impact);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        MeasureConstraints.Add(constraint);
        Measuring?.Invoke(this);
        return _intrinsic;
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        ArrangeBounds.Add(bounds);
        Arranging?.Invoke(this);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        RenderCalls++;
        Rendering?.Invoke(this);
        _ = canvas.Draw(
            Content.Span,
            new Point(ContentBounds.X, ContentBounds.Y),
            ResolvedStyle);
    }

    /// <inheritdoc/>
    protected override void OnRenderAdornment(TerminalCanvas canvas)
    {
        AdornmentRenderCalls++;
        RenderingAdornment?.Invoke(this);
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        AttachedCalls++;
        AttachedStateWasCommitted = Dispatcher is not null;
    }

    /// <inheritdoc/>
    protected override void OnDetached()
    {
        DetachedCalls++;
        DetachedStateWasCommitted = Dispatcher is null;
    }

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        DisposingCalls++;

        if (ThrowOnDisposing)
        {
            throw new InvalidOperationException("The probe disposal callback failed.");
        }
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        PointerCaptureCancellationCalls++;
        PointerCaptureCancellationReason = reason;
        PointerStateWasClearDuringCancellation =
            !HasPointerCapture && !IsPointerOver && !IsPressed;

        if (RecaptureDuringPointerCancellation)
        {
            RecaptureResult = CapturePointer();
        }

        if (ThrowOnPointerCaptureCancellation)
        {
            throw new InvalidOperationException("The probe capture-cancellation callback failed.");
        }
    }

    /// <inheritdoc/>
    protected override void OnPressedChanged(bool pressed)
    {
        base.OnPressedChanged(pressed);

        if (!pressed && RecaptureWhenPressedClears)
        {
            PressedClearRecaptureResult = CapturePointer();
        }
    }

    /// <summary>Draws one Rune using this control's resolved terminal style.</summary>
    internal void Draw(TerminalCanvas canvas, Rune value)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        _ = canvas.Draw(buffer[..length], new Point(Bounds.X, Bounds.Y), ResolvedStyle);
    }
}
