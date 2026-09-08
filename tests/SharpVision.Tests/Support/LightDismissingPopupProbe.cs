// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>
/// A Popup subclass standing in for an externally defined family, proving that
/// <see cref="Popup.ConfigureLightDismiss(PopupLightDismissPolicy)"/> and the public
/// <see cref="PopupLightDismissPolicy"/> constructor are enough, on their own, to opt one Popup
/// subclass into the shared outside-press dismissal - without any access to internal members that
/// only an in-assembly family (Flyout, ContextMenu) could reach.
/// </summary>
internal sealed class LightDismissingPopupProbe: Popup
{
    /// <summary>Initializes a closed probe that dismisses on an outside primary-button press.</summary>
    internal LightDismissingPopupProbe()
    {
        // Auto modal behavior would already dismiss on an outside press through the shared modal
        // boundary, masking whether ConfigureLightDismiss itself is doing the work. None isolates
        // the assertion to the light-dismiss path this probe exists to prove.
        ModalBehavior = PopupModalBehavior.None;
        ConfigureLightDismiss(new PopupLightDismissPolicy(
            includeAnchor: false,
            buttons: Buttons.Primary,
            interceptAtModalBoundary: false,
            dismiss: () => IsOpen = false));
    }
}
