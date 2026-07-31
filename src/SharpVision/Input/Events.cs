// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Exposes the standard typed terminal-input routed events.</summary>
[PublicAPI]
public static class Events
{
    /// <summary>Gets the routed event for keyboard key-down, key-up, and repeat transitions. Tunnels then bubbles.</summary>
    public static Event<KeyEventArgs> Key { get; } = new("Key", RoutingStrategy.TunnelBubble);

    /// <summary>Gets the routed event for decoded Unicode text input (printable characters). Tunnels then bubbles.</summary>
    public static Event<TextEventArgs> Text { get; } = new("Text", RoutingStrategy.TunnelBubble);

    /// <summary>Gets the routed event for mouse/trackpad input (move, press, release, wheel). Tunnels then bubbles.</summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "Pointer is the conventional terminal input domain term.")]
    public static Event<PointerEventArgs> Pointer { get; } =
        new("Pointer", RoutingStrategy.TunnelBubble);

    /// <summary>Gets the routed event for bracketed-paste content delivered as a single payload. Tunnels then bubbles.</summary>
    public static Event<PasteEventArgs> Paste { get; } = new("Paste", RoutingStrategy.TunnelBubble);

    /// <summary>Gets the routed event for terminal window focus gained/lost transitions. Tunnels then bubbles.</summary>
    public static Event<TerminalFocusEventArgs> TerminalFocusChanged { get; } =
        new("TerminalFocusChanged", RoutingStrategy.TunnelBubble);
}
