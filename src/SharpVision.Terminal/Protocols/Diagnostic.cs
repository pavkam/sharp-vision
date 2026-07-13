namespace SharpVision.Terminal.Protocols;

using System.Globalization;

/// <summary>
/// Describes a protocol condition without retaining untrusted or sensitive
/// payload bytes.
/// </summary>
public readonly record struct Diagnostic
{
    /// <summary>
    /// Initializes a non-sensitive structured diagnostic.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="offset"/> or <paramref name="discardedBytes"/> is
    /// negative.
    /// </exception>
    public Diagnostic(
        DiagnosticCode code,
        SequenceKind kind,
        long offset,
        long discardedBytes)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                offset,
                "The stream offset cannot be negative.");
        }

        if (discardedBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discardedBytes),
                discardedBytes,
                "The discarded-byte count cannot be negative.");
        }

        Code = code;
        Kind = kind;
        Offset = offset;
        DiscardedBytes = discardedBytes;
    }

    /// <summary>
    /// Gets the stable diagnostic category.
    /// </summary>
    public DiagnosticCode Code { get; }

    /// <summary>
    /// Gets the sequence family being processed.
    /// </summary>
    public SequenceKind Kind { get; }

    /// <summary>
    /// Gets the zero-based stream byte offset.
    /// </summary>
    public long Offset { get; }

    /// <summary>
    /// Gets the number of bytes discarded during recovery.
    /// </summary>
    public long DiscardedBytes { get; }

    /// <summary>
    /// Returns a culture-independent description containing structural fields
    /// only.
    /// </summary>
    /// <returns>A non-sensitive diagnostic description.</returns>
    public override string ToString()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Code} ({Kind}) at byte {Offset}; discarded {DiscardedBytes} byte(s).");
    }
}
