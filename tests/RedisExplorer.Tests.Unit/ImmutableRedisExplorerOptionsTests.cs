using FluentAssertions;
using Microsoft.Extensions.Options;

namespace RedisExplorer.Tests.Unit;

public class ImmutableRedisExplorerOptionsTests
{
    public class Construction
    {
        [Fact]
        public void ShouldBeCreatedCorrectly()
        {
            // Arrange
            var opt = new RedisExplorerOptions();
            var span = TimeSpan.FromHours(1);
            var span2 = TimeSpan.FromDays(1);
            opt.SetAbsoluteExpiration<IRedisExplorer>(span);
            opt.SetSlidingExpiration<RedisExplorer>(span2);
            opt.SetDefaultAbsoluteExpiration(span2);
            opt.SetDefaultSlidingExpiration(span);

            var options = Options.Create(opt);

            // Act
            var immutable = new ImmutableRedisExplorerOptions(options);
        
            // Assert
            immutable.GetEntryOptions<IRedisExplorer>().SlidingExpiration.Should().Be(span);
            immutable.GetEntryOptions<RedisExplorer>().SlidingExpiration.Should().Be(span2);
            immutable.GetEntryOptions<IRedisExplorer>().AbsoluteExpirationRelativeToNow.Should().Be(span);
            immutable.GetEntryOptions<RedisExplorer>().AbsoluteExpirationRelativeToNow.Should().Be(span2);
        }
    }
}
