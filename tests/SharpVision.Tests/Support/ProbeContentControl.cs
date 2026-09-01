// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Records the public single-content role and owns one later-registered private part.</summary>
internal sealed class ProbeContentControl: ContentControl
{
    private RetainedPropertyOverrideService? _contentOverrides;
    private readonly OwnedControlSlot _part;
    private Visibility _imposedContentVisibility;

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
                InvalidationImpact.Render),
            capacity: 1);
    }

    /// <summary>Gets committed old/new content pairs observed by the protected callback.</summary>
    internal List<(ControlBase? Previous, ControlBase? Current)> ContentChanges { get; } = [];

    /// <summary>Gets or sets an observer invoked from the protected content-change callback.</summary>
    internal Action<ProbeContentControl, ControlBase?, ControlBase?>? ContentChanging { get; set; }

    /// <summary>Gets or sets whether the protected content-change callback throws.</summary>
    internal bool ThrowOnContentChanged { get; set; }

    /// <summary>Adds one detached control to the private part slot.</summary>
    /// <param name="control">The non-null detached part.</param>
    internal void AddPart(ControlBase control) => _part.Add(control);

    /// <summary>Begins a retained visibility override through the inherited content ownership slot.</summary>
    /// <param name="visibility">The live visibility imposed until <see cref="EndContentVisibilityOverride"/>.</param>
    internal void BeginContentVisibilityOverride(Visibility visibility)
    {
        var content = Content ?? throw new InvalidOperationException("Content is required before the override begins.");
        _imposedContentVisibility = visibility;
        _contentOverrides = new RetainedPropertyOverrideService(
            this,
            ContentOwnershipSlot,
            OnAuthoredVisibilityChanged);
        var lease = _contentOverrides.Acquire(content, RetainedPropertyOverrides.Visibility);
        lease.SetLive(RetainedControlProperty.Visibility, visibility);
    }

    /// <summary>Restores the current content's latest authored visibility and retires the test lease.</summary>
    internal void EndContentVisibilityOverride()
    {
        if (_contentOverrides is null)
        {
            return;
        }

        if (Content is { } content)
        {
            _contentOverrides.Restore(content);
        }

        _contentOverrides.Dispose();
        _contentOverrides = null;
    }

    /// <summary>Gets the controls in global slot-registration order.</summary>
    /// <returns>A new identity-preserving snapshot.</returns>
    internal IReadOnlyList<ControlBase> GetOwnedOrder()
    {
        List<ControlBase> result = [];
        VisitChildren(result.Add);
        return result;
    }

    /// <inheritdoc/>
    protected override void OnContentChanged(ControlBase? previous, ControlBase? current)
    {
        if (_contentOverrides is not null)
        {
            if (previous is not null)
            {
                _contentOverrides.Restore(previous);
            }

            if (current is not null)
            {
                var lease = _contentOverrides.Acquire(current, RetainedPropertyOverrides.Visibility);
                lease.SetLive(RetainedControlProperty.Visibility, _imposedContentVisibility);
            }
        }

        ContentChanges.Add((previous, current));
        ContentChanging?.Invoke(this, previous, current);

        if (ThrowOnContentChanged)
        {
            throw new InvalidOperationException("The content callback failed.");
        }
    }

    private void OnAuthoredVisibilityChanged(ControlBase control, RetainedControlProperty property)
    {
        if (property == RetainedControlProperty.Visibility && ReferenceEquals(control, Content))
        {
            _contentOverrides!.Get(control).SetLive(
                RetainedControlProperty.Visibility,
                _imposedContentVisibility);
        }
    }
}
