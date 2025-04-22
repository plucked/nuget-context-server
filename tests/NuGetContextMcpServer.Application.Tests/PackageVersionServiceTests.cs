using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Versioning;
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
using NuGetContextMcpServer.Application.Services;
using NuGetContextMcpServer.Abstractions.Dtos; // Add using for PackageVersionInfo

namespace NuGetContextMcpServer.Application.Tests;

[TestFixture]
public class PackageVersionServiceTests
{
    private Mock<INuGetQueryService> _mockNuGetQueryService = null!;
    private Mock<ILogger<PackageVersionService>> _mockLogger = null!;
    private PackageVersionService _service = null!;

    [SetUp]
    public void Setup()
    {
        _mockNuGetQueryService = new Mock<INuGetQueryService>();
        _mockLogger = new Mock<ILogger<PackageVersionService>>();
        _service = new PackageVersionService(_mockNuGetQueryService.Object, _mockLogger.Object);
    }

    private static readonly List<NuGetVersion> TestVersions = new()
    {
        NuGetVersion.Parse("1.0.0"),
        NuGetVersion.Parse("2.0.0-beta"),
        NuGetVersion.Parse("1.1.0"),
        NuGetVersion.Parse("2.0.0"),
        NuGetVersion.Parse("0.9.0")
    };

    [Test]
    public async Task GetPackageVersionsAsync_StableOnly_FiltersPrereleaseAndReturnsSorted()
    {
        // Arrange
        var packageId = "TestPackage";
        var includePrerelease = false;
        var cancellationToken = CancellationToken.None;
        var expectedStableVersions = new List<string> // Expect strings
        {
            "2.0.0",
            "1.1.0",
            "1.0.0",
            "0.9.0"
        }; // Sorted descending

        _mockNuGetQueryService.Setup(s => s.GetAllVersionsAsync(packageId, cancellationToken))
                              .ReturnsAsync(TestVersions);

        // Act
        var results = await _service.GetPackageVersionsAsync(packageId, includePrerelease, cancellationToken);

        // Assert
        Assert.That(results, Is.EqualTo(expectedStableVersions)); // Checks order and content
        _mockNuGetQueryService.Verify(s => s.GetAllVersionsAsync(packageId, cancellationToken), Times.Once);
    }

    [Test]
    public async Task GetPackageVersionsAsync_IncludePrerelease_ReturnsAllSorted()
    {
        // Arrange
        var packageId = "TestPackage";
        var includePrerelease = true;
        var cancellationToken = CancellationToken.None;
        var expectedAllVersionsSorted = new List<string> // Expect strings
        {
            "2.0.0",
            "2.0.0-beta",
            "1.1.0",
            "1.0.0",
            "0.9.0"
        }; // Sorted descending

        _mockNuGetQueryService.Setup(s => s.GetAllVersionsAsync(packageId, cancellationToken))
                              .ReturnsAsync(TestVersions);

        // Act
        var results = await _service.GetPackageVersionsAsync(packageId, includePrerelease, cancellationToken);

        // Assert
        Assert.That(results, Is.EqualTo(expectedAllVersionsSorted)); // Checks order and content
        _mockNuGetQueryService.Verify(s => s.GetAllVersionsAsync(packageId, cancellationToken), Times.Once);
    }

    [Test]
    public async Task GetPackageVersionsAsync_QueryServiceThrows_ReturnsEmptyAndLogsError()
    {
        // Arrange
        var packageId = "ErrorPackage";
        var includePrerelease = false;
        var cancellationToken = CancellationToken.None;
        var exception = new InvalidOperationException("NuGet query failed");

        _mockNuGetQueryService.Setup(s => s.GetAllVersionsAsync(packageId, cancellationToken))
                              .ThrowsAsync(exception);

        // Act
        var results = await _service.GetPackageVersionsAsync(packageId, includePrerelease, cancellationToken);

        // Assert
        Assert.That(results, Is.Empty);
        _mockNuGetQueryService.Verify(s => s.GetAllVersionsAsync(packageId, cancellationToken), Times.Once);
        _mockLogger.Verify(
           x => x.Log(
               It.Is<LogLevel>(l => l == LogLevel.Error),
               It.IsAny<EventId>(),
               It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error getting versions for package: {packageId}")),
               It.Is<Exception>(ex => ex == exception),
               It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
           Times.Once);
    }

    [Test]
    public async Task GetLatestPackageVersionAsync_Stable_CallsCorrectQueryServiceMethod()
    {
        // Arrange
        var packageId = "StableCheck";
        var includePrerelease = false;
        var cancellationToken = CancellationToken.None;
        var expectedVersion = NuGetVersion.Parse("1.0.0"); // Mock returns NuGetVersion

        _mockNuGetQueryService.Setup(s => s.GetLatestStableVersionAsync(packageId, cancellationToken))
                              .ReturnsAsync(expectedVersion); // Return the NuGetVersion

        // Act
        var result = await _service.GetLatestPackageVersionAsync(packageId, includePrerelease, cancellationToken);

        // Assert
        // Analyzer might incorrectly warn here, but NuGetVersion implements equality correctly.
        // Assert against the PackageVersionInfo object returned by the service
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.PackageId, Is.EqualTo(packageId));
        Assert.That(result.LatestVersion, Is.EqualTo(expectedVersion.ToNormalizedString()));
        _mockNuGetQueryService.Verify(s => s.GetLatestStableVersionAsync(packageId, cancellationToken), Times.Once);
        _mockNuGetQueryService.Verify(s => s.GetLatestVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never); // Ensure the other wasn't called
    }

    [Test]
    public async Task GetLatestPackageVersionAsync_Prerelease_CallsCorrectQueryServiceMethod()
    {
        // Arrange
        var packageId = "PrereleaseCheck";
        var includePrerelease = true;
        var cancellationToken = CancellationToken.None;
        var expectedVersion = NuGetVersion.Parse("2.0.0-beta"); // Mock returns NuGetVersion

        _mockNuGetQueryService.Setup(s => s.GetLatestVersionAsync(packageId, cancellationToken))
                              .ReturnsAsync(expectedVersion); // Return the NuGetVersion

        // Act
        var result = await _service.GetLatestPackageVersionAsync(packageId, includePrerelease, cancellationToken);

        // Assert
        // Analyzer might incorrectly warn here, but NuGetVersion implements equality correctly.
        // Assert against the PackageVersionInfo object returned by the service
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.PackageId, Is.EqualTo(packageId));
        Assert.That(result.LatestVersion, Is.EqualTo(expectedVersion.ToNormalizedString()));
        _mockNuGetQueryService.Verify(s => s.GetLatestVersionAsync(packageId, cancellationToken), Times.Once);
        _mockNuGetQueryService.Verify(s => s.GetLatestStableVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never); // Ensure the other wasn't called
    }

    [TestCase(false)] // Test stable
    [TestCase(true)]  // Test include prerelease
    public async Task GetLatestPackageVersionAsync_PackageNotFound_ReturnsNull(bool includePrerelease)
    {
        // Arrange
        var packageId = "NotFound";
        var cancellationToken = CancellationToken.None;
        NuGetVersion? nullVersion = null;

        if (includePrerelease)
        {
            _mockNuGetQueryService.Setup(s => s.GetLatestVersionAsync(packageId, cancellationToken))
                                  .ReturnsAsync(nullVersion);
        }
        else
        {
            _mockNuGetQueryService.Setup(s => s.GetLatestStableVersionAsync(packageId, cancellationToken))
                                  .ReturnsAsync(nullVersion);
        }

        // Act
        var result = await _service.GetLatestPackageVersionAsync(packageId, includePrerelease, cancellationToken);

        // Assert
        Assert.That(result, Is.Null);
    }

    [TestCase(false)] // Test stable
    [TestCase(true)]  // Test include prerelease
    public async Task GetLatestPackageVersionAsync_QueryServiceThrows_ReturnsNullAndLogsError(bool includePrerelease)
    {
        // Arrange
        var packageId = "ErrorLatest";
        var cancellationToken = CancellationToken.None;
        var exception = new AggregateException("NuGet query failed");

        if (includePrerelease)
        {
            _mockNuGetQueryService.Setup(s => s.GetLatestVersionAsync(packageId, cancellationToken))
                                  .ThrowsAsync(exception);
        }
        else
        {
            _mockNuGetQueryService.Setup(s => s.GetLatestStableVersionAsync(packageId, cancellationToken))
                                  .ThrowsAsync(exception);
        }

        // Act
        var result = await _service.GetLatestPackageVersionAsync(packageId, includePrerelease, cancellationToken);

        // Assert
        Assert.That(result, Is.Null);
        _mockLogger.Verify(
           x => x.Log(
               It.Is<LogLevel>(l => l == LogLevel.Error),
               It.IsAny<EventId>(),
               It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error getting latest version for package: {packageId}")),
               It.Is<Exception>(ex => ex == exception),
               It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
           Times.Once);
    }
}