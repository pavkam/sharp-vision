namespace SharpVision.Terminal.Clipboard;

/// <summary>Describes how one packet affected a Kitty clipboard transaction.</summary>
public enum KittyAcceptResult
{
    /// <summary>The matching packet advanced the transaction.</summary>
    Accepted,

    /// <summary>The matching packet completed the transaction.</summary>
    Completed,

    /// <summary>The matching packet failed the transaction.</summary>
    Failed,

    /// <summary>The packet was late, unrelated, or arrived after a terminal state.</summary>
    Ignored,
}
