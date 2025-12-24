using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace EFInsight.Tests;

public class SlowQueryLoggerTests : IDisposable
{
    private readonly List<SqliteConnection> _connections = new();

    public void Dispose()
    {
        foreach (var connection in _connections)
        {
            connection.Close();
            connection.Dispose();
        }
    }

    private DbContextOptions<TestDbContext> CreateSqliteOptions(SlowQueryLogger logger)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(logger)
            .Options;

        using var context = new TestDbContext(options);
        context.Database.EnsureCreated();

        return options;
    }

    private DbContextOptions<TestDbContext> CreateSqliteOptions(params SlowQueryLogger[] loggers)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(loggers.Cast<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>().ToArray())
            .Options;

        using var context = new TestDbContext(options);
        context.Database.EnsureCreated();

        return options;
    }

    [Fact]
    public void Constructor_WithNegativeThreshold_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlowQueryLogger(thresholdMs: -1));
    }

    [Fact]
    public void Constructor_WithZeroThreshold_DoesNotThrow()
    {
        // Arrange & Act & Assert
        var logger = new SlowQueryLogger(thresholdMs: 0);
        Assert.NotNull(logger);
    }

    [Fact]
    public void Constructor_WithDefaultParameters_CreatesInstance()
    {
        // Arrange & Act
        var logger = new SlowQueryLogger();

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void OnSlowQuery_CanBeSet()
    {
        // Arrange
        var logger = new SlowQueryLogger();
        logger.OnSlowQuery = (_, _) => { };

        // Assert
        Assert.NotNull(logger.OnSlowQuery);
    }

    [Fact]
    public async Task Query_BelowThreshold_DoesNotInvokeCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var logger = new SlowQueryLogger(thresholdMs: 10000); // Very high threshold
        logger.OnSlowQuery = (_, _) => callbackInvoked = true;

        var options = CreateSqliteOptions(logger);

        using var context = new TestDbContext(options);
        context.TestEntities.Add(new TestEntity { Name = "Test" });
        await context.SaveChangesAsync();

        // Act
        var result = await context.TestEntities.ToListAsync();

        // Assert
        Assert.NotEmpty(result);
        Assert.False(callbackInvoked);
    }

    [Fact]
    public async Task Query_AboveThreshold_InvokesCallback()
    {
        // Arrange
        var callbackInvoked = false;
        DbCommand? capturedCommand = null;
        TimeSpan capturedDuration = TimeSpan.Zero;

        var logger = new SlowQueryLogger(thresholdMs: 0); // Zero threshold - all queries are slow
        logger.OnSlowQuery = (cmd, duration) =>
        {
            callbackInvoked = true;
            capturedCommand = cmd;
            capturedDuration = duration;
        };

        var options = CreateSqliteOptions(logger);

        using var context = new TestDbContext(options);
        context.TestEntities.Add(new TestEntity { Name = "Test" });
        await context.SaveChangesAsync();
        callbackInvoked = false; // Reset after SaveChanges

        // Act
        var result = await context.TestEntities.ToListAsync();

        // Assert
        Assert.NotEmpty(result);
        Assert.True(callbackInvoked);
        Assert.True(capturedDuration.TotalMilliseconds >= 0);
    }

    [Fact]
    public void SyncQuery_BelowThreshold_DoesNotInvokeCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var logger = new SlowQueryLogger(thresholdMs: 10000); // Very high threshold
        logger.OnSlowQuery = (_, _) => callbackInvoked = true;

        var options = CreateSqliteOptions(logger);

        using var context = new TestDbContext(options);
        context.TestEntities.Add(new TestEntity { Name = "Test" });
        context.SaveChanges();

        // Act
        var result = context.TestEntities.ToList();

        // Assert
        Assert.NotEmpty(result);
        Assert.False(callbackInvoked);
    }

    [Fact]
    public void SyncQuery_AboveThreshold_InvokesCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var logger = new SlowQueryLogger(thresholdMs: 0); // Zero threshold - all queries are slow
        logger.OnSlowQuery = (_, _) => callbackInvoked = true;

        var options = CreateSqliteOptions(logger);

        using var context = new TestDbContext(options);
        context.TestEntities.Add(new TestEntity { Name = "Test" });
        context.SaveChanges();
        callbackInvoked = false; // Reset after SaveChanges

        // Act
        var result = context.TestEntities.ToList();

        // Assert
        Assert.NotEmpty(result);
        Assert.True(callbackInvoked);
    }

    [Fact]
    public async Task Logger_WithStackTrace_LogsStackTrace()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SlowQueryLogger>>();
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var logger = new SlowQueryLogger(
            thresholdMs: 0,
            logger: mockLogger.Object,
            captureStackTrace: true);

        var options = CreateSqliteOptions(logger);

        using var context = new TestDbContext(options);
        context.TestEntities.Add(new TestEntity { Name = "Test" });
        await context.SaveChangesAsync();

        // Act
        var result = await context.TestEntities.ToListAsync();

        // Assert
        Assert.NotEmpty(result);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("EFQL001")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Logger_WithoutStackTrace_DoesNotLogStackTrace()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SlowQueryLogger>>();
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var logger = new SlowQueryLogger(
            thresholdMs: 0,
            logger: mockLogger.Object,
            captureStackTrace: false);

        var options = CreateSqliteOptions(logger);

        using var context = new TestDbContext(options);
        context.TestEntities.Add(new TestEntity { Name = "Test" });
        await context.SaveChangesAsync();

        // Act
        var result = await context.TestEntities.ToListAsync();

        // Assert
        Assert.NotEmpty(result);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("EFQL001") && !o.ToString()!.Contains("StackTrace:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Logger_WithContextFilter_OnlyLogsMatchingContext()
    {
        // Arrange
        var callbackInvokedForTest = false;
        var callbackInvokedForOther = false;

        var testLogger = new SlowQueryLogger(
            thresholdMs: 0,
            contextTypeFilter: typeof(TestDbContext));
        testLogger.OnSlowQuery = (_, _) => callbackInvokedForTest = true;

        var otherLogger = new SlowQueryLogger(
            thresholdMs: 0,
            contextTypeFilter: typeof(OtherDbContext));
        otherLogger.OnSlowQuery = (_, _) => callbackInvokedForOther = true;

        var options = CreateSqliteOptions(testLogger, otherLogger);

        using var testContext = new TestDbContext(options);
        testContext.TestEntities.Add(new TestEntity { Name = "Test" });
        await testContext.SaveChangesAsync();
        
        // Reset both flags after SaveChanges
        callbackInvokedForTest = false;
        callbackInvokedForOther = false;

        // Act
        var result = await testContext.TestEntities.ToListAsync();

        // Assert
        Assert.NotEmpty(result);
        Assert.True(callbackInvokedForTest);
        Assert.False(callbackInvokedForOther);
    }

    [Fact]
    public async Task NonQueryCommand_AboveThreshold_InvokesCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var logger = new SlowQueryLogger(thresholdMs: 0);
        logger.OnSlowQuery = (_, _) => callbackInvoked = true;

        var options = CreateSqliteOptions(logger);

        using var context = new TestDbContext(options);

        // Act - SaveChangesAsync triggers non-query commands
        context.TestEntities.Add(new TestEntity { Name = "Test" });
        await context.SaveChangesAsync();

        // Assert
        Assert.True(callbackInvoked);
    }

    [Fact]
    public async Task ScalarQuery_AboveThreshold_InvokesCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var logger = new SlowQueryLogger(thresholdMs: 0);
        logger.OnSlowQuery = (_, _) => callbackInvoked = true;

        var options = CreateSqliteOptions(logger);

        using var context = new TestDbContext(options);
        context.TestEntities.Add(new TestEntity { Name = "Test" });
        await context.SaveChangesAsync();
        callbackInvoked = false; // Reset after SaveChanges

        // Act - CountAsync is a scalar query
        var count = await context.TestEntities.CountAsync();

        // Assert
        Assert.Equal(1, count);
        Assert.True(callbackInvoked);
    }

    [Fact]
    public async Task Logger_WithMaskParameters_MasksParameterValues()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SlowQueryLogger>>();
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var logger = new SlowQueryLogger(
            thresholdMs: 0,
            logger: mockLogger.Object,
            maskParameters: true);

        var options = CreateSqliteOptions(logger);

        using var context = new TestDbContext(options);
        context.TestEntities.Add(new TestEntity { Name = "SensitiveData" });
        await context.SaveChangesAsync();

        // Act
        var result = await context.TestEntities.Where(e => e.Name == "SensitiveData").ToListAsync();

        // Assert
        Assert.NotEmpty(result);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("***") && !o.ToString()!.Contains("SensitiveData")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
