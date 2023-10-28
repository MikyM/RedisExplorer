using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace RedisExplorer.Unit.Tests;

public class CacheEntryOptionsShould
{
    [Fact]
    public void Translate_itself_correctly_to_memory_options()
    {
        // Arrange
        var now = DateTimeOffset.Now;
        var timeSpan = TimeSpan.FromHours(1);
        var own = new CacheEntryOptions
        {
            AbsoluteExpiration = now,
            AbsoluteExpirationRelativeToNow = timeSpan,
            SlidingExpiration = timeSpan
        };

        // Act
        MemoryCacheEntryOptions implicitTrans = own;

        // Assert
        implicitTrans.SlidingExpiration.Should().Be(own.SlidingExpiration);
        implicitTrans.AbsoluteExpirationRelativeToNow.Should().Be(own.AbsoluteExpirationRelativeToNow);
        implicitTrans.AbsoluteExpiration.Should().Be(own.AbsoluteExpiration);
    }
    
    [Fact]
    public void Translate_itself_correctly_to_distributed_options()
    {
        // Arrange
        var now = DateTimeOffset.Now;
        var timeSpan = TimeSpan.FromHours(1);
        var own = new CacheEntryOptions
        {
            AbsoluteExpiration = now,
            AbsoluteExpirationRelativeToNow = timeSpan,
            SlidingExpiration = timeSpan
        };

        // Act
        DistributedCacheEntryOptions implicitTrans = own;

        // Assert
        implicitTrans.SlidingExpiration.Should().Be(own.SlidingExpiration);
        implicitTrans.AbsoluteExpirationRelativeToNow.Should().Be(own.AbsoluteExpirationRelativeToNow);
        implicitTrans.AbsoluteExpiration.Should().Be(own.AbsoluteExpiration);
    }
}
