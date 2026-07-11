namespace SharpVision.Styling;

/// <summary>Identifies whether one resource change affects render or measurement.</summary>
public enum Impact
{
    /// <summary>Only semantic cell appearance changed.</summary>
    Render,

    /// <summary>Content or box geometry may have changed.</summary>
    Measure,
}
