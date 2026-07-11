namespace SharpVision.Styling;

/// <summary>Identifies composable visual behavior states.</summary>
[Flags]
public enum State
{
    /// <summary>The base appearance with no active overlay.</summary>
    Normal = 0,

    /// <summary>The pointer is hovering the control.</summary>
    Hovered = 1 << 0,

    /// <summary>The control owns keyboard focus.</summary>
    Focused = 1 << 1,

    /// <summary>The control has a checked or selected value.</summary>
    Checked = 1 << 2,

    /// <summary>An active pointer or keyboard press targets the control.</summary>
    Pressed = 1 << 3,

    /// <summary>The control does not accept behavior input.</summary>
    Disabled = 1 << 4,
}
