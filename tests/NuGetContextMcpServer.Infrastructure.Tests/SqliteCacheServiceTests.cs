using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGetContextMcpServer.Infrastructure.Caching;
using NuGetContextMcpServer.Infrastructure.Configuration;
using System.Text.Json; // Required for JsonSerializer
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace

namespace NuGetContextMcpServer.Infrastructure.Tests;

[TestFixture]
public class SqliteCacheServiceTests
{
    private const string InMemoryConnectionString = "Data Source=:memory:";
    // Removed manual connection management (_connection) as the service handles its own.
    private Mock<ILogger<SqliteCacheService>> _mockLogger = null!;
    private IOptions<CacheSettings> _cacheSettings = null!;
    private SqliteCacheService _cacheService = null!;

    [SetUp]
    public void Setup() // Changed to synchronous as no async setup needed now
    {
        _mockLogger = new Mock<ILogger<SqliteCacheService>>();
        // Corrected property name to DatabasePath
        _cacheSettings = Options.Create(new CacheSettings { DatabasePath = InMemoryConnectionString, DefaultExpirationMinutes = 30 });

        // Instantiate the service. Constructor handles connection and initialization.
        _cacheService = new SqliteCacheService(_cacheSettings, _mockLogger.Object);

        // Removed manual InitializeDatabaseAsync call
    }

    [TearDown]
    public void TearDown()
    {
        // Removed manual connection close/dispose
        _cacheService.Dispose(); // Dispose the service instance (closes its internal connection)
    }

    private record TestData(string Name, int Value);

    [Test]
    public async Task SetAsync_GetAsync_ReturnsCorrectValueBeforeExpiration()
    {
        // Arrange
        var key = "testKey";
        var data = new TestData("Example", 123);
        var expiration = TimeSpan.FromMinutes(10);

        // Act
        // Added CancellationToken.None
        await _cacheService.SetAsync(key, data, expiration, CancellationToken.None);
        var retrievedData = await _cacheService.GetAsync<TestData>(key, CancellationToken.None);

        // Assert
        Assert.That(retrievedData, Is.Not.Null);
        Assert.That(retrievedData!.Name, Is.EqualTo(data.Name)); // Added null forgiveness (!) as Assert.That checks for null
        Assert.That(retrievedData.Value, Is.EqualTo(data.Value));
    }

    [Test]
    public async Task GetAsync_ExpiredItem_ReturnsNull()
    {
        // Arrange
        var key = "expiredKey";
        var data = new TestData("Expired", 456);
        var expiration = TimeSpan.FromMilliseconds(10); // Very short expiration

        // Act
        // Added CancellationToken.None
        await _cacheService.SetAsync(key, data, expiration, CancellationToken.None);
        await Task.Delay(50); // Wait for expiration
        var retrievedData = await _cacheService.GetAsync<TestData>(key, CancellationToken.None);

        // Assert
        Assert.That(retrievedData, Is.Null);
    }

    [Test]
    public async Task RemoveExpiredAsync_RemovesExpiredItemsOnly()
    {
        // Arrange
        var expiredKey = "expired";
        var validKey = "valid";
        var expiredData = new TestData("Old", 1);
        var validData = new TestData("Current", 2);

        // Added CancellationToken.None
        await _cacheService.SetAsync(expiredKey, expiredData, TimeSpan.FromMilliseconds(1), CancellationToken.None); // Expired
        await _cacheService.SetAsync(validKey, validData, TimeSpan.FromMinutes(10), CancellationToken.None); // Valid

        await Task.Delay(10); // Ensure expiration

        // Act
        // Added CancellationToken.None
        await _cacheService.RemoveExpiredAsync(CancellationToken.None);

        // Assert
        // Added CancellationToken.None
        var retrievedExpired = await _cacheService.GetAsync<TestData>(expiredKey, CancellationToken.None);
        var retrievedValid = await _cacheService.GetAsync<TestData>(validKey, CancellationToken.None);

        Assert.That(retrievedExpired, Is.Null, "Expired item should be removed.");
        Assert.That(retrievedValid, Is.Not.Null, "Valid item should remain.");
        Assert.That(retrievedValid!.Name, Is.EqualTo(validData.Name)); // Added null forgiveness (!)
    }

    [Test]
    public async Task SetAsync_ExistingKey_OverwritesValue()
    {
        // Arrange
        var key = "overwriteKey";
        var initialData = new TestData("Initial", 1);
        var updatedData = new TestData("Updated", 2);
        var expiration = TimeSpan.FromMinutes(5);

        // Act
        // Added CancellationToken.None
        await _cacheService.SetAsync(key, initialData, expiration, CancellationToken.None);
        await _cacheService.SetAsync(key, updatedData, expiration, CancellationToken.None); // Overwrite
        var retrievedData = await _cacheService.GetAsync<TestData>(key, CancellationToken.None);

        // Assert
        Assert.That(retrievedData, Is.Not.Null);
        Assert.That(retrievedData!.Name, Is.EqualTo(updatedData.Name)); // Added null forgiveness (!)
        Assert.That(retrievedData.Value, Is.EqualTo(updatedData.Value));
    }

    [Test]
    public async Task GetAsync_NonExistentKey_ReturnsNull()
    {
        // Arrange
        var key = "nonExistentKey";

        // Act
        // Added CancellationToken.None
        var retrievedData = await _cacheService.GetAsync<TestData>(key, CancellationToken.None);

        // Assert
        Assert.That(retrievedData, Is.Null);
    }

    // Test for GetAsync_InvalidJson_ReturnsNullAndRemovesEntry is omitted due to complexity
    // as noted in the implementation plan. It would require either corrupting the DB manually
    // or complex mocking of JsonSerializer which is brittle.
}