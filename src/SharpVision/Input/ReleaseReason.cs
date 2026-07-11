namespace SharpVision.Input;

/// <summary>Identifies why active pointer interaction was cancelled.</summary>
public enum ReleaseReason
{
    /// <summary>The owned subtree detached.</summary>
    Detached,

    /// <summary>The owned subtree became disabled.</summary>
    Disabled,

    /// <summary>The owned subtree became hidden or collapsed.</summary>
    Hidden,

    /// <summary>The terminal reported focus loss.</summary>
    TerminalFocusLost,

    /// <summary>The owned subtree was disposed.</summary>
    Disposed,
}
