namespace SharpVision.Layout;

/// <summary>Selects whether one ScrollView bar is hidden, automatic, or permanent.</summary>
public enum ScrollBarVisibility
{
    /// <summary>Suppress the bar without disabling programmatic scrolling.</summary>
    Hidden,

    /// <summary>Show the bar only when content exceeds the candidate viewport.</summary>
    Auto,

    /// <summary>Always reserve and show the bar.</summary>
    Always,
}
