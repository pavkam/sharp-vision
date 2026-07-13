namespace SharpVision.Terminal.Runtime;

using SharpVision.Terminal.Transport;

/// <summary>Opens interactive console streams for a SharpVision application host.</summary>
public static class ConsoleHost
{
    /// <summary>Gets whether standard input and output are attached to an interactive console.</summary>
    public static bool IsInteractive =>
        !Console.IsInputRedirected && !Console.IsOutputRedirected;

    /// <summary>Opens the console input stream used by interactive hosts.</summary>
    /// <returns>A readable stream with one-byte buffering on supported platforms.</returns>
    /// <remarks>
    /// Unix hosts read directly from <c>/dev/tty</c> so escape-prefixed input is
    /// not deferred by canonical standard-input buffering. Windows uses the
    /// standard input stream.
    /// </remarks>
    public static Stream OpenInputStream()
    {
        return OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
            ? new FileStream(
                "/dev/tty",
                new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Options = FileOptions.Asynchronous,
                    Share = FileShare.ReadWrite,
                    BufferSize = 1,
                })
            : Console.OpenStandardInput(bufferSize: 1);
    }

    /// <summary>Opens the console output stream used by interactive hosts.</summary>
    /// <returns>The writable standard output stream.</returns>
    public static Stream OpenOutputStream() => Console.OpenStandardOutput();

    /// <summary>Creates a transport over the interactive console streams.</summary>
    /// <returns>A transport that leaves both streams open for host lifetime.</returns>
    public static StreamTransport CreateTransport() =>
        new(
            OpenInputStream(),
            OpenOutputStream(),
            leaveOpen: true);
}
