namespace SharpVision.Terminal.Input;

/// <summary>Identifies pointer movement, buttons, scrolling, and leave.</summary>
public enum PointerAction
{
    /// <summary>The pointer moved without a button transition.</summary>
    Move,

    /// <summary>A button was pressed.</summary>
    Press,

    /// <summary>A button was released.</summary>
    Release,

    /// <summary>A wheel changed.</summary>
    Wheel,

    /// <summary>The pointer left the tracked surface.</summary>
    Leave,
}
