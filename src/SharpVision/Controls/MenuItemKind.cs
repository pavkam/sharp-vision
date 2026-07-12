namespace SharpVision.Controls;

/// <summary>Classifies the activation and rendering behavior of one menu item.</summary>
public enum MenuItemKind
{
    /// <summary>Invokes one ordinary command-like item.</summary>
    Command,

    /// <summary>Toggles one independently checked item before invocation.</summary>
    Check,

    /// <summary>Selects one checked item within its containing menu and group name.</summary>
    Radio,

    /// <summary>Draws a non-interactive separator.</summary>
    Separator,
}
