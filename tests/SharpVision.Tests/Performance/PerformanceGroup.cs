namespace SharpVision.Tests.Performance;

/// <summary>Serializes allocation measurements against unrelated test activity.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceGroup
{
    /// <summary>Gets the xUnit collection name used by performance gates.</summary>
    public const string Name = "Performance";
}
