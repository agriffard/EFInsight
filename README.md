# EFInsight

A lightweight library for detecting, logging, and analyzing slow or problematic EF Core queries.

## Features

- 🔍 **Threshold-based detection** - Detect queries exceeding a configurable duration threshold
- 📝 **Structured logging** - Log SQL, duration, and parameters using `ILogger`
- ⚡ **Sync & Async support** - Works with both `ToList()` and `ToListAsync()`
- 🎯 **DbContext filtering** - Filter queries by specific DbContext types
- 🔧 **Custom callbacks** - Execute custom logic when slow queries are detected
- 📍 **Stack trace capture** - Optional stack trace for debugging
- 🔒 **Parameter masking** - Option to mask parameter values for security

## Installation

```bash
dotnet add package EFInsight
```

## Quick Start

```csharp
// Create the logger
var slowQueryLogger = new SlowQueryLogger(
    thresholdMs: 100,           // Queries longer than 100ms are considered slow
    logger: loggerFactory.CreateLogger<SlowQueryLogger>(),
    captureStackTrace: false    // Optional: capture stack trace for debugging
);

// Add to your DbContext
services.AddDbContext<MyDbContext>(options =>
    options.UseSqlServer(connectionString)
           .AddInterceptors(slowQueryLogger));
```

## Configuration Options

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `thresholdMs` | `int` | `100` | Duration in milliseconds before a query is considered slow |
| `logger` | `ILogger` | `null` | Logger instance for structured logging |
| `captureStackTrace` | `bool` | `false` | Whether to capture stack trace for slow queries |
| `contextTypeFilter` | `Type` | `null` | Filter queries by specific DbContext type |
| `maskParameters` | `bool` | `false` | Whether to mask parameter values in logs for security |

## Custom Callback

You can execute custom logic when a slow query is detected:

```csharp
var slowQueryLogger = new SlowQueryLogger(thresholdMs: 100);
slowQueryLogger.OnSlowQuery = (command, duration) =>
{
    Console.WriteLine($"Slow query detected! Duration: {duration.TotalMilliseconds}ms");
    Console.WriteLine($"SQL: {command.CommandText}");
    // Send alert, increment counter, etc.
};
```

## DbContext Filtering

Filter queries by specific DbContext type when using multiple contexts:

```csharp
var logger = new SlowQueryLogger(
    thresholdMs: 100,
    contextTypeFilter: typeof(MySpecificDbContext)
);
```

## Parameter Masking

For production environments with sensitive data, you can mask parameter values:

```csharp
var logger = new SlowQueryLogger(
    thresholdMs: 100,
    logger: loggerFactory.CreateLogger<SlowQueryLogger>(),
    maskParameters: true  // Parameters will be logged as "***"
);
```

## Diagnostic Codes

| Code | Description |
|------|-------------|
| EFQL001 | Slow query detected (exceeds threshold) |
| EFQL003 | Query filtered by context type |

## Log Output Example

```
warn: EFInsight.SlowQueryLogger[0]
      EFQL001: Slow query detected. Duration: 150.5ms, SQL: SELECT * FROM Users WHERE Name = @p0, Parameters: [@p0=John]
```

With parameter masking enabled:
```
warn: EFInsight.SlowQueryLogger[0]
      EFQL001: Slow query detected. Duration: 150.5ms, SQL: SELECT * FROM Users WHERE Name = @p0, Parameters: [@p0=***]
```

## Compatibility

- .NET 9.0+
- EF Core 8.0+
- Works with any relational database provider (SQL Server, PostgreSQL, SQLite, etc.)

## Thread Safety

`SlowQueryLogger` is thread-safe and can be used across multiple DbContext instances.

## Performance

- Minimal impact (<1ms overhead per query)
- Production-ready
- No unnecessary allocations

## License

MIT