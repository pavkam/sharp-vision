// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

/// <summary>Describes one immutable instruction in a compiled terminfo parameter program.</summary>
internal readonly struct Operation
{
    /// <summary>Identifies a raw-literal copy.</summary>
    public const byte Literal = 1;

    /// <summary>Identifies a positional-parameter push.</summary>
    public const byte PushParameter = 2;

    /// <summary>Identifies the ncurses first-two-parameters increment.</summary>
    public const byte IncrementParameters = 3;

    /// <summary>Identifies an integer-constant push.</summary>
    public const byte PushConstant = 4;

    /// <summary>Identifies a variable store.</summary>
    public const byte StoreVariable = 5;

    /// <summary>Identifies a variable load.</summary>
    public const byte LoadVariable = 6;

    /// <summary>Identifies integer addition.</summary>
    public const byte Add = 7;

    /// <summary>Identifies integer subtraction.</summary>
    public const byte Subtract = 8;

    /// <summary>Identifies integer multiplication.</summary>
    public const byte Multiply = 9;

    /// <summary>Identifies integer division.</summary>
    public const byte Divide = 10;

    /// <summary>Identifies integer remainder.</summary>
    public const byte Modulo = 11;

    /// <summary>Identifies bitwise AND.</summary>
    public const byte BitAnd = 12;

    /// <summary>Identifies bitwise OR.</summary>
    public const byte BitOr = 13;

    /// <summary>Identifies bitwise exclusive OR.</summary>
    public const byte BitXor = 14;

    /// <summary>Identifies numeric equality.</summary>
    public const byte Equal = 15;

    /// <summary>Identifies numeric less-than comparison.</summary>
    public const byte LessThan = 16;

    /// <summary>Identifies numeric greater-than comparison.</summary>
    public const byte GreaterThan = 17;

    /// <summary>Identifies logical AND.</summary>
    public const byte LogicalAnd = 18;

    /// <summary>Identifies logical OR.</summary>
    public const byte LogicalOr = 19;

    /// <summary>Identifies logical negation.</summary>
    public const byte LogicalNot = 20;

    /// <summary>Identifies bitwise complement.</summary>
    public const byte BitNot = 21;

    /// <summary>Identifies a conditional jump when the popped value is false.</summary>
    public const byte JumpIfFalse = 22;

    /// <summary>Identifies an unconditional jump.</summary>
    public const byte Jump = 23;

    /// <summary>Identifies formatted numeric output.</summary>
    public const byte FormatNumber = 24;

    /// <summary>Identifies single-byte character output.</summary>
    public const byte FormatCharacter = 25;

    /// <summary>Identifies raw string output.</summary>
    public const byte FormatString = 26;

    /// <summary>Identifies raw string byte-length evaluation.</summary>
    public const byte StringLength = 27;

    /// <summary>Initializes one immutable instruction and its three opcode-specific operands.</summary>
    /// <param name="code">The internal instruction identifier.</param>
    /// <param name="operand">The primary opcode-specific operand.</param>
    /// <param name="secondary">The secondary opcode-specific operand.</param>
    /// <param name="tertiary">The tertiary opcode-specific operand.</param>
    public Operation(byte code, int operand = 0, int secondary = 0, int tertiary = 0)
    {
        Code = code;
        Operand = operand;
        Secondary = secondary;
        Tertiary = tertiary;
    }

    /// <summary>Gets the internal instruction identifier.</summary>
    public byte Code { get; }

    /// <summary>Gets the primary opcode-specific operand.</summary>
    public int Operand { get; }

    /// <summary>Gets the secondary opcode-specific operand.</summary>
    public int Secondary { get; }

    /// <summary>Gets the tertiary opcode-specific operand.</summary>
    public int Tertiary { get; }
}
