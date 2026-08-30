// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Owns the bounded, dispatcher-affine session event history.</summary>
internal sealed class DiagnosticEventLog
{
    private const int _capacity = 500;
    private readonly List<DiagnosticEventRecord> _records = [];
    private readonly ReadOnlyCollection<DiagnosticEventRecord> _view;
    private long _nextSequence = 1;

    /// <summary>Initializes an empty event log.</summary>
    internal DiagnosticEventLog() => _view = _records.AsReadOnly();

    /// <summary>Raised after visible records or pause state change.</summary>
    internal event EventHandler? Changed;

    /// <summary>Gets the oldest-first immutable view of retained records.</summary>
    internal IReadOnlyList<DiagnosticEventRecord> Records => _view;

    /// <summary>Gets or sets whether incoming records are ignored.</summary>
    internal bool IsPaused
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Adds an owned record unless capture is paused.</summary>
    /// <param name="kind">The decoded event family.</param>
    /// <param name="summary">The concise non-empty summary.</param>
    /// <param name="explanation">The non-empty explanation.</param>
    /// <param name="fields">The non-null fields copied into the record.</param>
    internal void Add(
        DiagnosticEventKind kind,
        string summary,
        string explanation,
        IReadOnlyList<DiagnosticField> fields)
    {
        if (IsPaused)
        {
            return;
        }

        var record = new DiagnosticEventRecord(
            _nextSequence++,
            DateTimeOffset.Now,
            kind,
            summary,
            explanation,
            fields);
        _records.Add(record);

        if (_records.Count > _capacity)
        {
            _records.RemoveAt(0);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clears retained records without reusing sequence numbers.</summary>
    internal void Clear()
    {
        if (_records.Count == 0)
        {
            return;
        }

        _records.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
