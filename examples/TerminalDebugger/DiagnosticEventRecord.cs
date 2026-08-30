// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Owns one immutable structured event shown by the diagnostic inspector.</summary>
internal sealed class DiagnosticEventRecord
{
    /// <summary>Initializes one owned event record.</summary>
    /// <param name="sequence">The positive session sequence number.</param>
    /// <param name="timestamp">The local event timestamp.</param>
    /// <param name="kind">The decoded event family.</param>
    /// <param name="summary">The non-empty concise summary.</param>
    /// <param name="explanation">The non-empty plain-language explanation.</param>
    /// <param name="fields">The non-null fields to copy.</param>
    /// <exception cref="ArgumentOutOfRangeException">An enum or sequence value is invalid.</exception>
    /// <exception cref="ArgumentException">A text value is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="fields"/> is null.</exception>
    internal DiagnosticEventRecord(
        long sequence,
        DateTimeOffset timestamp,
        DiagnosticEventKind kind,
        string summary,
        string explanation,
        IReadOnlyList<DiagnosticField> fields)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentOutOfRangeException.ThrowIfNotDefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        ArgumentNullException.ThrowIfNull(fields);

        Sequence = sequence;
        Timestamp = timestamp;
        Kind = kind;
        Summary = summary;
        Explanation = explanation;
        Fields = Array.AsReadOnly(fields.ToArray());
    }

    /// <summary>Gets the positive session sequence number.</summary>
    internal long Sequence { get; }

    /// <summary>Gets the local event timestamp.</summary>
    internal DateTimeOffset Timestamp { get; }

    /// <summary>Gets the decoded event family.</summary>
    internal DiagnosticEventKind Kind { get; }

    /// <summary>Gets the concise event summary.</summary>
    internal string Summary { get; }

    /// <summary>Gets the event explanation.</summary>
    internal string Explanation { get; }

    /// <summary>Gets the immutable structured fields.</summary>
    internal IReadOnlyList<DiagnosticField> Fields { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{Sequence,4}  {Timestamp:HH:mm:ss.fff}  {Kind,-9} {Summary}";
}
