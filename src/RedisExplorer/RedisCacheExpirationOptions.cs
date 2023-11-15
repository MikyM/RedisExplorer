//
//  RedisExplorerOptions.cs
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

namespace RedisExplorer;

/// <summary>
/// Holds various settings for individual cache objects.
/// </summary>
[PublicAPI]
public class RedisCacheExpirationOptions : IOptions<RedisCacheExpirationOptions>
{
    private readonly Dictionary<Type, TimeSpan?> _absoluteCacheExpirations = new();
    private readonly Dictionary<Type, TimeSpan?> _slidingCacheExpirations = new();
    private readonly HashSet<Type> _configuredTypes = [];
    private readonly Dictionary<Type, CacheEntryOptions> _cacheEntryOptions = new();
    private CacheEntryOptions _defaultCacheEntryOptions = new();
    private bool _postConfigured;
    
    RedisCacheExpirationOptions IOptions<RedisCacheExpirationOptions>.Value => this;
    
    /// <summary>
    /// Runs post configuration.
    /// </summary>
    public void PostConfigure()
    {
        _defaultCacheEntryOptions = new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DefaultAbsoluteExpiration,
            SlidingExpiration = DefaultSlidingExpiration
        };

        foreach (var type in _configuredTypes)
        {
            _cacheEntryOptions[type] = new CacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    _absoluteCacheExpirations.GetValueOrDefault(type, DefaultAbsoluteExpiration),

                SlidingExpiration =
                    _slidingCacheExpirations.GetValueOrDefault(type, DefaultSlidingExpiration),
            };
        }

        _postConfigured = true;
    }

    /// <summary>
    /// Gets the absolute cache expiration values for various types.
    /// </summary>
    internal IReadOnlyDictionary<Type, TimeSpan?> AbsoluteCacheExpirations => _absoluteCacheExpirations;

    /// <summary>
    /// Gets the sliding cache expiration values for various types.
    /// </summary>
    internal IReadOnlyDictionary<Type, TimeSpan?> SlidingCacheExpirations => _slidingCacheExpirations;

    /// <summary>
    /// Gets the default absolute expiration value relative to now.
    /// </summary>
    public TimeSpan? DefaultAbsoluteExpiration { get; init; }

    /// <summary>
    /// Gets the default sliding expiration value.
    /// </summary>
    public TimeSpan? DefaultSlidingExpiration { get; init; }

    /// <summary>
    /// Gets a set of cache options, with expirations relative to now.
    /// </summary>
    /// <typeparam name="T">The cache entry type.</typeparam>
    /// <returns>The entry options.</returns>
    public CacheEntryOptions GetEntryOptions<T>()
        => _postConfigured 
            ? _cacheEntryOptions.GetValueOrDefault(typeof(T), _defaultCacheEntryOptions)
            : throw new InvalidOperationException("Post configuration has not been run, this method is only available after the options have been fully created.");
    
    /// <summary>
    /// Gets a set of types with custom configured expirations.
    /// </summary>
    internal IReadOnlyCollection<Type> ConfiguredTypes => _configuredTypes;

    /// <summary>
    /// Sets the absolute cache expiration for the given type.
    /// </summary>
    /// <remarks>
    /// This method also sets the expiration time for evicted values to the same value, provided no other expiration
    /// time has already been set.
    /// </remarks>
    /// <param name="absoluteExpiration">
    /// The absolute expiration value. If the value is null, cached values will be kept indefinitely.
    /// </param>
    /// <typeparam name="TCachedType">The cached type.</typeparam>
    /// <returns>The settings.</returns>
    public RedisCacheExpirationOptions SetAbsoluteExpiration<TCachedType>(TimeSpan? absoluteExpiration)
    {
        var added = _configuredTypes.Add(typeof(TCachedType));

        if (!added)
        {
            throw new InvalidOperationException("The absolute expiration for this type has already been set.");
        }
        
        _absoluteCacheExpirations[typeof(TCachedType)] = absoluteExpiration;

        return this;
    }

    /// <summary>
    /// Sets the sliding cache expiration for the given type.
    /// </summary>
    /// <remarks>
    /// This method also sets the expiration time for evicted values to the same value, provided no other expiration
    /// time has already been set.
    /// </remarks>
    /// <param name="slidingExpiration">
    /// The sliding expiration value. If the value is null, cached values will be kept indefinitely.
    /// </param>
    /// <typeparam name="TCachedType">The cached type.</typeparam>
    /// <returns>The settings.</returns>
    public RedisCacheExpirationOptions SetSlidingExpiration<TCachedType>(TimeSpan? slidingExpiration)
    {
        var added = _configuredTypes.Add(typeof(TCachedType));
        
        if (!added)
        {
            throw new InvalidOperationException("The sliding expiration for this type has already been set.");
        }
        
        _slidingCacheExpirations[typeof(TCachedType)] = slidingExpiration;

        return this;
    }
}
