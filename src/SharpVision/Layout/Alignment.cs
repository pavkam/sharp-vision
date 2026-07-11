namespace SharpVision.Layout;

/// <summary>Defines horizontal placement inside an arranged slot.</summary>
public enum HorizontalAlignment
{
    /// <summary>Places content at the left edge.</summary>
    Left,

    /// <summary>Centers content horizontally.</summary>
    Center,

    /// <summary>Places content at the right edge.</summary>
    Right,

    /// <summary>Uses the available horizontal extent when no fixed size overrides it.</summary>
    Stretch,
}

/// <summary>Defines vertical placement inside an arranged slot.</summary>
public enum VerticalAlignment
{
    /// <summary>Places content at the top edge.</summary>
    Top,

    /// <summary>Centers content vertically.</summary>
    Center,

    /// <summary>Places content at the bottom edge.</summary>
    Bottom,

    /// <summary>Uses the available vertical extent when no fixed size overrides it.</summary>
    Stretch,
}
