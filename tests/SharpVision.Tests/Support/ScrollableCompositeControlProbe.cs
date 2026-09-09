// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes the protected scrollable-composite authoring contract for behavioral tests.</summary>
internal sealed class ScrollableCompositeControlProbe: ScrollableCompositeControlBase
{
    /// <summary>Initializes a probe hosting one tall vertically scrollable stack of labeled rows.</summary>
    /// <param name="rowCount">The non-negative number of generated content rows.</param>
    internal ScrollableCompositeControlProbe(int rowCount = 20)
    {
        ScrollableHost = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
        };

        for (var index = 0; index < rowCount; index++)
        {
            ScrollableHost.Children.Add(new ControlText($"Row {index}"));
        }

        InitializeContent(ScrollableHost);
        InitializeScrollableContent(ScrollableHost);
    }

    /// <summary>Gets the private tall content host installed by the constructor.</summary>
    internal Stack ScrollableHost { get; }

    /// <summary>Gets, in commit order, one recorded entry per <see cref="OnScrollHostScrollChanged"/>
    /// invocation and one per forwarded <see cref="ScrollableCompositeControlBase.ScrollChanged"/>
    /// delivery a test subscribes to, so a test can assert their relative order.</summary>
    internal List<string> ScrollEventOrder { get; } = [];

    /// <summary>Attempts a second scrollable-content installation against the already-installed
    /// host, exercising the one-shot guard from outside the class.</summary>
    internal void InitializeScrollableContentAgain() => InitializeScrollableContent(ScrollableHost);

    /// <summary>Invokes the protected <c>HandleScrollKey</c> seam with a synthesized key stroke.</summary>
    /// <param name="code">The key code to synthesize.</param>
    /// <param name="consumeAtBoundary">Forwarded to <c>HandleScrollKey</c>.</param>
    /// <param name="modifiers">The modifiers to synthesize.</param>
    /// <returns>The result <c>HandleScrollKey</c> reports.</returns>
    internal bool RaiseScrollKey(Code code, bool consumeAtBoundary = true, Modifiers modifiers = Modifiers.None) =>
        HandleScrollKey(
            new KeyEventArgs(new Stroke(code, character: null, nativeCode: 0, modifiers, KeyAction.Press)),
            consumeAtBoundary);

    /// <summary>Invokes the protected <c>HandleScrollWheel</c> seam with a synthesized wheel record.</summary>
    /// <param name="wheelX">The horizontal wheel delta.</param>
    /// <param name="wheelY">The vertical wheel delta.</param>
    /// <returns>The result <c>HandleScrollWheel</c> reports.</returns>
    internal bool RaiseScrollWheel(int wheelX, int wheelY) =>
        HandleScrollWheel(new PointerEventArgs(new Pointer(
            cells: default,
            pixels: null,
            Buttons.None,
            PointerAction.Wheel,
            wheelX,
            wheelY,
            Modifiers.None,
            isMotion: false,
            isCellPositionInferred: false)));

    /// <inheritdoc/>
    protected override void OnScrollHostScrollChanged(ScrollChangedEventArgs eventArgs)
    {
        base.OnScrollHostScrollChanged(eventArgs);
        ScrollEventOrder.Add($"hook:{VerticalOffset}");
    }
}
