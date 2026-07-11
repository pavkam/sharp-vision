using System.Buffers;
using System.Diagnostics;

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
public sealed class Parser: IDisposable
{
    private const byte _cancel = 0x18;
    private const byte _escape = 0x1b;
    private const byte _substitute = 0x1a;
    private const byte _eightBitCsi = 0x9b;

    private readonly Limits _limits;
    private byte[]? _parameters;
    private byte[]? _intermediates;
    private State _state;
    private int _parameterLength;
    private int _intermediateLength;
    private DiagnosticCode _pendingCode;
    private SequenceKind _pendingKind;
    private long _pendingOffset;
    private long _discarded;

    /// <summary>
    /// Initializes a parser with conservative or caller-supplied finite limits.
    /// </summary>
    /// <param name="limits">
    /// The immutable limits, or <see langword="null"/> for
    /// <see cref="Limits.Default"/>.
    /// </param>
    public Parser(Limits? limits = null)
    {
        _limits = limits ?? Limits.Default;
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
        ArgumentNullException.ThrowIfNull(sink);

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
        ArgumentNullException.ThrowIfNull(sink);

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

        if (parameters is null || intermediates is null)
        {
            return;
        }

        _parameters = null;
        _intermediates = null;
        ArrayPool<byte>.Shared.Return(parameters, clearArray: true);
        ArrayPool<byte>.Shared.Return(intermediates, clearArray: true);
    }

    private bool IsIgnoring => _state is State.EscapeIgnore or State.CsiIgnore;

    private SequenceKind CurrentKind => _state switch
    {
        State.Escape or State.EscapeIntermediate or State.EscapeIgnore =>
            SequenceKind.Escape,
        State.Csi or State.CsiIntermediate or State.CsiIgnore => SequenceKind.Csi,
        State.Ground => SequenceKind.None,
        _ => throw new UnreachableException(),
    };

    private void BeginCsi()
    {
        ClearHeader();
        _state = State.Csi;
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
    }

    private void EnterGround()
    {
        _state = State.Ground;
        ClearHeader();
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

        if (value == _escape)
        {
            if (IsIgnoring)
            {
                ReportPending(ref sink);
            }

            ClearHeader();
            _state = State.Escape;
            return;
        }

        if (_state != State.Ground && value is _cancel or _substitute)
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

    private void ProcessEscape<TSink>(byte value, long currentOffset, ref TSink sink)
        where TSink : ISequenceSink
    {
        if (_state == State.Escape && value == (byte) '[')
        {
            BeginCsi();
            return;
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

        if (value == _eightBitCsi)
        {
            BeginCsi();
        }
        else
        {
            sink.Control(value);
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

    private enum State
    {
        Ground,
        Escape,
        EscapeIntermediate,
        EscapeIgnore,
        Csi,
        CsiIntermediate,
        CsiIgnore,
    }
}
