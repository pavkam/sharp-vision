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

/// <summary>
/// Describes the outcome of a clipboard request or decoded reply.
/// </summary>
public enum ClipboardStatus
{
    /// <summary>Clipboard text was decoded successfully.</summary>
    Success,

    /// <summary>The value requests clipboard data rather than carrying it.</summary>
    Query,

    /// <summary>The effective terminal profile has no safe clipboard path.</summary>
    Unavailable,

    /// <summary>The terminal or user denied clipboard access.</summary>
    Denied,

    /// <summary>The reply violated grammar, Base64, UTF-8, or size bounds.</summary>
    Malformed,
}

/// <summary>
/// Contains an immutable OSC 52 clipboard decode result.
/// </summary>
public readonly record struct ClipboardReply
{
    /// <summary>
    /// Initializes a clipboard result from owned data.
    /// </summary>
    /// <param name="status">The decode or operation status.</param>
    /// <param name="selection">The selected clipboard target.</param>
    /// <param name="data">Owned immutable UTF-8 data.</param>
    internal ClipboardReply(
        ClipboardStatus status,
        Selection selection,
        ReadOnlyMemory<byte> data)
    {
        Status = status;
        Selection = selection;
        Data = data;
    }

    /// <summary>Gets the decode or operation status.</summary>
    public ClipboardStatus Status { get; }

    /// <summary>Gets the clipboard target.</summary>
    public Selection Selection { get; }

    /// <summary>Gets owned UTF-8 clipboard bytes, empty for non-success results.</summary>
    public ReadOnlyMemory<byte> Data { get; }
}
