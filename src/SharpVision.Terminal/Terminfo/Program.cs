// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

/// <summary>
/// Owns one immutable terminal program as its original raw source bytes plus
/// validated compiler operations, or identifies a built-in ANSI intrinsic.
/// </summary>
internal readonly struct Program
{
    private readonly byte[]? _representation;
    private readonly Operation[]? _operations;
    private readonly byte[]? _literals;

    /// <summary>
    /// Initializes an empty program or compiles one owned raw program with the default limits.
    /// </summary>
    /// <param name="representation">The raw terminal-description program bytes to copy and compile.</param>
    /// <exception cref="ArgumentException">The program exceeds a default compilation bound.</exception>
    /// <exception cref="FormatException">The non-empty program is malformed or unsupported.</exception>
    /// <exception cref="NotSupportedException">The program requests hardware padding.</exception>
    public Program(ReadOnlySpan<byte> representation)
    {
        if (representation.IsEmpty)
        {
            _representation = [];
            _operations = null;
            _literals = null;
            IsIntrinsic = false;
            MaximumStackDepth = 0;
            return;
        }

        this = Compiler.Compile(representation, Limits.Default);
    }

    private Program(bool isIntrinsic)
    {
        Debug.Assert(isIntrinsic, "The private constructor creates only intrinsic programs.");
        _representation = null;
        _operations = null;
        _literals = null;
        IsIntrinsic = isIntrinsic;
        MaximumStackDepth = 0;
    }

    /// <summary>Initializes one owned compile-once terminfo program.</summary>
    /// <param name="representation">The original raw terminal-description bytes.</param>
    /// <param name="operations">The validated immutable instructions.</param>
    /// <param name="literals">The owned raw literal-byte pool.</param>
    /// <param name="maximumStackDepth">The maximum evaluation depth established by the compiler.</param>
    public Program(
        ReadOnlySpan<byte> representation,
        ReadOnlySpan<Operation> operations,
        ReadOnlySpan<byte> literals,
        int maximumStackDepth)
    {
        Debug.Assert(!representation.IsEmpty, "Compiled programs have source bytes.");
        Debug.Assert(!operations.IsEmpty, "Compiled programs have instructions.");
        Debug.Assert(maximumStackDepth >= 0, "Compiler stack depth cannot be negative.");

        _representation = representation.ToArray();
        _operations = operations.ToArray();
        _literals = literals.ToArray();
        IsIntrinsic = false;
        MaximumStackDepth = maximumStackDepth;
    }

    /// <summary>
    /// Gets the marker for an operation already implemented by the built-in ANSI
    /// encoder without inventing placeholder compiler bytecode.
    /// </summary>
    public static Program Intrinsic { get; } = new(isIntrinsic: true);

    /// <summary>Gets whether this value identifies a built-in ANSI encoder operation.</summary>
    public bool IsIntrinsic { get; }

    /// <summary>Gets whether this value contains neither raw source nor a built-in operation.</summary>
    public bool IsEmpty => !IsIntrinsic && (_representation is null || _representation.Length == 0);

    /// <summary>Gets read-only access to the owned original raw source bytes.</summary>
    public ReadOnlyMemory<byte> Representation => _representation ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>Gets whether this value contains validated compiler output.</summary>
    public bool IsCompiled => _operations is { Length: > 0 };

    /// <summary>Gets the number of immutable compiled instructions.</summary>
    public int OperationCount => _operations?.Length ?? 0;

    /// <summary>Gets the maximum evaluation-stack depth established during compilation.</summary>
    public int MaximumStackDepth { get; }

    /// <summary>Gets the exact positional-parameter arity referenced by this program.</summary>
    public int ParameterCount
    {
        get
        {
            var count = 0;

            foreach (var operation in Operations.Span)
            {
                if (operation.Code == Operation.PushParameter)
                {
                    count = Math.Max(count, operation.Operand + 1);
                }
                else if (operation.Code == Operation.IncrementParameters)
                {
                    count = Math.Max(count, 2);
                }
            }

            return count;
        }
    }

    /// <summary>Gets whether evaluation can begin without positional parameters.</summary>
    public bool AcceptsNoParameters => ParameterCount == 0;

    /// <summary>
    /// Gets whether compiler metadata proves numeric evaluation completes with
    /// non-empty output for every parameter value of the declared arity.
    /// </summary>
    public bool HasProvablyNonEmptyNumericExpansion
    {
        get
        {
            var producesOutput = false;

            foreach (var operation in Operations.Span)
            {
                if (operation.Code is Operation.JumpIfFalse or Operation.Jump or
                    Operation.Divide or Operation.Modulo or
                    Operation.StringLength or Operation.FormatString)
                {
                    return false;
                }

                producesOutput |= operation.Code switch
                {
                    Operation.Literal => operation.Secondary > 0,
                    Operation.FormatNumber or Operation.FormatCharacter => true,
                    _ => false
                };
            }

            return producesOutput;
        }
    }

    /// <summary>Gets the immutable compiled instructions.</summary>
    public ReadOnlyMemory<Operation> Operations => _operations ?? ReadOnlyMemory<Operation>.Empty;

    /// <summary>Gets the immutable raw literal-byte pool.</summary>
    public ReadOnlyMemory<byte> Literals => _literals ?? ReadOnlyMemory<byte>.Empty;
}
