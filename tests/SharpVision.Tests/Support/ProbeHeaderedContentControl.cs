// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Records the public header ownership role added on top of the single-content role.</summary>
internal sealed class ProbeHeaderedContentControl: HeaderedContentControl
{
    /// <summary>Gets committed old/new header pairs observed by the protected callback.</summary>
    internal List<(ControlBase? Previous, ControlBase? Current)> HeaderChanges { get; } = [];

    /// <summary>Gets or sets an observer invoked from the protected header-change callback.</summary>
    internal Action<ProbeHeaderedContentControl, ControlBase?, ControlBase?>? HeaderChanging { get; set; }

    /// <inheritdoc/>
    protected override void OnHeaderChanged(ControlBase? previous, ControlBase? current)
    {
        HeaderChanges.Add((previous, current));
        HeaderChanging?.Invoke(this, previous, current);
    }

    // Minimal stacked layout: this probe exists to exercise Header/HeaderText property mechanics,
    // not to prove out a real layout algorithm, so the override just needs to be a plausible
    // implementation of the abstract contract HeaderedContentControl now enforces.
    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var headerSize = Header is { } header && header.Visibility != Visibility.Collapsed
            ? MeasureChild(header, constraint)
            : default;
        var contentSize = Content is { } content && content.Visibility != Visibility.Collapsed
            ? MeasureChild(content, constraint)
            : default;

        return new Size(
            Math.Max(headerSize.Width, contentSize.Width),
            headerSize.Height.SaturatingAdd(contentSize.Height));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Header is { } header)
        {
            ArrangeChild(header, bounds, ResolvedAxes.Both);
        }

        if (Content is { } content)
        {
            ArrangeChild(content, bounds, ResolvedAxes.Both);
        }
    }
}
