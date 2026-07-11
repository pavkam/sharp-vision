namespace SharpVision.Input;

/// <summary>Provides the non-generic routing strategy contract.</summary>
internal interface IEvent
{
    /// <summary>Gets the ancestry traversal strategy.</summary>
    public Strategy Strategy { get; }
}
