// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes immutable control-owned appearance overlay registration for behavioral tests.</summary>
internal sealed class AppearanceOverlayProbe: ControlBase
{
    private readonly AppearanceStatesOverlay _overlay;
    private bool _isSelected;

    /// <summary>Initializes a probe with one registered immutable overlay.</summary>
    /// <param name="overlay">The overlay to register.</param>
    internal AppearanceOverlayProbe(AppearanceStatesOverlay overlay)
    {
        _overlay = overlay;
        InitializeAppearanceOverlay(overlay);
    }

    /// <summary>Attempts to register the same overlay a second time.</summary>
    internal void RegisterAgain() => InitializeAppearanceOverlay(_overlay);

    /// <summary>Commits the selected appearance state through the ordinary visual-state seam.</summary>
    internal void CommitSelection(bool value) =>
        _ = SetVisualStateProperty(ref _isSelected, value, nameof(IsSelectedState));

    /// <inheritdoc/>
    protected override bool IsSelectedState => _isSelected;
}
