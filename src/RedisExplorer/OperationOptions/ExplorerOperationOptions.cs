// ReSharper disable once CheckNamespace
namespace RedisExplorer;

/// <summary>
/// Base class for all operation options.
/// </summary>
[PublicAPI]
public abstract class ExplorerOperationOptions : ICloneable
{
    /// <inheritdoc/>
    public abstract object Clone();
}
