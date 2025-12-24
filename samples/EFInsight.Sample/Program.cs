using EFInsight;
using EFInsight.Sample;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== EFInsight Sample Application ===\n");

// Create a logger factory for console output
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var logger = loggerFactory.CreateLogger<SlowQueryLogger>();

// Create SlowQueryLogger with a very low threshold (1ms) to demonstrate logging
var slowQueryLogger = new SlowQueryLogger(
    thresholdMs: 1,              // Very low threshold to catch all queries for demo
    logger: logger,
    captureStackTrace: false,    // Set to true for debugging
    maskParameters: false        // Set to true to hide parameter values in logs
);

// Track slow queries with a custom callback
var slowQueryCount = 0;
slowQueryLogger.OnSlowQuery = (command, duration) =>
{
    slowQueryCount++;
    Console.WriteLine($"\n[CALLBACK] Slow query #{slowQueryCount} detected!");
    Console.WriteLine($"  Duration: {duration.TotalMilliseconds:F2}ms");
    Console.WriteLine($"  SQL: {command.CommandText[..Math.Min(100, command.CommandText.Length)]}...\n");
};

// Configure DbContext with SQLite and the slow query logger
var options = new DbContextOptionsBuilder<SampleDbContext>()
    .UseSqlite("Data Source=:memory:")
    .AddInterceptors(slowQueryLogger)
    .Options;

using var context = new SampleDbContext(options);

// Ensure database is created
context.Database.OpenConnection();
context.Database.EnsureCreated();

Console.WriteLine("--- Executing Sample Queries ---\n");

// Query 1: Simple select all
Console.WriteLine("1. Fetching all products...");
var allProducts = await context.Products.ToListAsync();
Console.WriteLine($"   Found {allProducts.Count} products\n");

// Query 2: Query with filter
Console.WriteLine("2. Finding products with price > $50...");
var expensiveProducts = await context.Products
    .Where(p => p.Price > 50)
    .ToListAsync();
Console.WriteLine($"   Found {expensiveProducts.Count} expensive products\n");

// Query 3: Query with Include (eager loading)
Console.WriteLine("3. Loading products with categories...");
var productsWithCategories = await context.Products
    .Include(p => p.Category)
    .ToListAsync();
foreach (var product in productsWithCategories)
{
    Console.WriteLine($"   - {product.Name} ({product.Category?.Name})");
}
Console.WriteLine();

// Query 4: Aggregation query
Console.WriteLine("4. Counting products per category...");
var productCounts = await context.Products
    .GroupBy(p => p.CategoryId)
    .Select(g => new { CategoryId = g.Key, Count = g.Count() })
    .ToListAsync();
foreach (var count in productCounts)
{
    Console.WriteLine($"   Category {count.CategoryId}: {count.Count} products");
}
Console.WriteLine();

// Query 5: Insert operation
Console.WriteLine("5. Adding a new product...");
context.Products.Add(new Product 
{ 
    Name = "Headphones", 
    Price = 149.99m, 
    CategoryId = 1 
});
await context.SaveChangesAsync();
Console.WriteLine("   Product added successfully\n");

// Query 6: Update operation
Console.WriteLine("6. Updating product price...");
var laptop = await context.Products.FirstAsync(p => p.Name == "Laptop");
laptop.Price = 899.99m;
await context.SaveChangesAsync();
Console.WriteLine("   Price updated successfully\n");

Console.WriteLine("=== Summary ===");
Console.WriteLine($"Total slow queries detected: {slowQueryCount}");
Console.WriteLine("\nNote: The low threshold (1ms) ensures all queries are logged for demonstration.");
Console.WriteLine("In production, set thresholdMs to a higher value (e.g., 100ms or more).");
