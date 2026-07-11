using System.Diagnostics.CodeAnalysis;

using SharpVision.Controls;
using SharpVision.Terminal.Input;

namespace SharpVision.Input;

/// <summary>Provides controlled source, phase, and handled state for one route.</summary>
public abstract class RoutedEventArgs: EventArgs
{
    /// <summary>Gets the control that started the current or most recent route.</summary>
    public Control? OriginalSource { get; private set; }

    /// <summary>Gets the current logical source, which may be explicitly retargeted.</summary>
    public Control? Source { get; private set; }

    /// <summary>Gets the active or most recent route phase.</summary>
    public Phase Phase { get; internal set; }

    /// <summary>Gets or sets whether ordinary handling and default behavior should stop.</summary>
    public bool Handled { get; set; }

    private bool IsRouting { get; set; }

    /// <summary>Retargets the logical source without changing the original source.</summary>
    /// <param name="source">The non-null replacement logical source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="InvalidOperationException">No route is currently active.</exception>
    public void Retarget(Control source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!IsRouting)
        {
            throw new InvalidOperationException("Source can be retargeted only during routing.");
        }

        Source = source;
    }

    /// <summary>Begins one route and resets per-route mutable state.</summary>
    internal void Begin(Control source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (IsRouting)
        {
            throw new InvalidOperationException("Event arguments cannot be routed recursively.");
        }

        IsRouting = true;
        OriginalSource = source;
        Source = source;
        Phase = Phase.Preview;
        Handled = false;
    }

    /// <summary>Ends the active route while preserving observable final values.</summary>
    internal void End() => IsRouting = false;
}

/// <summary>Provides an immutable decoded keyboard transition.</summary>
/// <param name="stroke">The decoded keyboard transition.</param>
public sealed class KeyEventArgs(Stroke stroke): RoutedEventArgs
{
    /// <summary>Gets the decoded keyboard transition.</summary>
    public Stroke Stroke { get; } = stroke;
}

/// <summary>Provides one immutable Unicode text scalar.</summary>
/// <param name="text">The decoded text scalar.</param>
public sealed class TextEventArgs(Text text): RoutedEventArgs
{
    /// <summary>Gets the decoded text scalar.</summary>
    public Text Text { get; } = text;
}

/// <summary>Provides immutable cell and optional pixel pointer input.</summary>
/// <param name="pointer">The decoded pointer value.</param>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Pointer is the conventional terminal input domain term.")]
public sealed class PointerEventArgs(Pointer pointer): RoutedEventArgs
{
    /// <summary>Gets the decoded pointer value.</summary>
    public Pointer Pointer { get; } = pointer;
}

/// <summary>Provides an owned immutable bracketed-paste payload.</summary>
public sealed class PasteEventArgs: RoutedEventArgs
{
    /// <summary>Initializes paste event arguments.</summary>
    /// <param name="paste">The non-null owned paste payload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="paste"/> is null.</exception>
    public PasteEventArgs(Paste paste)
    {
        ArgumentNullException.ThrowIfNull(paste);
        Paste = paste;
    }

    /// <summary>Gets the owned paste payload.</summary>
    public Paste Paste { get; }
}

/// <summary>Provides an immutable terminal focus transition.</summary>
/// <param name="focus">The decoded focus transition.</param>
public sealed class FocusEventArgs(Focus focus): RoutedEventArgs
{
    /// <summary>Gets the decoded focus transition.</summary>
    public Focus Focus { get; } = focus;
}
