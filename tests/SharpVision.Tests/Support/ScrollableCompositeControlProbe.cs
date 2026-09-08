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

    /// <summary>Attempts a second scrollable-content installation against the already-installed
    /// host, exercising the one-shot guard from outside the class.</summary>
    internal void InitializeScrollableContentAgain() => InitializeScrollableContent(ScrollableHost);
}
