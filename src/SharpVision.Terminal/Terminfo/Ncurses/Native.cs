// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Ncurses;

/// <summary>Owns the ncurses 6 low-level terminfo exports and validates pointer sentinels.</summary>
internal sealed class Native: INative
{
    private readonly SetupTermDelegate _setupTerm;
    private readonly GetIntegerDelegate _getFlag;
    private readonly GetIntegerDelegate _getNumber;
    private readonly GetStringDelegate _getString;
    private readonly SetCurrentTerminalDelegate _setCurrentTerminal;
    private readonly DeleteTerminalDelegate _deleteTerminal;
    private readonly UseExtendedNamesDelegate? _useExtendedNames;
    private bool _disposed;

    /// <summary>Initializes and validates one already-loaded native library handle.</summary>
    /// <param name="handle">The nonzero owned dynamic-library handle.</param>
    /// <exception cref="ArgumentException"><paramref name="handle"/> is zero.</exception>
    /// <exception cref="EntryPointNotFoundException">A required ncurses 6 export is absent.</exception>
    public Native(nint handle)
    {
        if (handle == 0)
        {
            throw new ArgumentException("A native library handle cannot be zero.", nameof(handle));
        }

        Handle = handle;
        CurrentTerminalAddress = ExportAddress(handle, "cur_term");
        _setupTerm = Export<SetupTermDelegate>(handle, "setupterm");
        _getFlag = Export<GetIntegerDelegate>(handle, "tigetflag");
        _getNumber = Export<GetIntegerDelegate>(handle, "tigetnum");
        _getString = Export<GetStringDelegate>(handle, "tigetstr");
        _setCurrentTerminal = Export<SetCurrentTerminalDelegate>(handle, "set_curterm");
        _deleteTerminal = Export<DeleteTerminalDelegate>(handle, "del_curterm");
        _useExtendedNames = TryExport<UseExtendedNamesDelegate>(handle, "use_extended_names");
    }

    /// <inheritdoc/>
    public nint CurrentTerminal
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Marshal.ReadIntPtr(CurrentTerminalAddress);
        }
    }

    /// <inheritdoc/>
    public int Setup(string name, int outputFileDescriptor, out int error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(outputFileDescriptor);

        var namePointer = Marshal.StringToCoTaskMemUTF8(name);
        var errorPointer = Marshal.AllocHGlobal(sizeof(int));

        try
        {
            Marshal.WriteInt32(errorPointer, 0);
            var result = _setupTerm(namePointer, outputFileDescriptor, errorPointer);
            error = Marshal.ReadInt32(errorPointer);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(errorPointer);
            Marshal.FreeCoTaskMem(namePointer);
        }
    }

    /// <inheritdoc/>
    public bool EnableExtendedNames(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _useExtendedNames is not null && _useExtendedNames(enabled ? 1 : 0) != 0;
    }

    /// <inheritdoc/>
    public int GetFlag(string name) => WithName(name, _getFlag);

    /// <inheritdoc/>
    public int GetNumber(string name) => WithName(name, _getNumber);

    /// <inheritdoc/>
    public NativeString GetString(string name, int maximumBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);

        var namePointer = Marshal.StringToCoTaskMemUTF8(name);
        nint result;

        try
        {
            result = _getString(namePointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(namePointer);
        }

        if (result == 0)
        {
            return NativeString.Absent;
        }

        if (result == -1)
        {
            return NativeString.WrongType;
        }

        var length = 0;

        while (length <= maximumBytes && Marshal.ReadByte(result, length) != 0)
        {
            length++;
        }

        if (length > maximumBytes)
        {
            return NativeString.OverLimit;
        }

        var bytes = new byte[length];
        Marshal.Copy(result, bytes, 0, length);
        return NativeString.Present(bytes);
    }

    /// <inheritdoc/>
    public nint SetCurrentTerminal(nint terminal)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _setCurrentTerminal(terminal);
    }

    /// <inheritdoc/>
    public void DeleteTerminal(nint terminal)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_deleteTerminal(terminal) < 0)
        {
            throw new InvalidOperationException("ncurses rejected del_curterm cleanup.");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        NativeLibrary.Free(Handle);
        _disposed = true;
    }

    private nint Handle { get; }

    private nint CurrentTerminalAddress { get; }

    private static T Export<T>(nint handle, string name)
        where T : Delegate
    {
        return NativeLibrary.TryGetExport(handle, name, out var address)
            ? Marshal.GetDelegateForFunctionPointer<T>(address)
            : throw new EntryPointNotFoundException($"The ncurses export '{name}' is unavailable.");
    }

    private static nint ExportAddress(nint handle, string name) =>
        NativeLibrary.TryGetExport(handle, name, out var address)
            ? address
            : throw new EntryPointNotFoundException($"The ncurses export '{name}' is unavailable.");

    private static T? TryExport<T>(nint handle, string name)
        where T : Delegate =>
        NativeLibrary.TryGetExport(handle, name, out var address)
            ? Marshal.GetDelegateForFunctionPointer<T>(address)
            : null;

    private int WithName(string name, GetIntegerDelegate function)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var namePointer = Marshal.StringToCoTaskMemUTF8(name);

        try
        {
            return function(namePointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(namePointer);
        }
    }
}
