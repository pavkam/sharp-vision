namespace SharpVision.Terminal.Protocols;

/// <summary>Identifies the active bounded parser state.</summary>
internal enum State
{
    /// <summary>Plain text and control processing.</summary>
    Ground,

    /// <summary>An escape introducer was read.</summary>
    Escape,

    /// <summary>An escape intermediate is accumulating.</summary>
    EscapeIntermediate,

    /// <summary>An invalid escape is being discarded.</summary>
    EscapeIgnore,

    /// <summary>A CSI header is accumulating.</summary>
    Csi,

    /// <summary>A CSI intermediate is accumulating.</summary>
    CsiIntermediate,

    /// <summary>An invalid CSI is being discarded.</summary>
    CsiIgnore,

    /// <summary>A DCS header is accumulating.</summary>
    Dcs,

    /// <summary>A DCS intermediate is accumulating.</summary>
    DcsIntermediate,

    /// <summary>An invalid DCS header is being discarded.</summary>
    DcsHeaderIgnore,

    /// <summary>A bounded string payload is accumulating.</summary>
    StringPayload,

    /// <summary>An escape was read inside a string.</summary>
    StringEscape,

    /// <summary>An oversized or invalid string is being discarded.</summary>
    StringIgnore,

    /// <summary>An escape was read while discarding a string.</summary>
    StringIgnoreEscape,
}
