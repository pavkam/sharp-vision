namespace SharpVision.Terminal.Runtime;

/// <summary>Identifies one terminal mode lease owned by a session.</summary>
internal enum Lease
{
    /// <summary>The alternate-screen lease.</summary>
    AlternateScreen,

    /// <summary>The hidden-cursor lease.</summary>
    Cursor,

    /// <summary>The focus-reporting lease.</summary>
    Focus,

    /// <summary>The bracketed-paste lease.</summary>
    Paste,

    /// <summary>The mouse tracking and coordinate lease.</summary>
    Mouse,

    /// <summary>The Kitty keyboard enhancement lease.</summary>
    Keyboard,
}
