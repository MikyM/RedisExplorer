//
//  RedisCacheOptions.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using Microsoft.Extensions.Logging;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis.Configuration;
using StackExchange.Redis.Profiling;
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace RedisExplorer;

/// <summary>
/// Configuration options for <see cref="RedisExplorer"/>.
/// </summary>
[PublicAPI]
public class RedisCacheOptions : IOptions<RedisCacheOptions>
{
    /// <summary>
    /// The configuration used to connect to Redis.
    /// </summary>
    public string? Configuration { get; set; }

    /// <summary>
    /// The configuration used to connect to Redis.
    /// This is preferred over Configuration.
    /// </summary>
    public ConfigurationOptions? ConfigurationOptions { get; set; }

    /// <summary>
    /// The configuration used to obtain a Redis connection using a factory method.
    /// </summary>
    public ConnectionMultiplexerFactoryOptions? ConnectionMultiplexerFactoryOptions { get; set; }
    
    /// <summary>
    /// Gets or sets a delegate to create the DistributedLockFactory instance.
    /// </summary>
    public Func<IConnectionMultiplexer,ILoggerFactory,Task<IDistributedLockFactory>> DistributedLockFactory { get; set; }
        = (multiplexer,loggerFactory) => Task.FromResult<IDistributedLockFactory>(new RedisExplorerDistributedLockFactory(RedLockFactory.Create(new List<RedLockMultiplexer>()
            { (ConnectionMultiplexer)multiplexer }, loggerFactory)));

    /// <summary>
    /// The Redis key prefix. Allows partitioning a single backend cache for use with multiple apps/services.
    /// If set, the cache keys are prefixed with this value.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// The Redis profiling session
    /// </summary>
    public Func<ProfilingSession>? ProfilingSession { get; set; }

    RedisCacheOptions IOptions<RedisCacheOptions>.Value => this;

    private bool? _useForceReconnect;
    internal bool UseForceReconnect
    {
        get
        {
            return _useForceReconnect ??= GetDefaultValue();
            static bool GetDefaultValue() =>
                AppContext.TryGetSwitch("Microsoft.AspNetCore.Caching.StackExchangeRedis.UseForceReconnect", out var value) && value;
        }
        set => _useForceReconnect = value;
    }

    internal ConfigurationOptions GetConfiguredOptions(string libSuffix)
    {
        var options = ConfigurationOptions?.Clone() ?? ConfigurationOptions.Parse(Configuration!);

        // we don't want an initially unavailable server to prevent DI creating the service itself
        options.AbortOnConnectFail = false;

        if (!string.IsNullOrWhiteSpace(libSuffix))
        {
            var provider = DefaultOptionsProvider.GetProvider(options.EndPoints);
            options.LibraryName = $"{provider.LibraryName} {libSuffix}";
        }
        return options;
    }
}
