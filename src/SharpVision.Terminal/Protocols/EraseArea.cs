namespace SharpVision.Terminal.Protocols;

/// <summary>Identifies the portion of a display or line erased by a command.</summary>
public enum EraseArea
{
    /// <summary>Erase from the cursor through the end.</summary>
    After,

    /// <summary>Erase from the beginning through the cursor.</summary>
    Before,

    /// <summary>Erase the complete display or line.</summary>
    All,

    /// <summary>Erase saved scrollback; valid only for display erasure.</summary>
    Scrollback,
}
