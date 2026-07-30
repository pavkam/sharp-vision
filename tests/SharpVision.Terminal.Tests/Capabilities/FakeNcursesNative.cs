// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;
/// <summary>Provides deterministic ncurses return values and lifecycle observations.</summary>
internal sealed class FakeNcursesNative: INative
{
    private readonly Dictionary<string, int> _flags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _numbers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NativeString> _strings = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the terminal active before setup.</summary>
    public nint CurrentTerminal
    {
        get => CurrentTerminalException is { } exception ? throw exception : field;
        set;
    } = 41;

    /// <summary>Gets or sets an exception raised while reading the active terminal.</summary>
    internal Exception? CurrentTerminalException { get; set; }

    /// <summary>Gets or sets the terminal installed by setup.</summary>
    internal nint LoadedTerminal { get; set; } = 73;

    /// <summary>Gets or sets the setup return status.</summary>
    internal int SetupStatus { get; set; }

    /// <summary>Gets or sets the setup error result.</summary>
    internal int SetupError { get; set; } = 1;

    /// <summary>Gets or sets whether setup installs <see cref="LoadedTerminal"/>.</summary>
    internal bool SetupChangesTerminal { get; set; } = true;

    /// <summary>Gets the number of native setup attempts.</summary>
    internal int SetupCalls { get; private set; }

    /// <summary>Gets the number of capability reads.</summary>
    internal int CapabilityReads { get; private set; }

    /// <summary>Gets the terminal passed to restoration.</summary>
    internal nint RestoredTerminal { get; private set; }

    /// <summary>Gets the terminal released during cleanup.</summary>
    internal nint DeletedTerminal { get; private set; }

    /// <summary>Gets whether disposal occurred.</summary>
    internal bool IsDisposed { get; private set; }

    /// <summary>Gets or sets the process-global extended-name state before loading.</summary>
    internal bool ExtendedNames { get; set; }

    /// <summary>Gets the sequence of requested extended-name states.</summary>
    internal List<bool> ExtendedNameChanges { get; } = [];

    /// <summary>Gets or sets an exception raised while reading a capability.</summary>
    internal Exception? ReadException { get; set; }

    /// <summary>Gets or sets an exception raised while deleting the loaded terminal.</summary>
    internal Exception? DeleteException { get; set; }

    /// <summary>Gets or sets an exception raised after setup changes native state.</summary>
    internal Exception? SetupException { get; set; }

    /// <summary>Gets or sets an exception raised while restoring the previous terminal.</summary>
    internal Exception? RestoreException { get; set; }

    /// <summary>Gets or sets an exception raised while restoring extended-name state.</summary>
    internal Exception? ExtendedRestoreException { get; set; }

    /// <summary>Gets or sets an exception raised while disposing the library boundary.</summary>
    internal Exception? DisposeException { get; set; }

    /// <summary>Gets or sets an unexpected pointer returned from terminal restoration.</summary>
    internal nint? RestoreReturn { get; set; }

    /// <summary>Sets one raw Boolean result.</summary>
    internal void SetFlag(string name, int value) => _flags[name] = value;

    /// <summary>Sets one raw numeric result.</summary>
    internal void SetNumber(string name, int value) => _numbers[name] = value;

    /// <summary>Sets one owned string result.</summary>
    internal void SetString(string name, NativeString value) => _strings[name] = value;

    /// <inheritdoc/>
    public int Setup(string name, int outputFileDescriptor, out int error)
    {
        SetupCalls++;
        error = SetupError;

        if (SetupChangesTerminal)
        {
            CurrentTerminal = LoadedTerminal;
        }

        return SetupException is not null ? throw SetupException : SetupStatus;
    }

    /// <inheritdoc/>
    public bool EnableExtendedNames(bool enabled)
    {
        if (!enabled && ExtendedRestoreException is not null)
        {
            throw ExtendedRestoreException;
        }

        var previous = ExtendedNames;
        ExtendedNames = enabled;
        ExtendedNameChanges.Add(enabled);
        return previous;
    }

    /// <inheritdoc/>
    public int GetFlag(string name)
    {
        CapabilityReads++;
        ThrowIfRequested();
        return _flags.GetValueOrDefault(name, 0);
    }

    /// <inheritdoc/>
    public int GetNumber(string name)
    {
        CapabilityReads++;
        ThrowIfRequested();
        return _numbers.GetValueOrDefault(name, -1);
    }

    /// <inheritdoc/>
    public NativeString GetString(string name, int maximumBytes)
    {
        CapabilityReads++;
        ThrowIfRequested();
        return _strings.GetValueOrDefault(name, NativeString.Absent);
    }

    /// <inheritdoc/>
    public nint SetCurrentTerminal(nint terminal)
    {
        if (RestoreException is not null)
        {
            throw RestoreException;
        }

        var replaced = CurrentTerminal;
        RestoredTerminal = terminal;
        CurrentTerminal = terminal;
        return RestoreReturn ?? replaced;
    }

    /// <inheritdoc/>
    public void DeleteTerminal(nint terminal)
    {
        if (DeleteException is not null)
        {
            throw DeleteException;
        }

        DeletedTerminal = terminal;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (DisposeException is not null)
        {
            throw DisposeException;
        }

        IsDisposed = true;
    }

    private void ThrowIfRequested()
    {
        if (ReadException is not null)
        {
            throw ReadException;
        }
    }
}
