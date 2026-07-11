using SharpVision.Terminal.Input;

namespace SharpVision.Terminal.Runtime;

/// <summary>Receives ordered terminal input, resize, closure, and fault events.</summary>
public interface ISink: IInputSink
{
    /// <summary>Receives one immutable terminal dimension change.</summary>
    /// <param name="value">The new cell and optional pixel dimensions.</param>
    public void Resize(in Dimensions value);

    /// <summary>Reports orderly terminal input closure once.</summary>
    public void Closed();

    /// <summary>Reports one terminal runtime failure.</summary>
    /// <param name="exception">The non-null original failure.</param>
    public void Fault(Exception exception);
}
