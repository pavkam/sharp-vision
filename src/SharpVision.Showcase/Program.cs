using SharpVision.Runtime;
using SharpVision.Showcase;
using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Transport;

if (Console.IsInputRedirected || Console.IsOutputRedirected)
{
    Console.WriteLine(StartupMessage.Get());
    return;
}

using var gallery = new Gallery();
var transport = new StreamTransport(
    Console.OpenStandardInput(),
    Console.OpenStandardOutput(),
    leaveOpen: true);
var resize = new ConsoleResizeSource(TimeSpan.FromMilliseconds(100));
await using var application = new Application(gallery.Root, transport, resize);
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
    await application.StopAsync();
}
