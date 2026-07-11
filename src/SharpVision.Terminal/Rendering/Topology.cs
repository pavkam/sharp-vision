namespace SharpVision.Terminal.Rendering;

/// <summary>Combines one cell's connections with its line-family intent.</summary>
internal readonly struct Topology
{
    /// <summary>Initializes one internally validated topology.</summary>
    /// <param name="connections">The non-empty connection mask.</param>
    /// <param name="line">The line family.</param>
    internal Topology(Connections connections, LineStyle line)
    {
        Connections = connections;
        Line = line;
    }

    /// <summary>Gets the connection mask.</summary>
    internal Connections Connections { get; }

    /// <summary>Gets the line family.</summary>
    internal LineStyle Line { get; }
}
