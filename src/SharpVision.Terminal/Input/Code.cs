namespace SharpVision.Terminal.Input;

/// <summary>Identifies a logical keyboard key independently of wire encoding.</summary>
public enum Code
{
    /// <summary>A valid but unmapped native key.</summary>
    Unknown,

    /// <summary>A Unicode character key.</summary>
    Character,

    /// <summary>The Escape key.</summary>
    Escape,

    /// <summary>The Enter key.</summary>
    Enter,

    /// <summary>The Tab key.</summary>
    Tab,

    /// <summary>The Backspace key.</summary>
    Backspace,

    /// <summary>The upward cursor key.</summary>
    Up,

    /// <summary>The downward cursor key.</summary>
    Down,

    /// <summary>The leftward cursor key.</summary>
    Left,

    /// <summary>The rightward cursor key.</summary>
    Right,

    /// <summary>The Home key.</summary>
    Home,

    /// <summary>The End key.</summary>
    End,

    /// <summary>The Insert key.</summary>
    Insert,

    /// <summary>The Delete key.</summary>
    Delete,

    /// <summary>The Page Up key.</summary>
    PageUp,

    /// <summary>The Page Down key.</summary>
    PageDown,

    /// <summary>Function key F1.</summary>
    F1,

    /// <summary>Function key F2.</summary>
    F2,

    /// <summary>Function key F3.</summary>
    F3,

    /// <summary>Function key F4.</summary>
    F4,

    /// <summary>Function key F5.</summary>
    F5,

    /// <summary>Function key F6.</summary>
    F6,

    /// <summary>Function key F7.</summary>
    F7,

    /// <summary>Function key F8.</summary>
    F8,

    /// <summary>Function key F9.</summary>
    F9,

    /// <summary>Function key F10.</summary>
    F10,

    /// <summary>Function key F11.</summary>
    F11,

    /// <summary>Function key F12.</summary>
    F12,
}
