# RedisExplorer

[![Build Status](https://github.com/MikyM/RedisExplorer/actions/workflows/release.yml/badge.svg)](https://github.com/MikyM/RedisExplorer/actions)

A meta library attempting to offer extended Redis-related features and optimizations to other implementations (such as `Microsoft.Extensions.Caching.StackExchangeRedis`).

Heavily inspired by / re-uses:
- uses https://github.com/StackExchange/StackExchange.Redis
- uses https://github.com/samcook/RedLock.net
- uses https://github.com/redis/NRedisStack
- re-uses parts of code from `Microsoft.Extensions.Caching.StackExchangeRedis`
- re-uses parts of code from https://github.com/Remora/Remora.Discord/ and it's caching implementations/abstractions/configuration

## Features

- Locks via https://github.com/samcook/RedLock.net
- Configurable caching settings per type based on https://github.com/Remora/Remora.Discord/
- Redis stack support via https://github.com/redis/NRedisStack
- `IDistributedCache` implementation based on Microsoft's with a bunch of tweaks, especially to atomic get and refresh operation now doing expiration math server-side with a single round-trip
- Additional methods that will handle de/serialization automatically based on configured `JsonSerializerSettings`

## Description

Library was mainly created to optimize get and refresh operations done by `Microsoft.Extensions.Caching.StackExchangeRedis` plus connect a few other libraries together.

The main service implements `IDistributedCache` and registers itself as such with the DI container (and as it's own interface - `IRedisExplorer`).

Access to the underlying services is provided through appropriate methods on `IRedisExplorer` - `GetDatabase`, `GetMultiplexer` and their async versions - should you need to do something 'unusual'.

## Installation

To register the services use the extension method on `IServiceCollection` (it's similar to the one provided by `Microsoft.Extensions.Caching.StackExchangeRedis`):

```csharp
services.AddRedisExplorer(redisConnectionSetupAction, redisExplorerSetupAction);
```

These will overwrite any other implementation of `IDistributedCache` currently registered with the container.

If you wish to configure the `JsonSerializerOptions` used for de/serializing:
```csharp
services.Configure<JsonSerializerOptions>(RedisExplorer.JsonOptionsName, yourOptions);
```

## Documentation

Documentation available at https://redis-explorer.mikym.me/.