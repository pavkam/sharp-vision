// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>An <see cref="InputBase"/> derivative that enables an owned popup plus press
/// activation over a plain list-like container - mirroring <see cref="ComboBox"/>'s
/// capability combination without its selection semantics.</summary>
internal sealed class PopupListInputProbe: InputBase
{
    /// <summary>Initializes a probe whose popup wraps one focusable content child.</summary>
    internal PopupListInputProbe()
    {
        Content = new ProbeContainer();
        Item = new ProbeControl { IsFocusable = true };
        Content.Children.Add(Item);
        Popup = EnablePopup(Content, focusOnOpen: false);
        EnablePressActivation();
    }

    /// <summary>Gets the popup's owned container content.</summary>
    internal ProbeContainer Content { get; }

    /// <summary>Gets the focusable child inside <see cref="Content"/>.</summary>
    internal ProbeControl Item { get; }

    /// <summary>Gets the constructed, owned popup.</summary>
    internal Popup Popup { get; }

    /// <summary>Gets completed activation causes in commit order.</summary>
    internal List<ActivationCause> Activations { get; } = [];

    /// <summary>Gets the number of times the popup opened.</summary>
    internal int DropDownOpenedCount { get; private set; }

    /// <summary>Gets the number of times the popup closed.</summary>
    internal int DropDownClosedCount { get; private set; }

    /// <summary>Gets or sets whether the owned popup is open.</summary>
    internal new bool IsOpen
    {
        get => base.IsOpen;
        set => base.IsOpen = value;
    }

    /// <summary>Attempts to enable the popup capability a second time.</summary>
    internal void EnablePopupAgain() => _ = EnablePopup(Content);

    /// <summary>Accepts and closes the active popup session through the protected seam.</summary>
    internal void ProbeAcceptPopupAndClose() => AcceptPopupAndClose();

    /// <summary>Resolves the shared drop-down disclosure glyph through the protected seam.</summary>
    internal Rune ProbeResolveDropDownGlyph(Rune fallback) => ResolveDropDownGlyph(fallback);

    /// <summary>Gets the shared drop-down indicator cell width through the protected seam.</summary>
    internal static int ProbeDropDownIndicatorWidth => DropDownIndicatorWidth;

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        Activations.Add(cause);
        IsOpen = !IsOpen;
    }

    /// <inheritdoc/>
    protected override void OnDropDownOpened() => DropDownOpenedCount++;

    /// <inheritdoc/>
    protected override void OnDropDownClosed() => DropDownClosedCount++;

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }
}
