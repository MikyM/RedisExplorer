using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RedLockNet;

namespace RedisExplorer;

/// <summary>
/// Service collection extensions.
/// </summary>
[PublicAPI]
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Redis distributed caching services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="setupAction">An <see cref="Action{RedisCacheOptions}"/> to configure the provided
    /// <see cref="RedisCacheOptions"/>.</param>
    /// <param name="redisExplorerAction">An <see cref="Action{RedisExplorerOptions}"/> to configure the provided
    /// <see cref="RedisExplorerOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddRedisExplorer(this IServiceCollection services, Action<RedisCacheOptions> setupAction, Action<RedisExplorerOptions>? redisExplorerAction = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(setupAction);

        services.AddOptions();

        services.Configure(setupAction);

        if (redisExplorerAction is not null)
        {
            services.AddOptions<RedisExplorerOptions>().Configure(redisExplorerAction);
        }
        else
        {
            services.AddOptions<RedisExplorerOptions>();
        }
        
        services.AddSingleton<ImmutableRedisExplorerOptions>();

        services.AddSingleton<RedisExplorerImpl>();

        services.AddSingleton<IRedisExplorer>(x => x.GetRequiredService<RedisExplorerImpl>());
        
        services.AddSingleton<IDistributedCache>(x => x.GetRequiredService<RedisExplorerImpl>());
        
        services.AddSingleton<IDistributedLockFactory>(x => x.GetRequiredService<RedisExplorerImpl>());
        
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
