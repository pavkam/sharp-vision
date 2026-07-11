using System.Text;

namespace SharpVision.Terminal.Input;

/// <summary>Represents one immutable logical keyboard transition.</summary>
public readonly record struct Stroke
{
    /// <summary>Initializes a validated keyboard transition.</summary>
    /// <param name="code">The logical key.</param>
    /// <param name="character">The character for <see cref="Code.Character"/>.</param>
    /// <param name="nativeCode">The non-negative native numeric code, or zero.</param>
    /// <param name="modifiers">The active modifiers.</param>
    /// <param name="action">The key transition.</param>
    /// <exception cref="ArgumentException">
    /// Character presence does not agree with <paramref name="code"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An enum or native code value is invalid.
    /// </exception>
    public Stroke(
        Code code,
        Rune? character,
        int nativeCode,
        Modifiers modifiers,
        Action action)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "The logical code is unknown.");
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "The key action is unknown.");
        }

        if ((modifiers & ~_allModifiers) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modifiers),
                modifiers,
                "The modifier set contains unknown flags.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(nativeCode);

        if (code == Code.Character != character.HasValue)
        {
            throw new ArgumentException(
                "Only a character code must carry a Unicode scalar.",
                nameof(character));
        }

        Code = code;
        Character = character;
        NativeCode = nativeCode;
        Modifiers = modifiers;
        Action = action;
    }

    /// <summary>Gets every currently defined modifier flag.</summary>
    private const Modifiers _allModifiers =
        Modifiers.Shift |
        Modifiers.Alt |
        Modifiers.Control |
        Modifiers.Super |
        Modifiers.Hyper |
        Modifiers.Meta |
        Modifiers.CapsLock |
        Modifiers.NumLock;

    /// <summary>Gets the logical key.</summary>
    public Code Code { get; }

    /// <summary>Gets the character for <see cref="Code.Character"/>.</summary>
    public Rune? Character { get; }

    /// <summary>Gets the native numeric code, or zero when absent.</summary>
    public int NativeCode { get; }

    /// <summary>Gets active modifiers.</summary>
    public Modifiers Modifiers { get; }

    /// <summary>Gets the transition kind.</summary>
    public Action Action { get; }
}
