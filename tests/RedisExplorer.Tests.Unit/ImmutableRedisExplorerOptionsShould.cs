using FluentAssertions;
using Microsoft.Extensions.Options;

namespace RedisExplorer.Tests.Unit;

public class ImmutableRedisExplorerOptionsShould
{
    [Fact]
    public void Create_itself_correctly_from_RedisExplorerOptions()
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
    
    [Fact]
    public void Return_correct_CacheEntryOptions()
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
