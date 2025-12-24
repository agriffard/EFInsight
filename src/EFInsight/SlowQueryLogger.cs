using System.Data.Common;
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
    private readonly bool _maskParameters;

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
    /// <param name="maskParameters">Whether to mask parameter values in logs for security.</param>
    public SlowQueryLogger(
        int thresholdMs = 100,
        ILogger? logger = null,
        bool captureStackTrace = false,
        Type? contextTypeFilter = null,
        bool maskParameters = false)
    {
        if (thresholdMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(thresholdMs), "Threshold must be non-negative.");
        }

        _thresholdMs = thresholdMs;
        _logger = logger;
        _captureStackTrace = captureStackTrace;
        _contextTypeFilter = contextTypeFilter;
        _maskParameters = maskParameters;
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

    private string GetParameters(DbCommand command)
    {
        if (command.Parameters.Count == 0)
        {
            return "[]";
        }

        var parameters = new List<string>();
        foreach (DbParameter param in command.Parameters)
        {
            var value = _maskParameters ? "***" : param.Value?.ToString() ?? "null";
            parameters.Add($"{param.ParameterName}={value}");
        }

        return $"[{string.Join(", ", parameters)}]";
    }
}
