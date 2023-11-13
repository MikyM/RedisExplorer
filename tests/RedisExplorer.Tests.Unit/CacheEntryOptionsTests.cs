using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace RedisExplorer.Tests.Unit;

public class CacheEntryOptionsTests
{
    public class ImplicitMemoryOptions
    {
        [Fact]
        public void ShouldBeTranslatedCorrectly()
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
    }

    public class ImplicitDistributedOptions
    {
        [Fact]
        public void ShouldBeTranslatedCorrectly()
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
}
