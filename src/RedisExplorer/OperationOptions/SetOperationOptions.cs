using Microsoft.Extensions.Caching.Distributed;

// ReSharper disable once CheckNamespace
namespace RedisExplorer;

/// <summary>
/// Options for SET operations.
/// </summary>
[PublicAPI]
public sealed class SetOperationOptions : ExplorerOperationOptions
{
    /// <summary>
    /// The default options.
    /// </summary>
    public static SetOperationOptions Default { get; } = new();

    /// <summary>
    /// Gets expiration options.
    /// </summary>
    public DistributedCacheEntryOptions? ExpirationOptions { get; private set; }

    /// <summary>
    /// Gets whether to overwrite the key if it already exists. Default is true as it saves a single Redis call.
    /// </summary>
    public bool OverwriteIfKeyExists { get; private set; } = true;

    /// <summary>
    /// Makes the operation check if the key in question already exists prior to setting, if it does, the operation is cancelled.
    /// </summary>
    /// <remarks>This adds one more Redis call to the script.</remarks>
    /// <param name="shouldOverwrite">Whether the operation should overwrite existing key.</param>
    /// <returns>Current instance for chaining.</returns>
    public SetOperationOptions WithoutKeyOverwriting(bool shouldOverwrite = false)
    {
        OverwriteIfKeyExists = shouldOverwrite;
        return this;
    }

    /// <summary>
    /// Sets expiration options.
    /// </summary>
    /// <param name="expirationOptions">The options to set.</param>
    /// <returns>Current instance for chaining.</returns>
    public SetOperationOptions WithExpirationOptions(DistributedCacheEntryOptions expirationOptions)
    {
        ExpirationOptions = expirationOptions;
        return this;
    }

    /// <inheritdoc/>
    public override object Clone()
        => new SetOperationOptions
        {
            ExpirationOptions = ExpirationOptions,
            OverwriteIfKeyExists = OverwriteIfKeyExists
        };
}
