using System.Diagnostics.CodeAnalysis;

namespace SharpVision.Input;

/// <summary>Exposes the standard typed terminal-input routed events.</summary>
public static class Events
{
    /// <summary>Gets decoded keyboard transitions.</summary>
    public static Event<KeyEventArgs> Key { get; } = new("Key", Strategy.TunnelBubble);

    /// <summary>Gets decoded Unicode text scalars.</summary>
    public static Event<TextEventArgs> Text { get; } = new("Text", Strategy.TunnelBubble);

    /// <summary>Gets decoded cell and pixel pointer input.</summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "Pointer is the conventional terminal input domain term.")]
    public static Event<PointerEventArgs> Pointer { get; } =
        new("Pointer", Strategy.TunnelBubble);

    /// <summary>Gets owned bracketed-paste payloads.</summary>
    public static Event<PasteEventArgs> Paste { get; } = new("Paste", Strategy.TunnelBubble);

    /// <summary>Gets terminal focus transitions.</summary>
    public static Event<FocusEventArgs> Focus { get; } = new("Focus", Strategy.TunnelBubble);
}
