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

    [Test]
    public async Task SearchPackagesAsync_ValidTerm_ReturnsResultsFromQueryService()
    {
        // Arrange
        var searchTerm = "Newtonsoft.Json";
        var includePrerelease = false;
        var skip = 0;
        var take = 20;
        var cancellationToken = CancellationToken.None;
        var expectedResults = new List<IPackageSearchMetadata>
        {
            // Add mock PackageSearchResult instances if needed
        };
        // Example of mocking the DTO directly if INuGetQueryService returns it
        var expectedDtos = new List<PackageSearchResult> {
             // new PackageSearchResult("Newtonsoft.Json", "13.0.3", "Json.NET is a popular high-performance JSON framework for .NET", "https://www.newtonsoft.com/json")
             // Add more mock DTOs as needed
        };


        _mockNuGetQueryService.Setup(s => s.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken))
                              .ReturnsAsync(expectedDtos); // Assuming INuGetQueryService returns the DTO directly

        // Act
        // Note: The service applies skip/take *after* the query service call
        var results = await _service.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, cancellationToken);

        // Assert
        Assert.That(results, Is.Not.Null);
        // Assertions should check the final results after skip/take potentially
        // Assert.That(results.Count(), Is.EqualTo(expectedDtos.Count)); // Adjust if skip/take apply
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
}