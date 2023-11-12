using JetBrains.Annotations;
using NRedisStack;

namespace RedisExplorer.Stack;

/// <summary>
/// Extensions to <see cref="IRedisExplorer"/> providing Redis Stack commands.
/// </summary>
[PublicAPI]
public static class RedisExplorerExtensions
{
    /// <summary>
    /// Gets <see cref="IJsonCommands"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <returns>An instance of <see cref="IJsonCommands"/>.</returns>
    public static IJsonCommands GetJsonCommands(this IRedisExplorer explorer)
        => new JsonCommands(explorer.GetDatabase());
    
    /// <summary>
    /// Gets <see cref="IJsonCommandsAsync"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <returns>An instance of <see cref="IJsonCommandsAsync"/>.</returns>
    public static IJsonCommandsAsync GetJsonCommandsAsync(this IRedisExplorer explorer)
        => new JsonCommandsAsync(explorer.GetDatabase());

    /// <summary>
    /// Gets <see cref="IBloomCommands"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <returns>An instance of <see cref="IBloomCommands"/>.</returns>
    public static IBloomCommands GetBloomCommands(this IRedisExplorer explorer)
        => new BloomCommands(explorer.GetDatabase());
    
    /// <summary>
    /// Gets <see cref="IBloomCommandsAsync"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <returns>An instance of <see cref="IBloomCommandsAsync"/>.</returns>
    public static IBloomCommandsAsync GetBloomCommandsAsync(this IRedisExplorer explorer)
        => new BloomCommandsAsync(explorer.GetDatabase());
    
    /// <summary>
    /// Gets <see cref="ICmsCommands"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <returns>An instance of <see cref="ICmsCommands"/>.</returns>
    public static ICmsCommands GetCmsCommands(this IRedisExplorer explorer)
        => new CmsCommands(explorer.GetDatabase());

    /// <summary>
    /// Gets <see cref="ICmsCommandsAsync"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <returns>An instance of <see cref="ICmsCommandsAsync"/>.</returns>
    public static ICmsCommandsAsync GetCmsCommandsAsync(this IRedisExplorer explorer)
        => new CmsCommandsAsync(explorer.GetDatabase());

    /// <summary>
    /// Gets <see cref="ICuckooCommands"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <returns>An instance of <see cref="ICuckooCommands"/>.</returns>
    public static ICuckooCommands GetCuckooCommands(this IRedisExplorer explorer)
        => new CuckooCommands(explorer.GetDatabase());

    /// <summary>
    /// Gets <see cref="ICuckooCommandsAsync"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <returns>An instance of <see cref="ICuckooCommandsAsync"/>.</returns>
    public static ICuckooCommandsAsync GetCuckooCommandsAsync(this IRedisExplorer explorer)
        => new CuckooCommandsAsync(explorer.GetDatabase());

    /// <summary>
    /// Gets <see cref="ISearchCommands"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <param name="defaultDialect">The default dialect, if any.</param>
    /// <returns>An instance of <see cref="ISearchCommands"/>.</returns>
    public static ISearchCommands GetSearchCommands(this IRedisExplorer explorer, int? defaultDialect = null)
        => new SearchCommands(explorer.GetDatabase(), defaultDialect);
    
    /// <summary>
    /// Gets <see cref="ISearchCommandsAsync"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <returns>An instance of <see cref="ISearchCommandsAsync"/>.</returns>
    public static ISearchCommandsAsync GetSearchCommandsAsync(this IRedisExplorer explorer)
        => new SearchCommandsAsync(explorer.GetDatabase());

    /// <summary>
    /// Gets <see cref="ITdigestCommands"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <returns>An instance of <see cref="ITdigestCommands"/>.</returns>
    public static ITdigestCommands GetTdigestCommands(this IRedisExplorer explorer)
        => new TdigestCommands(explorer.GetDatabase());

    /// <summary>
    /// Gets <see cref="ITdigestCommandsAsync"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <returns>An instance of <see cref="ITdigestCommandsAsync"/>.</returns>
    public static ITdigestCommandsAsync GetTdigestCommandsAsync(this IRedisExplorer explorer)
        => new TdigestCommandsAsync(explorer.GetDatabase());
}