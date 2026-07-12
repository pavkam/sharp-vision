using SharpVision.Runtime;
using SharpVision.Showcase;
using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Transport;

if (Console.IsInputRedirected || Console.IsOutputRedirected)
{
    Console.WriteLine(StartupMessage.Get());
    return;
}

using var rawMode = ConsoleRawMode.Enter();
using var gallery = new Gallery();
using var input = OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
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
var transport = new StreamTransport(
    input,
    Console.OpenStandardOutput(),
    leaveOpen: true);
var resize = new ConsoleResizeSource(TimeSpan.FromMilliseconds(100));
var environment = new Dictionary<string, string?>(StringComparer.Ordinal);

foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
{
    if (entry.Key is string key)
    {
        environment[key] = entry.Value?.ToString();
    }
}

await using var application = new Application(
    gallery.Root,
    transport,
    resize,
    StartupOptions.Create(environment));
application.Started += FocusSelectedNavigation;
using var cancellation = new CancellationTokenSource();

ConsoleCancelEventHandler cancel = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};
Console.CancelKeyPress += cancel;

try
{
    await application.StartAsync(cancellation.Token);
    _ = await Task.WhenAny(
        application.Completion,
        Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token));
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
}
finally
{
    Console.CancelKeyPress -= cancel;
    application.Started -= FocusSelectedNavigation;
    await application.StopAsync();
}

void FocusSelectedNavigation(object? sender, EventArgs eventArgs)
{
    _ = sender;
    _ = eventArgs;
    _ = gallery.FocusSelected(application.Focus);
}
