namespace RedisExplorer;

/// <summary>
/// Configuration options for th<see cref="RedisExplorer"/>.
/// </summary>
[PublicAPI]
public class ConnectionMultiplexerFactoryOptions
{
    /// <summary>
    /// Creates a new instance of <see cref="ConnectionMultiplexerFactoryOptions"/>.
    /// </summary>
    /// <param name="factory">The factory method.</param>
    /// <param name="isOwned">Whether the instance returned by the factory should be owned by (and disposed with) <see cref="IRedisExplorer"/>.</param>
    public ConnectionMultiplexerFactoryOptions(Func<Task<IConnectionMultiplexer>> factory, bool isOwned = true)
    {
        Factory = factory;
        IsOwned = isOwned;
    }

    /// <summary>
    /// Gets or sets a delegate to create the ConnectionMultiplexer instance.
    /// </summary>
    public Func<Task<IConnectionMultiplexer>> Factory { get; init; }

    /// <summary>
    /// Gets or sets whether the instance returned by the factory should be owned by (and disposed with) <see cref="IRedisExplorer"/>. This defaults to true.
    /// </summary>
    public bool IsOwned { get; init; }
}
