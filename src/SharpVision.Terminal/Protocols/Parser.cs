// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>
/// Parses bounded ECMA-48 terminal sequences across arbitrary input reads.
/// </summary>
/// <remarks>
/// The parser retains only bounded copied sequence state. Text spans may point
/// directly into the current input and every callback span expires when that
/// callback returns. One parser instance is single-threaded and cannot be used
/// recursively.
/// </remarks>
/// <example>
/// <code>
/// using var parser = new Parser();
/// parser.Parse(input, ref sink);
/// parser.Complete(ref sink);
/// </code>
/// </example>
[PublicAPI]
public sealed class Parser: IDisposable
{
    private readonly ParserLimits _limits;
    private byte[]? _parameters;
    private byte[]? _intermediates;
    private byte[]? _payload;
    private State _state;
    private int _parameterLength;
    private int _intermediateLength;
    private int _payloadLength;
    private byte _dcsFinal;
    private SequenceKind _stringKind;
    private DiagnosticCode _pendingCode;
    private SequenceKind _pendingKind;
    private long _pendingOffset;
    private long _discarded;

    /// <summary>
    /// Initializes a parser with conservative or caller-supplied finite limits.
    /// </summary>
    /// <param name="limits">
    /// The immutable limits, or <see langword="null"/> for
    /// <see cref="ParserLimits.Default"/>.
    /// </param>
    public Parser(ParserLimits? limits = null)
    {
        _limits = limits ?? ParserLimits.Default;
        _parameters = ArrayPool<byte>.Shared.Rent(_limits.MaxParameterBytes);

        try
        {
            _intermediates = ArrayPool<byte>.Shared.Rent(_limits.MaxIntermediateBytes);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(_parameters);
            _parameters = null;
            throw;
        }
    }

    /// <summary>
    /// Gets the total number of bytes consumed since construction or reset.
    /// </summary>
    public long Offset { get; private set; }

    /// <summary>Gets whether no terminal sequence is currently pending.</summary>
    /// <exception cref="ObjectDisposedException">The parser is disposed.</exception>
    public bool IsGround
    {
        get
        {
            ThrowIfDisposed();
            return _state == State.Ground;
        }
    }

    /// <summary>
    /// Begins one selectively recognized eight-bit CSI while preserving its
    /// single transport-byte offset.
    /// </summary>
    internal void BeginCsiFromEightBit()
    {
        ThrowIfDisposed();
        Debug.Assert(_state == State.Ground, "Selective CSI recognition begins only from ground.");

        Offset = checked(Offset + 1);
        BeginCsi();
    }

    /// <summary>
    /// Consumes one transport read and synchronously reports parsed events.
    /// </summary>
    /// <typeparam name="TSink">The sink type; structs avoid interface boxing.</typeparam>
    /// <param name="input">Borrowed input bytes.</param>
    /// <param name="sink">The synchronous event sink.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="sink"/> is a null reference.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The parser is disposed.</exception>
    public void Parse<TSink>(ReadOnlySpan<byte> input, ref TSink sink)
        where TSink : ISequenceSink
    {
        ThrowIfDisposed();
        ThrowIfNull(ref sink);

        var position = 0;

        while (position < input.Length)
        {
            if (_state == State.Ground && IsText(input[position]))
            {
                var start = position++;

                while (position < input.Length && IsText(input[position]))
                {
                    position++;
                }

                var text = input[start..position];
                Offset = checked(Offset + text.Length);
                sink.Text(text);
                continue;
            }

            var value = input[position++];
            var currentOffset = Offset;
            Offset = checked(Offset + 1);
            Process(value, currentOffset, ref sink);
        }
    }

    /// <summary>
    /// Completes the stream and reports one truncated sequence when necessary.
    /// </summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="sink">The synchronous event sink.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="sink"/> is a null reference.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The parser is disposed.</exception>
    public void Complete<TSink>(ref TSink sink)
        where TSink : ISequenceSink
    {
        ThrowIfDisposed();
        ThrowIfNull(ref sink);

        if (_state != State.Ground)
        {
            if (IsIgnoring)
            {
                ReportPending(ref sink);
            }
            else
            {
                var diagnostic = new Diagnostic(
                    DiagnosticCode.Truncated,
                    CurrentKind,
                    Offset,
                    0);
                sink.Report(in diagnostic);
            }

            EnterGround();
        }
    }

    /// <summary>
    /// Clears streaming state and restarts the byte offset at zero.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The parser is disposed.</exception>
    public void Reset()
    {
        ThrowIfDisposed();
        EnterGround();
        Offset = 0;
    }

    /// <summary>
    /// Returns rented parser storage. Disposal is idempotent.
    /// </summary>
    public void Dispose()
    {
        var parameters = _parameters;
        var intermediates = _intermediates;
        var payload = _payload;

        if (parameters is null || intermediates is null)
        {
            return;
        }

        _parameters = null;
        _intermediates = null;
        _payload = null;
        ArrayPool<byte>.Shared.Return(parameters, clearArray: true);
        ArrayPool<byte>.Shared.Return(intermediates, clearArray: true);

        if (payload is not null)
        {
            ArrayPool<byte>.Shared.Return(payload, clearArray: true);
        }
    }

    private bool IsIgnoring => _state is
        State.EscapeIgnore or
        State.CsiIgnore or
        State.DcsHeaderIgnore or
        State.StringIgnore or
        State.StringIgnoreEscape;

    private bool IsStringState => _state is
        State.StringPayload or
        State.StringEscape or
        State.StringIgnore or
        State.StringIgnoreEscape;

    private SequenceKind CurrentKind => _state switch
    {
        State.Escape or State.EscapeIntermediate or State.EscapeIgnore =>
            SequenceKind.Escape,
        State.Csi or State.CsiIntermediate or State.CsiIgnore => SequenceKind.Csi,
        State.Dcs or State.DcsIntermediate or State.DcsHeaderIgnore => SequenceKind.Dcs,
        State.StringPayload or State.StringEscape => _stringKind,
        State.StringIgnore or State.StringIgnoreEscape => _pendingKind,
        State.Ground => SequenceKind.None,
        _ => throw new UnreachableException()
    };

    private void BeginCsi()
    {
        ClearPayload();
        ClearHeader();
        _state = State.Csi;
    }

    private void BeginDcs()
    {
        ClearPayload();
        ClearHeader();
        _stringKind = SequenceKind.Dcs;
        _dcsFinal = 0;
        _state = State.Dcs;
    }

    private void BeginString(SequenceKind kind)
    {
        Debug.Assert(
            kind is SequenceKind.Osc or SequenceKind.Apc or SequenceKind.Pm or SequenceKind.Sos,
            "Only terminal string families can enter string payload state.");

        ClearPayload();
        ClearHeader();
        _stringKind = kind;
        _state = State.StringPayload;
    }

    private void BeginIgnore(
        DiagnosticCode code,
        SequenceKind kind,
        long offset,
        State state)
    {
        _pendingCode = code;
        _pendingKind = kind;
        _pendingOffset = offset;
        _discarded = 1;
        _state = state;
    }

    private void ClearHeader()
    {
        _parameterLength = 0;
        _intermediateLength = 0;
        _dcsFinal = 0;
    }

    private void ClearPayload()
    {
        if (_payloadLength > 0)
        {
            _payload.AsSpan(0, _payloadLength).Clear();
            _payloadLength = 0;
        }
    }

    private void EnterGround()
    {
        _state = State.Ground;
        ClearPayload();
        ClearHeader();
        _stringKind = default;
        _pendingCode = default;
        _pendingKind = default;
        _pendingOffset = 0;
        _discarded = 0;
    }

    private void EmitCsi<TSink>(byte final, ref TSink sink)
        where TSink : ISequenceSink
    {
        var parameters = _parameters.AsSpan(0, _parameterLength);
        var intermediates = _intermediates.AsSpan(0, _intermediateLength);
        _state = State.Ground;

        try
        {
            sink.Csi(parameters, intermediates, final);
        }
        finally
        {
            ClearHeader();
        }
    }

    private void EmitEscape<TSink>(byte final, ref TSink sink)
        where TSink : ISequenceSink
    {
        var intermediates = _intermediates.AsSpan(0, _intermediateLength);
        _state = State.Ground;

        try
        {
            sink.Escape(intermediates, final);
        }
        finally
        {
            ClearHeader();
        }
    }

    private void EmitString<TSink>(StringTerminator terminator, ref TSink sink)
        where TSink : ISequenceSink
    {
        var payload = _payload.AsSpan(0, _payloadLength);
        var kind = _stringKind;
        var parameters = _parameters.AsSpan(0, _parameterLength);
        var intermediates = _intermediates.AsSpan(0, _intermediateLength);
        var final = _dcsFinal;
        _state = State.Ground;

        try
        {
            if (kind == SequenceKind.Dcs)
            {
                sink.Dcs(parameters, intermediates, final, payload, terminator);
            }
            else
            {
                sink.Sequence(kind, payload, terminator);
            }
        }
        finally
        {
            ClearPayload();
            ClearHeader();
            _stringKind = default;
        }
    }

    private bool TryAppendPayload(byte value, long currentOffset)
    {
        if (_payloadLength == _limits.MaxStringBytes)
        {
            BeginStringIgnore(DiagnosticCode.StringLimit, currentOffset);

            return false;
        }

        EnsurePayloadCapacity(_payloadLength + 1);
        _payload![_payloadLength++] = value;

        return true;
    }

    private void EnsurePayloadCapacity(int required)
    {
        Debug.Assert(required > 0 && required <= _limits.MaxStringBytes);

        if (_payload is not null && required <= _payload.Length)
        {
            return;
        }

        var size = _payload is null
            ? Math.Min(_limits.MaxStringBytes, Math.Max(256, required))
            : Math.Min(_limits.MaxStringBytes, Math.Max(required, _payload.Length * 2));
        var replacement = ArrayPool<byte>.Shared.Rent(size);

        if (_payload is not null)
        {
            _payload.AsSpan(0, _payloadLength).CopyTo(replacement);
            ArrayPool<byte>.Shared.Return(_payload, clearArray: true);
        }

        _payload = replacement;
    }

    private void BeginStringIgnore(DiagnosticCode code, long currentOffset)
    {
        var kind = _stringKind;
        ClearPayload();
        BeginIgnore(code, kind, currentOffset, State.StringIgnore);
    }

    private bool IsText(byte value) =>
        value is > 0x1f and not 0x7f &&
        (!_limits.AcceptEightBitControls || value is < 0x80 or > 0x9f);

    private void Process<TSink>(byte value, long currentOffset, ref TSink sink)
        where TSink : ISequenceSink
    {
        if (value == 0x7f)
        {
            return;
        }

        if (IsStringState)
        {
            ProcessString(value, currentOffset, ref sink);
            return;
        }

        if (value == ControlBytes.Escape)
        {
            if (IsIgnoring)
            {
                ReportPending(ref sink);
            }

            ClearHeader();
            _state = State.Escape;
            return;
        }

        if (_state != State.Ground && value is ControlBytes.Cancel or ControlBytes.Substitute)
        {
            if (IsIgnoring)
            {
                ReportPending(ref sink);
            }
            else
            {
                var diagnostic = new Diagnostic(
                    DiagnosticCode.Cancelled,
                    CurrentKind,
                    currentOffset,
                    0);
                sink.Report(in diagnostic);
            }

            EnterGround();
            return;
        }

        if (value < 0x20)
        {
            sink.Control(value);
            return;
        }

        switch (_state)
        {
            case State.Ground:
                ProcessGround(value, ref sink);
                break;

            case State.Escape:
            case State.EscapeIntermediate:
                ProcessEscape(value, currentOffset, ref sink);
                break;

            case State.EscapeIgnore:
                ProcessEscapeIgnore(value, ref sink);
                break;

            case State.Csi:
            case State.CsiIntermediate:
                ProcessCsi(value, currentOffset, ref sink);
                break;

            case State.CsiIgnore:
                ProcessCsiIgnore(value, ref sink);
                break;

            case State.Dcs:
            case State.DcsIntermediate:
                ProcessDcs(value, currentOffset, ref sink);
                break;

            case State.DcsHeaderIgnore:
                ProcessDcsHeaderIgnore(value);
                break;

            case State.StringPayload:
            case State.StringEscape:
            case State.StringIgnore:
            case State.StringIgnoreEscape:
                throw new UnreachableException("String states are processed before general controls.");

            default:
                throw new UnreachableException();
        }
    }

    private void ProcessCsi<TSink>(byte value, long currentOffset, ref TSink sink)
        where TSink : ISequenceSink
    {
        if (value is >= 0x30 and <= 0x3f)
        {
            if (_state == State.CsiIntermediate)
            {
                BeginIgnore(DiagnosticCode.Malformed, SequenceKind.Csi, currentOffset, State.CsiIgnore);
            }
            else if (_parameterLength == _limits.MaxParameterBytes)
            {
                BeginIgnore(
                    DiagnosticCode.ParameterLimit,
                    SequenceKind.Csi,
                    currentOffset,
                    State.CsiIgnore);
            }
            else
            {
                _parameters![_parameterLength++] = value;
            }

            return;
        }

        if (value is >= 0x20 and <= 0x2f)
        {
            if (_intermediateLength == _limits.MaxIntermediateBytes)
            {
                BeginIgnore(
                    DiagnosticCode.IntermediateLimit,
                    SequenceKind.Csi,
                    currentOffset,
                    State.CsiIgnore);
            }
            else
            {
                _intermediates![_intermediateLength++] = value;
                _state = State.CsiIntermediate;
            }

            return;
        }

        if (value is >= 0x40 and <= 0x7e)
        {
            EmitCsi(value, ref sink);
            return;
        }

        var diagnostic = new Diagnostic(
            DiagnosticCode.Malformed,
            SequenceKind.Csi,
            currentOffset,
            1);
        sink.Report(in diagnostic);
        EnterGround();
    }

    private void ProcessCsiIgnore<TSink>(byte value, ref TSink sink)
        where TSink : ISequenceSink
    {
        if (value is >= 0x40 and <= 0x7e)
        {
            ReportPending(ref sink);
            EnterGround();
        }
        else
        {
            _discarded++;
        }
    }

    private void ProcessDcs<TSink>(byte value, long currentOffset, ref TSink sink)
        where TSink : ISequenceSink
    {
        if (value is >= 0x30 and <= 0x3f)
        {
            if (_state == State.DcsIntermediate)
            {
                BeginIgnore(
                    DiagnosticCode.Malformed,
                    SequenceKind.Dcs,
                    currentOffset,
                    State.DcsHeaderIgnore);
            }
            else if (_parameterLength == _limits.MaxParameterBytes)
            {
                BeginIgnore(
                    DiagnosticCode.ParameterLimit,
                    SequenceKind.Dcs,
                    currentOffset,
                    State.DcsHeaderIgnore);
            }
            else
            {
                _parameters![_parameterLength++] = value;
            }

            return;
        }

        if (value is >= 0x20 and <= 0x2f)
        {
            if (_intermediateLength == _limits.MaxIntermediateBytes)
            {
                BeginIgnore(
                    DiagnosticCode.IntermediateLimit,
                    SequenceKind.Dcs,
                    currentOffset,
                    State.DcsHeaderIgnore);
            }
            else
            {
                _intermediates![_intermediateLength++] = value;
                _state = State.DcsIntermediate;
            }

            return;
        }

        if (value is >= 0x40 and <= 0x7e)
        {
            _dcsFinal = value;
            _state = State.StringPayload;
            return;
        }

        var diagnostic = new Diagnostic(
            DiagnosticCode.Malformed,
            SequenceKind.Dcs,
            currentOffset,
            1);
        sink.Report(in diagnostic);
        EnterGround();
    }

    private void ProcessDcsHeaderIgnore(byte value)
    {
        if (value is >= 0x40 and <= 0x7e)
        {
            _dcsFinal = value;
            _state = State.StringIgnore;
        }
        else
        {
            _discarded++;
        }
    }

    private void ProcessString<TSink>(byte value, long currentOffset, ref TSink sink)
        where TSink : ISequenceSink
    {
        if (value is ControlBytes.Cancel or ControlBytes.Substitute)
        {
            if (IsIgnoring)
            {
                ReportPending(ref sink);
            }
            else
            {
                var diagnostic = new Diagnostic(
                    DiagnosticCode.Cancelled,
                    CurrentKind,
                    currentOffset,
                    0);
                sink.Report(in diagnostic);
            }

            EnterGround();
            return;
        }

        if (_state is State.StringIgnore or State.StringIgnoreEscape)
        {
            ProcessStringIgnore(value, ref sink);
            return;
        }

        if (_state == State.StringEscape)
        {
            if (value == (byte) '\\')
            {
                EmitString(StringTerminator.EscapeBackslash, ref sink);
                return;
            }

            if (!TryAppendPayload(ControlBytes.Escape, currentOffset))
            {
                ProcessStringIgnore(value, ref sink);
                return;
            }

            if (value == ControlBytes.Escape)
            {
                _state = State.StringEscape;
                return;
            }

            _state = State.StringPayload;
        }

        if (value == ControlBytes.Escape)
        {
            _state = State.StringEscape;
            return;
        }

        if (_limits.AcceptEightBitControls && value == ControlBytes.EightBitSt)
        {
            EmitString(StringTerminator.EightBit, ref sink);
            return;
        }

        if (_stringKind == SequenceKind.Osc && value == ControlBytes.Bell)
        {
            if (_limits.AcceptBellTerminatedOsc)
            {
                EmitString(StringTerminator.Bell, ref sink);
            }
            else
            {
                BeginStringIgnore(DiagnosticCode.Malformed, currentOffset);
            }

            return;
        }

        _ = TryAppendPayload(value, currentOffset);
    }

    private void ProcessStringIgnore<TSink>(byte value, ref TSink sink)
        where TSink : ISequenceSink
    {
        if (_state == State.StringIgnoreEscape)
        {
            if (value == (byte) '\\')
            {
                // The tentative count on entry assumed the ESC might be
                // discarded payload; it instead completed an accepted ST
                // terminator, so it was never actually discarded.
                _discarded--;
                ReportPending(ref sink);
                EnterGround();
                return;
            }

            // The tentatively counted ESC was ordinary payload after all.
            // The current byte gets the same terminator handling any other
            // ignore-state byte would: a fresh ESC starts a new terminator
            // candidate, otherwise BEL/enabled C1 ST can still end recovery
            // here rather than being silently skipped.
            if (value == ControlBytes.Escape)
            {
                _state = State.StringIgnoreEscape;
                _discarded++;
                return;
            }

            _state = State.StringIgnore;

            if ((_limits.AcceptEightBitControls && value == ControlBytes.EightBitSt) ||
                (_pendingKind == SequenceKind.Osc &&
                 _limits.AcceptBellTerminatedOsc &&
                 value == ControlBytes.Bell))
            {
                ReportPending(ref sink);
                EnterGround();
                return;
            }

            _discarded++;
            return;
        }

        if (value == ControlBytes.Escape)
        {
            _state = State.StringIgnoreEscape;
            _discarded++;
            return;
        }

        if ((_limits.AcceptEightBitControls && value == ControlBytes.EightBitSt) ||
            (_pendingKind == SequenceKind.Osc &&
             _limits.AcceptBellTerminatedOsc &&
             value == ControlBytes.Bell))
        {
            ReportPending(ref sink);
            EnterGround();
            return;
        }

        _discarded++;
    }

    private void ProcessEscape<TSink>(byte value, long currentOffset, ref TSink sink)
        where TSink : ISequenceSink
    {
        if (_state == State.Escape)
        {
            switch (value)
            {
                case (byte) '[':
                    BeginCsi();
                    return;

                case (byte) ']':
                    BeginString(SequenceKind.Osc);
                    return;

                case (byte) 'P':
                    BeginDcs();
                    return;

                case (byte) '_':
                    BeginString(SequenceKind.Apc);
                    return;

                case (byte) '^':
                    BeginString(SequenceKind.Pm);
                    return;

                case (byte) 'X':
                    BeginString(SequenceKind.Sos);
                    return;

                default:
                    break;
            }
        }

        if (value is >= 0x20 and <= 0x2f)
        {
            if (_intermediateLength == _limits.MaxIntermediateBytes)
            {
                BeginIgnore(
                    DiagnosticCode.IntermediateLimit,
                    SequenceKind.Escape,
                    currentOffset,
                    State.EscapeIgnore);
            }
            else
            {
                _intermediates![_intermediateLength++] = value;
                _state = State.EscapeIntermediate;
            }

            return;
        }

        if (value is >= 0x30 and <= 0x7e)
        {
            EmitEscape(value, ref sink);
            return;
        }

        var diagnostic = new Diagnostic(
            DiagnosticCode.Malformed,
            SequenceKind.Escape,
            currentOffset,
            1);
        sink.Report(in diagnostic);
        EnterGround();
    }

    private void ProcessEscapeIgnore<TSink>(byte value, ref TSink sink)
        where TSink : ISequenceSink
    {
        if (value is >= 0x30 and <= 0x7e)
        {
            ReportPending(ref sink);
            EnterGround();
        }
        else
        {
            _discarded++;
        }
    }

    private void ProcessGround<TSink>(byte value, ref TSink sink)
        where TSink : ISequenceSink
    {
        Debug.Assert(_limits.AcceptEightBitControls, "Only configured C1 bytes reach ground processing.");

        switch (value)
        {
            case ControlBytes.EightBitCsi:
                BeginCsi();
                break;

            case ControlBytes.EightBitDcs:
                BeginDcs();
                break;

            case ControlBytes.EightBitOsc:
                BeginString(SequenceKind.Osc);
                break;

            case ControlBytes.EightBitApc:
                BeginString(SequenceKind.Apc);
                break;

            case ControlBytes.EightBitPm:
                BeginString(SequenceKind.Pm);
                break;

            case ControlBytes.EightBitSos:
                BeginString(SequenceKind.Sos);
                break;

            default:
                sink.Control(value);
                break;
        }
    }

    private void ReportPending<TSink>(ref TSink sink)
        where TSink : ISequenceSink
    {
        Debug.Assert(IsIgnoring, "A pending diagnostic belongs to an ignore state.");

        var diagnostic = new Diagnostic(
            _pendingCode,
            _pendingKind,
            _pendingOffset,
            _discarded);
        sink.Report(in diagnostic);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_parameters is null, this);

    private static void ThrowIfNull<TSink>(ref TSink sink)
        where TSink : ISequenceSink
    {
        if (!typeof(TSink).IsValueType &&
            Unsafe.As<TSink, object?>(ref sink) is null)
        {
            throw new ArgumentNullException(nameof(sink));
        }
    }
}
