using RedLockNet;

namespace RedisExplorer;

/// <summary>
/// <see cref="IRedLock"/> extensions.
/// </summary>
[PublicAPI]
public static class RedLockExtensions
{
    /// <summary>
    /// Creates a new instance of <see cref="IDistributedLock"/> from <see cref="IRedLock"/>.
    /// </summary>
    /// <param name="redLock">The <see cref="IRedLock"/> to translate.</param>
    /// <returns>A translated <see cref="IDistributedLock"/>.</returns>
    public static IDistributedLock ToDistributedLock(this IRedLock redLock)
        => RedisExplorerLock.FromRedLock(redLock);
}
