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
    /// Whether to use bandwidth optimization when using a proxy.
    /// </summary>
    public bool UseBandwidthOptimizationForProxies { get; init; } = true;
    
    /// <summary>
    /// Gets or sets the connection options.
    /// </summary>
    public RedisCacheConnectionOptions ConnectionOptions { get; init; } = new("localhost");
    
    /// <summary>
    /// Gets or sets the expiration options.
    /// </summary>
    public RedisCacheExpirationOptions ExpirationOptions { get; init; } = new();

    /// <summary>
    /// The Redis key prefix. Allows partitioning a single backend cache for use with multiple apps/services.
    /// If init, the cache keys are prefixed with this value.
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// The Redis profiling session
    /// </summary>
    public Func<ProfilingSession>? ProfilingSession { get; init; }

    RedisCacheOptions IOptions<RedisCacheOptions>.Value => this;

    /// <summary>
    /// Runs post configuration.
    /// </summary>
    public void PostConfigure()
    {
        ExpirationOptions.PostConfigure();
        ConnectionOptions.PostConfigure();
    }
}
