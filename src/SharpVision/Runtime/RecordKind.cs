namespace SharpVision.Runtime;

/// <summary>Identifies one copied terminal input queue record.</summary>
internal enum RecordKind
{
    /// <summary>A key stroke.</summary>
    Key,

    /// <summary>A text Rune.</summary>
    Text,

    /// <summary>A pointer value.</summary>
    Pointer,

    /// <summary>An owned paste.</summary>
    Paste,

    /// <summary>A terminal focus report.</summary>
    Focus,

    /// <summary>A terminal protocol diagnostic.</summary>
    Diagnostic,

    /// <summary>A typed terminal protocol response.</summary>
    Response,

    /// <summary>An orderly input closure.</summary>
    Closed,

    /// <summary>An input fault.</summary>
    Fault,
}
