// ReSharper disable once CheckNamespace
namespace RedisExplorer;

/// <summary>
/// Options for GET operations.
/// </summary>
[PublicAPI]
public sealed class GetOperationOptions : ExplorerOperationOptions
{
    /// <summary>
    /// The default options.
    /// </summary>
    public static GetOperationOptions Default { get; } = new();
    
    /// <summary>
    /// Gets whether the operation should also refresh expiration if applicable. Default is true.
    /// </summary>
    public bool ShouldRefresh { get; private set; } = true;

    /// <summary>
    /// Makes the operation also refresh the key while obtaining the value.
    /// </summary>
    /// <param name="shouldRefresh">Whether the operation should refresh the expiration.</param>
    /// <returns>Current instance for chaining.</returns>
    public GetOperationOptions WithoutRefreshing(bool shouldRefresh = false)
    {
        ShouldRefresh = shouldRefresh;
        return this;
    }

    /// <inheritdoc/>
    public override object Clone()
        => new GetOperationOptions
        {
            ShouldRefresh = ShouldRefresh
        };
}
