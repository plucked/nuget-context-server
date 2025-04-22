using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Protocol.Core.Types;
using NuGetContextMcpServer.Abstractions.Dtos; // Updated namespace
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
using NuGetContextMcpServer.Application.Services;

namespace NuGetContextMcpServer.Application.Tests;

[TestFixture]
public class PackageSearchServiceTests
{
    private Mock<INuGetQueryService> _mockNuGetQueryService = null!;
    private Mock<ILogger<PackageSearchService>> _mockLogger = null!;
    private PackageSearchService _service = null!;

    [SetUp]
    public void Setup()
    {
        _mockNuGetQueryService = new Mock<INuGetQueryService>();
        _mockLogger = new Mock<ILogger<PackageSearchService>>();
        _service = new PackageSearchService(_mockNuGetQueryService.Object, _mockLogger.Object);
    }

    private static readonly List<PackageSearchResult> TestSearchResults = Enumerable.Range(0, 10)
        .Select(i => new PackageSearchResult($"Package{i}", $"{i}.0.0", $"Description {i}", $"http://example.com/package{i}"))
        .ToList();

    [Test]
    public async Task SearchPackagesAsync_ValidTerm_CallsQueryService() // Renamed and simplified
    {
        // Arrange
        var searchTerm = "Newtonsoft.Json";
        var includePrerelease = false;
        var skip = 0;
        var take = 20;
        var cancellationToken = CancellationToken.None;
        var mockResults = new List<PackageSearchResult>(); // Empty is fine for this verification

        _mockNuGetQueryService.Setup(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken))
                              .ReturnsAsync(mockResults);

        // Act
        var results = await _service.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, cancellationToken);

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results, Is.Empty); // Expect empty based on mock
        _mockNuGetQueryService.Verify(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken), Times.Once);
        // Verify logger was NOT called for error
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Never);
    }

    [Test]
    public async Task SearchPackagesAsync_QueryServiceThrowsException_ReturnsEmptyAndLogsError()
    {
        // Arrange
        var searchTerm = "SomePackage";
        var includePrerelease = false;
        var skip = 0;
        var take = 10;
        var cancellationToken = CancellationToken.None;
        var exception = new InvalidOperationException("NuGet service unavailable");

        _mockNuGetQueryService.Setup(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken))
                              .ThrowsAsync(exception);

        // Act
        var results = await _service.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, cancellationToken);

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results, Is.Empty);
        _mockNuGetQueryService.Verify(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken), Times.Once);
        // Verify logger was called with Error level
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error, // Be specific about the level
                It.IsAny<EventId>(), // EventId usually doesn't matter for verification
                It.Is<It.IsAnyType>((state, type) => // Check the state object's string representation
                    state.ToString()!.Contains($"Error searching packages for term: {searchTerm}")),
                exception, // Verify the exact exception instance was passed
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), // The formatter function is usually not verified directly
            Times.Once);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task SearchPackagesAsync_HandlesDifferentPrereleaseFlags(bool includePrerelease)
    {
        // Arrange
        var searchTerm = "TestPackage";
        var skip = 0;
        var take = 5;
        var cancellationToken = CancellationToken.None;
        // Corrected type to match INuGetQueryService return type
        var expectedDtos = new List<PackageSearchResult>(); // Empty list is fine

        _mockNuGetQueryService.Setup(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken))
                              .ReturnsAsync(expectedDtos);

        // Act
        await _service.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, cancellationToken);

        // Assert
        // Verify the underlying query service was called with the correct prerelease flag
        _mockNuGetQueryService.Verify(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken), Times.Once);
    }

    [Test]
    public async Task SearchPackagesAsync_AppliesSkipCorrectly()
    {
        // Arrange
        var searchTerm = "SkipTest";
        var includePrerelease = false;
        var skip = 3;
        var take = 10; // Take more than remaining to ensure skip is the limiter
        var cancellationToken = CancellationToken.None;

        _mockNuGetQueryService.Setup(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken))
                              .ReturnsAsync(TestSearchResults); // Returns 10 items

        // Act
        var results = (await _service.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, cancellationToken)).ToList();

        // Assert
        Assert.That(results.Count, Is.EqualTo(7)); // 10 total - 3 skipped = 7
        Assert.That(results[0].Id, Is.EqualTo("Package3")); // First item should be Package3
        Assert.That(results[6].Id, Is.EqualTo("Package9")); // Last item should be Package9
        _mockNuGetQueryService.Verify(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken), Times.Once);
    }

    [Test]
    public async Task SearchPackagesAsync_AppliesTakeCorrectly()
    {
        // Arrange
        var searchTerm = "TakeTest";
        var includePrerelease = false;
        var skip = 0;
        var take = 4;
        var cancellationToken = CancellationToken.None;

        _mockNuGetQueryService.Setup(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken))
                              .ReturnsAsync(TestSearchResults); // Returns 10 items

        // Act
        var results = (await _service.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, cancellationToken)).ToList();

        // Assert
        Assert.That(results.Count, Is.EqualTo(4)); // Should take only 4
        Assert.That(results[0].Id, Is.EqualTo("Package0")); // First item
        Assert.That(results[3].Id, Is.EqualTo("Package3")); // Last item
        _mockNuGetQueryService.Verify(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken), Times.Once);
    }

    [Test]
    public async Task SearchPackagesAsync_AppliesSkipAndTakeCorrectly()
    {
        // Arrange
        var searchTerm = "SkipTakeTest";
        var includePrerelease = false;
        var skip = 2;
        var take = 5;
        var cancellationToken = CancellationToken.None;

        _mockNuGetQueryService.Setup(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken))
                              .ReturnsAsync(TestSearchResults); // Returns 10 items

        // Act
        var results = (await _service.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, cancellationToken)).ToList();

        // Assert
        Assert.That(results.Count, Is.EqualTo(5)); // Should take 5
        Assert.That(results[0].Id, Is.EqualTo("Package2")); // First item after skipping 2
        Assert.That(results[4].Id, Is.EqualTo("Package6")); // Last item (index 2 + 5 - 1 = 6)
        _mockNuGetQueryService.Verify(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken), Times.Once);
    }

    [Test]
    public async Task SearchPackagesAsync_SkipGreaterThanTotal_ReturnsEmpty()
    {
        // Arrange
        var searchTerm = "SkipAllTest";
        var includePrerelease = false;
        var skip = 15; // More than available (10)
        var take = 5;
        var cancellationToken = CancellationToken.None;

        _mockNuGetQueryService.Setup(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken))
                              .ReturnsAsync(TestSearchResults); // Returns 10 items

        // Act
        var results = (await _service.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, cancellationToken)).ToList();

        // Assert
        Assert.That(results, Is.Empty);
        _mockNuGetQueryService.Verify(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken), Times.Once);
    }

    [Test]
    public async Task SearchPackagesAsync_TakeZero_ReturnsEmpty()
    {
        // Arrange
        var searchTerm = "TakeZeroTest";
        var includePrerelease = false;
        var skip = 0;
        var take = 0;
        var cancellationToken = CancellationToken.None;

        _mockNuGetQueryService.Setup(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken))
                              .ReturnsAsync(TestSearchResults); // Returns 10 items

        // Act
        var results = (await _service.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, cancellationToken)).ToList();

        // Assert
        Assert.That(results, Is.Empty);
        _mockNuGetQueryService.Verify(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken), Times.Once);
    }
}