namespace RedisExplorer;

/// <summary>
/// Key helpers.
/// </summary>
[PublicAPI]
public static class KeyHelpers
{
    /// <summary>
    /// Creates a key from the given parts by formatting them in a part:part:part manner.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <returns>The created key.</returns>
    public static string CreateKey(IEnumerable<string> parts)
        => CreateKeyPrivate(parts);
    
    /// <summary>
    /// Creates a key from the given parts by formatting them in a part:part:part manner.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <returns>The created key.</returns>
    public static string CreateKey(params string[] parts)
        => CreateKeyPrivate(parts);
    
    /// <summary>
    /// Creates a key from the given parts by formatting them in a part:part:part manner.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <returns>The created key.</returns>
    public static string CreateKey(IEnumerable<object> parts)
        => CreateKeyPrivate(parts.Select(x => x.ToString() ?? x.GetType().Name));
    
    /// <summary>
    /// Creates a key from the given parts by formatting them in a part:part:part manner.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <returns>The created key.</returns>
    public static string CreateKey(params object[] parts)
        => CreateKeyPrivate(parts.Select(x => x.ToString() ?? x.GetType().Name));
    
    private static string CreateKeyPrivate(IEnumerable<string> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        return Format(parts);
    }

    private static string Format(IEnumerable<string> parts)
        => string.Join(":", parts);
}
