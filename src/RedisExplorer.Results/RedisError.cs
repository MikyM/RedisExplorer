using JetBrains.Annotations;
using Remora.Results;
using StackExchange.Redis;

namespace RedisExplorer.Results;

/// <summary>
/// An error indicating that an operation against Redis is considered a failure.
/// </summary>
/// <param name="Message">The message.</param>
/// <param name="RelatedKey">The key related to the operation, if any.</param>
/// <param name="RelatedResult">The result related to the operation, if any.</param>
/// <param name="RelatedResultType">The result type related to the operation, if any.</param>
/// <param name="Exception">The exception, if any occurred.</param>
/// <param name="ErrorType">The error type.</param>
[PublicAPI]
public sealed record RedisError(string Message, RedisErrorType ErrorType, string? RelatedKey, string? RelatedResult, ResultType? RelatedResultType, Exception? Exception) : ResultError(Message);
