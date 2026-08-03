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
}
