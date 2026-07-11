namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies a bounded terminal query response family.</summary>
public enum QueryKind
{
    /// <summary>Primary device attributes.</summary>
    PrimaryAttributes,

    /// <summary>Secondary device attributes.</summary>
    SecondaryAttributes,

    /// <summary>A cursor position report.</summary>
    CursorPosition,

    /// <summary>A DEC private mode report.</summary>
    PrivateMode,

    /// <summary>A default foreground color reply.</summary>
    ForegroundColor,

    /// <summary>A default background color reply.</summary>
    BackgroundColor,

    /// <summary>A correlated Kitty clipboard response.</summary>
    KittyClipboard,

    /// <summary>Current Kitty progressive keyboard flags.</summary>
    Keyboard,
}
