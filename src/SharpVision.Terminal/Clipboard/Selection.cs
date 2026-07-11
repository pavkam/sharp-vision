namespace SharpVision.Terminal.Clipboard;

/// <summary>
/// Identifies one xterm OSC 52 clipboard or selection target.
/// </summary>
public enum Selection
{
    /// <summary>The desktop clipboard, encoded as <c>c</c>.</summary>
    Clipboard,

    /// <summary>The primary selection, encoded as <c>p</c>.</summary>
    Primary,

    /// <summary>The secondary selection, encoded as <c>q</c>.</summary>
    Secondary,

    /// <summary>The select buffer, encoded as <c>s</c>.</summary>
    Select,

    /// <summary>Cut buffer zero.</summary>
    Cut0,

    /// <summary>Cut buffer one.</summary>
    Cut1,

    /// <summary>Cut buffer two.</summary>
    Cut2,

    /// <summary>Cut buffer three.</summary>
    Cut3,

    /// <summary>Cut buffer four.</summary>
    Cut4,

    /// <summary>Cut buffer five.</summary>
    Cut5,

    /// <summary>Cut buffer six.</summary>
    Cut6,

    /// <summary>Cut buffer seven.</summary>
    Cut7,
}
