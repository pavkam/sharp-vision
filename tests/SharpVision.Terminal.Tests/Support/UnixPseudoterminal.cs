using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Runtime;

namespace SharpVision.Terminal.Tests.Support;

/// <summary>Owns one raw Unix pseudoterminal master/slave pair for integration tests.</summary>
internal sealed partial class UnixPseudoterminal: IAsyncDisposable
{
    private const int _openReadWrite = 2;
    private const int _signalWindowChange = 28;
    private readonly Size? _pixels;
    private readonly FileStream _slave;
    private readonly string _slaveName;
    private FileStream? _master;

    private UnixPseudoterminal(
        FileStream master,
        FileStream slave,
        int slaveDescriptor,
        string slaveName,
        Size? pixels)
    {
        _master = master;
        _slave = slave;
        SlaveDescriptor = slaveDescriptor;
        _slaveName = slaveName;
        _pixels = pixels;
    }

    /// <summary>Gets the raw master stream while it remains open.</summary>
    /// <exception cref="ObjectDisposedException">The master has been closed.</exception>
    internal Stream Master => _master ?? throw new ObjectDisposedException(nameof(Master));

    /// <summary>Gets the owned raw slave stream.</summary>
    internal Stream Slave => _slave;

    /// <summary>Gets the slave descriptor used by terminal ioctl operations.</summary>
    internal int SlaveDescriptor { get; }

    /// <summary>Opens and configures one raw PTY pair.</summary>
    /// <returns>The owned PTY pair.</returns>
    /// <exception cref="PlatformNotSupportedException">The platform is not Linux or macOS.</exception>
    /// <exception cref="IOException">A native PTY or raw-mode operation fails.</exception>
    internal static UnixPseudoterminal Open(Dimensions? dimensions = null)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "Unix pseudoterminals require Linux or macOS.");
        }

        if (OperatingSystem.IsMacOS() && dimensions.HasValue)
        {
            return OpenMac(dimensions.Value);
        }

        var noControllingTerminal = OperatingSystem.IsMacOS() ? 0x0002_0000 : 0x0000_0100;
        var masterDescriptor = PosixOpenPt(_openReadWrite | noControllingTerminal);

        if (masterDescriptor < 0)
        {
            throw NativeFailure("The pseudoterminal master could not be opened.");
        }

        var masterHandle = new SafeFileHandle(masterDescriptor, ownsHandle: true);

        try
        {
            RequireSuccess(GrantPt(masterDescriptor), "The pseudoterminal could not be granted.");
            RequireSuccess(UnlockPt(masterDescriptor), "The pseudoterminal could not be unlocked.");
            var namePointer = PtsName(masterDescriptor);

            if (namePointer == 0)
            {
                throw NativeFailure("The pseudoterminal slave name could not be read.");
            }

            var slaveName = Marshal.PtrToStringUTF8(namePointer)
                ?? throw new IOException("The pseudoterminal slave name was null.");
            var slaveDescriptor = NativeOpen(slaveName, _openReadWrite | noControllingTerminal);

            if (slaveDescriptor < 0)
            {
                throw NativeFailure("The pseudoterminal slave could not be opened.");
            }

            var slaveHandle = new SafeFileHandle(slaveDescriptor, ownsHandle: true);

            try
            {
                // Keep the slave open while changing its line discipline. Darwin
                // resets the settings when the temporary stty descriptor is last.
                ConfigureRaw(slaveName);
                var master = new FileStream(
                    masterHandle,
                    FileAccess.ReadWrite,
                    bufferSize: 4096,
                    isAsync: false);
                var slave = new FileStream(
                    slaveHandle,
                    FileAccess.ReadWrite,
                    bufferSize: 4096,
                    isAsync: false);
                var terminal = new UnixPseudoterminal(
                    master,
                    slave,
                    slaveDescriptor,
                    slaveName,
                    dimensions?.Pixels);

                if (dimensions.HasValue)
                {
                    terminal.SetWindowSize(dimensions.Value);
                }

                return terminal;
            }
            catch
            {
                slaveHandle.Dispose();
                throw;
            }
        }
        catch
        {
            masterHandle.Dispose();
            throw;
        }
    }

    /// <summary>Sets the kernel cell and optional pixel dimensions.</summary>
    /// <param name="value">The positive terminal dimensions.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension exceeds the PTY field size.</exception>
    /// <exception cref="IOException">The resize ioctl fails.</exception>
    internal unsafe void SetWindowSize(Dimensions value)
    {
        if (OperatingSystem.IsMacOS())
        {
            if (value.Pixels != _pixels)
            {
                throw new ArgumentException(
                    "Darwin test resizes must preserve the pixels initialized by openpty.",
                    nameof(value));
            }

            ConfigureSize(_slaveName, value);
            return;
        }

        var window = new WindowSize
        {
            Rows = checked((ushort) value.Cells.Height),
            Columns = checked((ushort) value.Cells.Width),
            PixelWidth = checked((ushort) (value.Pixels?.Width ?? 0)),
            PixelHeight = checked((ushort) (value.Pixels?.Height ?? 0)),
        };
        var request = OperatingSystem.IsMacOS() ? 0x8008_7467u : 0x5414u;

        if (Ioctl(SlaveDescriptor, request, (nint) (&window)) != 0)
        {
            throw NativeFailure("The pseudoterminal dimensions could not be set.");
        }
    }

    /// <summary>Signals the process-level SIGWINCH observer.</summary>
    /// <exception cref="IOException">The signal cannot be delivered.</exception>
    internal void SignalWindowChange()
    {
        ObjectDisposedException.ThrowIf(_slave.SafeFileHandle.IsClosed, this);

        if (Kill(GetProcessId(), _signalWindowChange) != 0)
        {
            throw NativeFailure("SIGWINCH could not be delivered.");
        }
    }

    /// <summary>Closes the master endpoint exactly once.</summary>
    internal async ValueTask CloseMasterAsync()
    {
        var master = Interlocked.Exchange(ref _master, null);

        if (master is not null)
        {
            await master.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Closes both owned descriptors.</summary>
    public async ValueTask DisposeAsync()
    {
        await CloseMasterAsync().ConfigureAwait(false);
        await _slave.DisposeAsync().ConfigureAwait(false);
    }

    private static void ConfigureRaw(string slaveName)
    {
        var start = new ProcessStartInfo("/bin/stty")
        {
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(OperatingSystem.IsMacOS() ? "-f" : "-F");
        start.ArgumentList.Add(slaveName);
        start.ArgumentList.Add("raw");
        start.ArgumentList.Add("-echo");
        using var process = Process.Start(start)
            ?? throw new IOException("The raw-mode utility could not start.");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new IOException($"The pseudoterminal could not enter raw mode: {error}");
        }
    }

    private static void ConfigureSize(string slaveName, Dimensions value)
    {
        var start = new ProcessStartInfo("/bin/stty")
        {
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-f");
        start.ArgumentList.Add(slaveName);
        start.ArgumentList.Add("rows");
        start.ArgumentList.Add(value.Cells.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("columns");
        start.ArgumentList.Add(value.Cells.Width.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var process = Process.Start(start)
            ?? throw new IOException("The resize utility could not start.");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new IOException($"The pseudoterminal could not be resized: {error}");
        }
    }

    private static unsafe UnixPseudoterminal OpenMac(Dimensions dimensions)
    {
        var masterDescriptor = -1;
        var slaveDescriptor = -1;
        Span<byte> name = stackalloc byte[1024];
        var window = CreateWindow(dimensions);

        fixed (byte* namePointer = name)
        {
            if (OpenPty(
                    &masterDescriptor,
                    &slaveDescriptor,
                    namePointer,
                    0,
                    &window) != 0)
            {
                throw NativeFailure("The Darwin pseudoterminal could not be opened.");
            }
        }

        var nameLength = name.IndexOf((byte) 0);

        if (nameLength <= 0)
        {
            throw new IOException("The Darwin pseudoterminal slave name was invalid.");
        }

        var slaveName = System.Text.Encoding.UTF8.GetString(name[..nameLength]);
        var masterHandle = new SafeFileHandle(masterDescriptor, ownsHandle: true);
        var slaveHandle = new SafeFileHandle(slaveDescriptor, ownsHandle: true);

        try
        {
            ConfigureRaw(slaveName);
            var master = new FileStream(
                masterHandle,
                FileAccess.ReadWrite,
                bufferSize: 4096,
                isAsync: false);
            var slave = new FileStream(
                slaveHandle,
                FileAccess.ReadWrite,
                bufferSize: 4096,
                isAsync: false);
            return new UnixPseudoterminal(
                master,
                slave,
                slaveDescriptor,
                slaveName,
                dimensions.Pixels);
        }
        catch
        {
            masterHandle.Dispose();
            slaveHandle.Dispose();
            throw;
        }
    }

    private static WindowSize CreateWindow(Dimensions value) => new()
    {
        Rows = checked((ushort) value.Cells.Height),
        Columns = checked((ushort) value.Cells.Width),
        PixelWidth = checked((ushort) (value.Pixels?.Width ?? 0)),
        PixelHeight = checked((ushort) (value.Pixels?.Height ?? 0)),
    };

    private static void RequireSuccess(int result, string message)
    {
        if (result != 0)
        {
            throw NativeFailure(message);
        }
    }

    private static IOException NativeFailure(string message) =>
        new(message, new Win32Exception(Marshal.GetLastPInvokeError()));

    [LibraryImport("libc", EntryPoint = "posix_openpt", SetLastError = true)]
    private static partial int PosixOpenPt(int flags);

    [LibraryImport("libc", EntryPoint = "grantpt", SetLastError = true)]
    private static partial int GrantPt(int fileDescriptor);

    [LibraryImport("libc", EntryPoint = "unlockpt", SetLastError = true)]
    private static partial int UnlockPt(int fileDescriptor);

    [LibraryImport("libc", EntryPoint = "ptsname", SetLastError = true)]
    private static partial nint PtsName(int fileDescriptor);

    [LibraryImport(
        "libc",
        EntryPoint = "open",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf8)]
    private static partial int NativeOpen(string path, int flags);

    [LibraryImport("libc", EntryPoint = "openpty", SetLastError = true)]
    private static unsafe partial int OpenPty(
        int* master,
        int* slave,
        byte* name,
        nint terminalAttributes,
        WindowSize* window);

    // ioctl is variadic, so the runtime marshaller is intentional here.
    [SuppressMessage(
        "Interoperability",
        "SYSLIB1054:Use LibraryImportAttribute",
        Justification = "The native ioctl function is variadic.")]
    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int Ioctl(int fileDescriptor, nuint request, nint value);

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int Kill(int processId, int signal);

    [LibraryImport("libc", EntryPoint = "getpid")]
    private static partial int GetProcessId();

}
