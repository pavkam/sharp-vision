namespace SharpVision.Terminal.Tests.Support;

/// <summary>Stores one deterministic transport failure and optional written prefix.</summary>
/// <param name="Exception">The exact failure to throw.</param>
/// <param name="PrefixBytes">The maximum prefix to commit before throwing.</param>
internal sealed record Failure(Exception Exception, int PrefixBytes);
