using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// <see cref="RedisCacheExpirationOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddRedisExplorer(this IServiceCollection services, Action<RedisCacheOptions> setupAction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(setupAction);

        services.AddOptions();

        services.AddOptions<RedisCacheOptions>().Configure(setupAction).PostConfigure(x => x.PostConfigure());

        services.AddSingleton<RedisExplorer>();

        services.AddSingleton<IRedisExplorer>(x => x.GetRequiredService<RedisExplorer>());
        
        services.AddSingleton<IDistributedCache>(x => x.GetRequiredService<RedisExplorer>());
        
        services.AddTransient<IDistributedLockFactory>(x => x.GetRequiredService<RedisExplorer>().GetLockFactory());
        
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
