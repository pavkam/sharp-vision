// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Support;


/// <summary>
/// Owns one real Windows ConPTY (pseudo console) and the <c>SharpVision.Terminal.Probe</c>
/// child process attached to it, for integration tests.
/// </summary>
/// <remarks>
/// Unlike <see cref="UnixPseudoterminal"/>, a Windows pseudo console has no "slave" that the
/// current process can simply enter raw mode on: the OS only ever exposes a real per-process
/// console (the thing <see cref="WindowsConsoleMode"/> and <see cref="WindowsConsoleHost"/>
/// operate on) to a process that was created attached to the pseudo console via
/// <c>PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE</c>. This fixture therefore spawns the
/// <c>SharpVision.Terminal.Probe</c> helper (built as a sibling project, resolved from its own
/// build output directory rather than copied next to the test binaries, see
/// <see cref="ResolveProbePath"/> and <c>tests/SharpVision.Terminal.Probe/ConPtyProbe.cs</c>) as
/// that attached process, and exposes
/// the host-side pipe ends as <see cref="Output"/>/<see cref="Input"/> so tests can drive it and
/// observe its plain-text and raw-byte reports.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsPseudoterminal: IAsyncDisposable
{
    private const int _procThreadAttributePseudoconsole = 0x00020016;
    private const uint _extendedStartupInfoPresent = 0x00080000;
    private const int _useStdHandles = 0x00000100;
    private const uint _waitObject0 = 0x00000000;
    private const uint _waitTimeout = 0x00000102;
    private const uint _waitPollMilliseconds = 50;

    private readonly SafeFileHandle _pseudoConsoleInputWrite;
    private readonly SafeFileHandle _pseudoConsoleOutputRead;
    private readonly nint _pseudoConsole;
    private readonly nint _attributeList;
    private readonly SafeProcessHandle _processHandle;
    private FileStream? _input;
    private FileStream? _output;

    private WindowsPseudoterminal(
        SafeFileHandle pseudoConsoleInputWrite,
        SafeFileHandle pseudoConsoleOutputRead,
        nint pseudoConsole,
        nint attributeList,
        SafeProcessHandle processHandle,
        int processId)
    {
        _pseudoConsoleInputWrite = pseudoConsoleInputWrite;
        _pseudoConsoleOutputRead = pseudoConsoleOutputRead;
        _pseudoConsole = pseudoConsole;
        _attributeList = attributeList;
        _processHandle = processHandle;
        ProcessId = processId;
        _input = new FileStream(pseudoConsoleInputWrite, FileAccess.Write, bufferSize: 1, isAsync: false);
        _output = new FileStream(pseudoConsoleOutputRead, FileAccess.Read, bufferSize: 1, isAsync: false);
    }

    /// <summary>Gets the stream writing to the probe process's standard input.</summary>
    internal Stream Input => _input ?? throw new ObjectDisposedException(nameof(Input));

    /// <summary>Gets the stream reading the probe process's standard output.</summary>
    internal Stream Output => _output ?? throw new ObjectDisposedException(nameof(Output));

    /// <summary>Gets the process id of the attached probe process.</summary>
    internal int ProcessId { get; }

    /// <summary>Opens a real pseudo console and attaches a freshly spawned probe process to it.</summary>
    /// <param name="scenarioArguments">
    /// Arguments after the fixed <c>conpty</c> argument, selecting the probe scenario (see
    /// <c>tests/SharpVision.Terminal.Probe/ConPtyProbe.cs</c>).
    /// </param>
    /// <param name="cells">The initial pseudo console cell size.</param>
    /// <returns>The owned pseudo console and attached probe process.</returns>
    /// <exception cref="PlatformNotSupportedException">The platform is not Windows.</exception>
    /// <exception cref="IOException">A native pseudo-console or process-creation call fails.</exception>
    internal static WindowsPseudoterminal Open(string[] scenarioArguments, Size? cells = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows pseudo consoles require Windows.");
        }

        var size = cells ?? new Size(80, 24);

        _ = RequireSuccess(
            CreatePipe(out var inputRead, out var inputWrite, 0, 0),
            "The pseudo-console input pipe could not be created.");

        try
        {
            _ = RequireSuccess(
                CreatePipe(out var outputRead, out var outputWrite, 0, 0),
                "The pseudo-console output pipe could not be created.");

            try
            {
                var hresult = CreatePseudoConsole(
                    new Coord { X = checked((short) size.Width), Y = checked((short) size.Height) },
                    inputRead,
                    outputWrite,
                    dwFlags: 0,
                    out var pseudoConsole);

                if (hresult != 0)
                {
                    throw new IOException(
                        $"CreatePseudoConsole failed with HRESULT 0x{hresult:X8}.",
                        Marshal.GetExceptionForHR(hresult));
                }

                try
                {
                    var (processHandle, processId) = SpawnProbe(pseudoConsole, scenarioArguments, out var attributeList);

                    // The pseudo console duplicated these; the near-side handles the fixture
                    // keeps are inputWrite/outputRead below.
                    inputRead.Dispose();
                    outputWrite.Dispose();

                    return new WindowsPseudoterminal(
                        inputWrite, outputRead, pseudoConsole, attributeList, processHandle, processId);
                }
                catch
                {
                    ClosePseudoConsole(pseudoConsole);
                    throw;
                }
            }
            catch
            {
                outputRead.Dispose();
                outputWrite.Dispose();
                throw;
            }
        }
        catch
        {
            inputRead.Dispose();
            inputWrite.Dispose();
            throw;
        }
    }

    /// <summary>Resizes the pseudo console, which the attached process observes as its console size.</summary>
    /// <param name="cells">The new cell size.</param>
    /// <exception cref="IOException">The native resize call fails.</exception>
    internal void Resize(Size cells)
    {
        var hresult = ResizePseudoConsole(
            _pseudoConsole,
            new Coord { X = checked((short) cells.Width), Y = checked((short) cells.Height) });

        if (hresult != 0)
        {
            throw new IOException(
                $"ResizePseudoConsole failed with HRESULT 0x{hresult:X8}.",
                Marshal.GetExceptionForHR(hresult));
        }
    }

    /// <summary>Reads one newline-terminated UTF-8 status line written by the probe.</summary>
    /// <returns>The line without its terminator, or null at end of stream.</returns>
    internal async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        List<byte> line = [];
        var single = new byte[1];

        while (true)
        {
            var read = await Output.ReadAsync(single, cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                return line.Count > 0 ? Encoding.UTF8.GetString([.. line]) : null;
            }

            if (single[0] == (byte) '\n')
            {
                return Encoding.UTF8.GetString([.. line]);
            }

            if (single[0] != (byte) '\r')
            {
                line.Add(single[0]);
            }
        }
    }

    /// <summary>Waits for the attached probe process to exit.</summary>
    /// <remarks>
    /// Deliberately waits and reads the exit code against the raw <c>hProcess</c> handle
    /// <see cref="SpawnProbe"/> retains from its own <c>CreateProcessW</c> call, instead of going
    /// through <see cref="Process"/>. A <c>Process</c> obtained via
    /// <c>Process.GetProcessById</c> (necessary since the process here is actually started via
    /// the raw <c>CreateProcessW</c> call, not <c>Process.Start()</c>) throws
    /// <see cref="InvalidOperationException"/> ("Process was not started by this object...") from
    /// <c>WaitForExitAsync</c>/<c>ExitCode</c>, and its lazily-opened <c>SafeHandle</c> throws a
    /// *different* "process has exited" exception once the process has actually already exited by
    /// the time it's first accessed — there is no working combination of `Process` members here.
    /// </remarks>
    internal async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = WaitForSingleObject(_processHandle, _waitPollMilliseconds);

            if (result == _waitObject0)
            {
                break;
            }

            if (result != _waitTimeout)
            {
                throw NativeFailure("WaitForSingleObject failed.");
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        return !GetExitCodeProcess(_processHandle, out var exitCode)
            ? throw NativeFailure("GetExitCodeProcess failed.")
            : exitCode;
    }

    /// <summary>Closes every owned handle and the pseudo console.</summary>
    public async ValueTask DisposeAsync()
    {
        var input = Interlocked.Exchange(ref _input, null);
        var output = Interlocked.Exchange(ref _output, null);

        if (input is not null)
        {
            await input.DisposeAsync().ConfigureAwait(false);
        }

        if (output is not null)
        {
            await output.DisposeAsync().ConfigureAwait(false);
        }

        ClosePseudoConsole(_pseudoConsole);
        DeleteProcThreadAttributeList(_attributeList);
        Marshal.FreeHGlobal(_attributeList);

        if (WaitForSingleObject(_processHandle, 0) == _waitTimeout)
        {
            _ = TerminateProcess(_processHandle, unchecked((uint) -1));
        }

        _processHandle.Dispose();
    }

    private static unsafe (SafeProcessHandle Handle, int ProcessId) SpawnProbe(
        nint pseudoConsole, string[] scenarioArguments, out nint attributeList)
    {
        var probePath = ResolveProbePath();
        var commandLine = BuildCommandLine(probePath, scenarioArguments);

        var size = nint.Zero;
        _ = InitializeProcThreadAttributeList(nint.Zero, 1, 0, ref size);
        attributeList = Marshal.AllocHGlobal(size);

        if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
        {
            Marshal.FreeHGlobal(attributeList);
            throw NativeFailure("InitializeProcThreadAttributeList failed.");
        }

        if (!UpdateProcThreadAttribute(
                attributeList,
                0,
                 _procThreadAttributePseudoconsole,
                pseudoConsole,
                 nint.Size,
                nint.Zero,
                nint.Zero))
        {
            DeleteProcThreadAttributeList(attributeList);
            Marshal.FreeHGlobal(attributeList);
            throw NativeFailure("UpdateProcThreadAttribute failed.");
        }

        // dwFlags = STARTF_USESTDHANDLES, hStdInput/Output/Error left null (the struct's default):
        // without this, CreateProcessW's legacy "magically inherit the caller's console" fallback
        // duplicates the *calling* process's standard handles into the pseudo-console-attached
        // child whenever that caller's own handles are redirected (as they are here - the CI test
        // runner's stdout/stderr are piped for log capture) instead of wiring the child to the
        // pseudo console at all. See https://github.com/microsoft/terminal/issues/11276.
        var startupInfo = new StartupInfoEx
        {
            StartupInfo = new StartupInfo
            {
                cb = Marshal.SizeOf<StartupInfoEx>(),
                dwFlags = _useStdHandles
            },
            lpAttributeList = attributeList
        };

        var commandLineBuffer = commandLine.ToCharArray();
        Array.Resize(ref commandLineBuffer, commandLineBuffer.Length + 1);

        fixed (char* commandLinePointer = commandLineBuffer)
        {
            if (!CreateProcessW(
                    null,
                    commandLinePointer,
                    nint.Zero,
                    nint.Zero,
                    bInheritHandles: false,
                    _extendedStartupInfoPresent,
                    nint.Zero,
                    null,
                    ref startupInfo,
                    out var processInformation))
            {
                throw NativeFailure("CreateProcess for the attached probe failed.");
            }

            _ = CloseHandleQuiet(processInformation.hThread);
            return (new SafeProcessHandle(processInformation.hProcess, ownsHandle: true), processInformation.dwProcessId);
        }
    }

    private static string ResolveProbePath()
    {
        var testAssemblyDirectory = Path.GetDirectoryName(typeof(WindowsPseudoterminal).Assembly.Location)
                                     ?? throw new IOException("The test assembly directory could not be resolved.");
        var configuration = new DirectoryInfo(testAssemblyDirectory).Parent?.Name ?? "Debug";
        var probePath = Path.GetFullPath(Path.Combine(
            testAssemblyDirectory,
            "..",
            "..",
            "..",
            "..",
            "SharpVision.Terminal.Probe",
            "bin",
            configuration,
            "net10.0",
            "SharpVision.Terminal.Probe.exe"));

        return !File.Exists(probePath)
            ? throw new FileNotFoundException(
                "The SharpVision.Terminal.Probe executable was not found in its sibling project's build output. " +
                "It is referenced with ReferenceOutputAssembly=false, which builds it as a dependency but does " +
                "not copy it next to the test binaries, so it is located via the sibling project's own output " +
                "path instead (see ProbeRunner.Run's identical pattern).",
                probePath)
            : probePath;
    }

    private static string BuildCommandLine(string probePath, string[] scenarioArguments)
    {
        List<string> parts = [probePath, "conpty", .. scenarioArguments];
        return string.Join(' ', parts.Select(Quote));

        static string Quote(string value) => value.Contains(' ', StringComparison.Ordinal)
            ? $"\"{value}\""
            : value;
    }

    private static bool RequireSuccess(bool result, string message) => !result ? throw NativeFailure(message) : result;

    private static IOException NativeFailure(string message) =>
        new(message, new Win32Exception(Marshal.GetLastPInvokeError()));

    private static bool CloseHandleQuiet(nint handle) => CloseHandle(handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct Coord
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int cb;
        public nint lpReserved;
        public nint lpDesktop;
        public nint lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public nint lpReserved2;
        public nint hStdInput;
        public nint hStdOutput;
        public nint hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public nint lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public nint hProcess;
        public nint hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [LibraryImport("kernel32", EntryPoint = "CreatePipe", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreatePipe(
        out SafeFileHandle readHandle,
        out SafeFileHandle writeHandle,
        nint pipeAttributes,
        uint size);

    [LibraryImport("kernel32", EntryPoint = "CreatePseudoConsole", SetLastError = true)]
    private static partial int CreatePseudoConsole(
        Coord size,
        SafeFileHandle input,
        SafeFileHandle output,
        uint dwFlags,
        out nint pseudoConsole);

    [LibraryImport("kernel32", EntryPoint = "ResizePseudoConsole", SetLastError = true)]
    private static partial int ResizePseudoConsole(nint pseudoConsole, Coord size);

    [LibraryImport("kernel32", EntryPoint = "ClosePseudoConsole", SetLastError = true)]
    private static partial void ClosePseudoConsole(nint pseudoConsole);

    [LibraryImport("kernel32", EntryPoint = "InitializeProcThreadAttributeList", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool InitializeProcThreadAttributeList(
        nint attributeList,
        int attributeCount,
        int flags,
        ref nint size);

    [LibraryImport("kernel32", EntryPoint = "UpdateProcThreadAttribute", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UpdateProcThreadAttribute(
        nint attributeList,
        uint flags,
        nint attribute,
        nint value,
        nint valueSize,
        nint previousValue,
        nint returnSize);

    [LibraryImport("kernel32", EntryPoint = "DeleteProcThreadAttributeList")]
    private static partial void DeleteProcThreadAttributeList(nint attributeList);

    [LibraryImport("kernel32", EntryPoint = "CreateProcessW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool CreateProcessW(
        string? applicationName,
        char* commandLine,
        nint processAttributes,
        nint threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        uint creationFlags,
        nint environment,
        string? currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [LibraryImport("kernel32", EntryPoint = "CloseHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    [LibraryImport("kernel32", EntryPoint = "GetExitCodeProcess", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(SafeHandle process, out int exitCode);

    [LibraryImport("kernel32", EntryPoint = "WaitForSingleObject", SetLastError = true)]
    private static partial uint WaitForSingleObject(SafeHandle handle, uint milliseconds);

    [LibraryImport("kernel32", EntryPoint = "TerminateProcess", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateProcess(SafeHandle process, uint exitCode);
}
