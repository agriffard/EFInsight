using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EFInsight;

/// <summary>
/// An EF Core interceptor that logs queries exceeding a configurable threshold.
/// </summary>
public class SlowQueryLogger : DbCommandInterceptor
{
    private readonly int _thresholdMs;
    private readonly ILogger? _logger;
    private readonly bool _captureStackTrace;
    private readonly Type? _contextTypeFilter;

    /// <summary>
    /// Optional callback invoked when a slow query is detected.
    /// </summary>
    public Action<DbCommand, TimeSpan>? OnSlowQuery { get; set; }

    /// <summary>
    /// Creates a new instance of SlowQueryLogger.
    /// </summary>
    /// <param name="thresholdMs">Duration in milliseconds before a query is considered slow.</param>
    /// <param name="logger">Optional ILogger for structured logging.</param>
    /// <param name="captureStackTrace">Whether to capture stack trace for slow queries.</param>
    /// <param name="contextTypeFilter">Optional DbContext type to filter queries by context.</param>
    public SlowQueryLogger(
        int thresholdMs = 100,
        ILogger? logger = null,
        bool captureStackTrace = false,
        Type? contextTypeFilter = null)
    {
        if (thresholdMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(thresholdMs), "Threshold must be non-negative.");
        }

        _thresholdMs = thresholdMs;
        _logger = logger;
        _captureStackTrace = captureStackTrace;
        _contextTypeFilter = contextTypeFilter;
    }

    /// <summary>
    /// Called when a reader is about to be executed (sync).
    /// </summary>
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        command.SetStartTime();
        return base.ReaderExecuting(command, eventData, result);
    }

    /// <summary>
    /// Called when a reader is about to be executed (async).
    /// </summary>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        command.SetStartTime();
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Called after a reader has been executed (sync).
    /// </summary>
    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        CheckForSlowQuery(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    /// <summary>
    /// Called after a reader has been executed (async).
    /// </summary>
    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        CheckForSlowQuery(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Called when a scalar command is about to be executed (sync).
    /// </summary>
    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        command.SetStartTime();
        return base.ScalarExecuting(command, eventData, result);
    }

    /// <summary>
    /// Called when a scalar command is about to be executed (async).
    /// </summary>
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        command.SetStartTime();
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Called after a scalar command has been executed (sync).
    /// </summary>
    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        CheckForSlowQuery(command, eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    /// <summary>
    /// Called after a scalar command has been executed (async).
    /// </summary>
    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        CheckForSlowQuery(command, eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Called when a non-query command is about to be executed (sync).
    /// </summary>
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        command.SetStartTime();
        return base.NonQueryExecuting(command, eventData, result);
    }

    /// <summary>
    /// Called when a non-query command is about to be executed (async).
    /// </summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        command.SetStartTime();
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Called after a non-query command has been executed (sync).
    /// </summary>
    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        CheckForSlowQuery(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    /// <summary>
    /// Called after a non-query command has been executed (async).
    /// </summary>
    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        CheckForSlowQuery(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void CheckForSlowQuery(DbCommand command, CommandExecutedEventData eventData)
    {
        // Apply context type filter if specified
        if (_contextTypeFilter != null && eventData.Context != null)
        {
            if (eventData.Context.GetType() != _contextTypeFilter)
            {
                LogContextFiltered(command);
                return;
            }
        }

        var duration = eventData.Duration;

        if (duration.TotalMilliseconds > _thresholdMs)
        {
            LogSlowQuery(command, duration);
            OnSlowQuery?.Invoke(command, duration);
        }
    }

    private void LogSlowQuery(DbCommand command, TimeSpan duration)
    {
        if (_logger == null)
        {
            return;
        }

        var parameters = GetParameters(command);
        var stackTrace = _captureStackTrace ? Environment.StackTrace : null;

        _logger.LogWarning(
            "EFQL001: Slow query detected. Duration: {Duration}ms, SQL: {Sql}, Parameters: {Parameters}{StackTrace}",
            duration.TotalMilliseconds,
            command.CommandText,
            parameters,
            stackTrace != null ? $", StackTrace: {stackTrace}" : string.Empty);
    }

    private void LogContextFiltered(DbCommand command)
    {
        if (_logger == null)
        {
            return;
        }

        _logger.LogDebug(
            "EFQL003: Query filtered by context type. SQL: {Sql}",
            command.CommandText);
    }

    private static string GetParameters(DbCommand command)
    {
        if (command.Parameters.Count == 0)
        {
            return "[]";
        }

        var parameters = new List<string>();
        foreach (DbParameter param in command.Parameters)
        {
            parameters.Add($"{param.ParameterName}={param.Value}");
        }

        return $"[{string.Join(", ", parameters)}]";
    }
}

/// <summary>
/// Extension methods for DbCommand to store timing information.
/// </summary>
internal static class DbCommandExtensions
{
    private static readonly Dictionary<int, long> StartTimes = new();
    private static readonly object Lock = new();

    public static void SetStartTime(this DbCommand command)
    {
        lock (Lock)
        {
            StartTimes[command.GetHashCode()] = Stopwatch.GetTimestamp();
        }
    }

    public static TimeSpan GetElapsedTime(this DbCommand command)
    {
        lock (Lock)
        {
            if (StartTimes.TryGetValue(command.GetHashCode(), out var startTicks))
            {
                StartTimes.Remove(command.GetHashCode());
                return Stopwatch.GetElapsedTime(startTicks);
            }
        }

        return TimeSpan.Zero;
    }
}
