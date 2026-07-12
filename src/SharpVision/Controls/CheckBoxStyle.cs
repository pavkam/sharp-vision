namespace SharpVision.Controls;

/// <summary>Selects the built-in visual mark family used by a CheckBox.</summary>
public enum CheckBoxStyle
{
    /// <summary>Uses the configurable one-cell <see cref="Marks"/> glyphs.</summary>
    Square,

    /// <summary>Uses fixed-width ASCII-compatible bracket marks such as [x].</summary>
    Brackets,

    /// <summary>Uses Unicode circle and checkmark marks.</summary>
    Tick,
}
