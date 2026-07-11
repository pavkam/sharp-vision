namespace SharpVision.Input;

/// <summary>Defines how a typed event traverses the control ancestry.</summary>
public enum Strategy
{
    /// <summary>Invokes preview root-to-target, then bubble target-to-root.</summary>
    TunnelBubble,

    /// <summary>Invokes only bubble target-to-root.</summary>
    Bubble,

    /// <summary>Invokes only the target.</summary>
    Direct,
}
