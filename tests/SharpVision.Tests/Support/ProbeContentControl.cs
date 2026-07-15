// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Records the public single-content role and owns one later-registered private part.</summary>
internal sealed class ProbeContentControl: ContentControl
{
    private readonly OwnedControlSlot _part;

    /// <summary>Initializes the probe and registers one private part after the inherited content slot.</summary>
    internal ProbeContentControl()
    {
        _part = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: false,
                participatesInNavigation: false,
                partKey: "probe-part",
                ChangeImpact.Render),
            capacity: 1);
    }

    /// <summary>Gets committed old/new content pairs observed by the protected callback.</summary>
    internal List<(Control? Previous, Control? Current)> ContentChanges { get; } = [];

    /// <summary>Gets or sets an observer invoked from the protected content-change callback.</summary>
    internal Action<ProbeContentControl, Control?, Control?>? ContentChanging { get; set; }

    /// <summary>Gets or sets whether the protected content-change callback throws.</summary>
    internal bool ThrowOnContentChanged { get; set; }

    /// <summary>Adds one detached control to the private part slot.</summary>
    /// <param name="control">The non-null detached part.</param>
    internal void AddPart(Control control) => _part.Add(control);

    /// <summary>Gets the controls in global slot-registration order.</summary>
    /// <returns>A new identity-preserving snapshot.</returns>
    internal IReadOnlyList<Control> GetOwnedOrder()
    {
        List<Control> result = [];
        VisitChildren(result.Add);
        return result;
    }

    /// <inheritdoc/>
    protected override void OnContentChanged(Control? previous, Control? current)
    {
        ContentChanges.Add((previous, current));
        ContentChanging?.Invoke(this, previous, current);

        if (ThrowOnContentChanged)
        {
            throw new InvalidOperationException("The content callback failed.");
        }
    }
}
