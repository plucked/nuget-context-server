using Moq;
using NUnit.Framework; // Use NUnit
using Microsoft.Extensions.Logging;
using NuGetContextMcpServer.Abstractions.Interfaces;
using NuGetContextMcpServer.Application.Services;
using NuGetContextMcpServer.Abstractions.Dtos;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types; // Required for IPackageSearchMetadata
using NuGet.Versioning; // Required for NuGetVersion
using System; // Required for It.IsAny

namespace NuGetContextMcpServer.Application.Tests;

[TestFixture] // Add NUnit TestFixture attribute
public class PackageMetadataServiceTests
{
    // Make fields nullable and non-readonly for SetUp initialization
    private Mock<INuGetQueryService>? _mockNugetQueryService;
    private Mock<ILogger<PackageMetadataService>>? _mockLogger;
    private PackageMetadataService? _service;

    [SetUp] // NUnit attribute to run before each test
    public void SetUp()
    {
        // Initialize mocks and service here to ensure isolation between tests
        _mockNugetQueryService = new Mock<INuGetQueryService>();
        _mockLogger = new Mock<ILogger<PackageMetadataService>>();
        _service = new PackageMetadataService(_mockNugetQueryService.Object, _mockLogger.Object);
    }

    private Mock<IPackageSearchMetadata> CreateMockMetadata(string id, string version, string? description = "Test Desc", string? authors = "Author", string? projectUrl = "http://project.url", string? licenseUrl = "http://license.url", string? iconUrl = "http://icon.url", string? tags = "tag1 tag2", DateTimeOffset? published = null, bool isListed = true)
    {
        var mockMetadata = new Mock<IPackageSearchMetadata>();
        var identity = new NuGet.Packaging.Core.PackageIdentity(id, NuGetVersion.Parse(version));
        mockMetadata.SetupGet(m => m.Identity).Returns(identity);
        mockMetadata.SetupGet(m => m.Description).Returns(description);
        mockMetadata.SetupGet(m => m.Authors).Returns(authors);
        mockMetadata.SetupGet(m => m.ProjectUrl).Returns(projectUrl != null ? new Uri(projectUrl) : null);
        mockMetadata.SetupGet(m => m.LicenseUrl).Returns(licenseUrl != null ? new Uri(licenseUrl) : null);
        mockMetadata.SetupGet(m => m.IconUrl).Returns(iconUrl != null ? new Uri(iconUrl) : null);
        mockMetadata.SetupGet(m => m.Tags).Returns(tags);
        mockMetadata.SetupGet(m => m.Published).Returns(published ?? DateTimeOffset.UtcNow);
        mockMetadata.SetupGet(m => m.IsListed).Returns(isListed);
        // Note: DependencySets are harder to mock and not used in the current mapping
        return mockMetadata;
    }

    [Test] // Use NUnit Test attribute
    public async Task GetPackageDetailsAsync_SpecificVersion_ReturnsDetails()
    {
        // Arrange
        var packageId = "TestPackage";
        var version = "1.0.0";
        var nugetVersion = NuGetVersion.Parse(version);
        var mockMetadata = CreateMockMetadata(packageId, version);

        // Use Assert.NotNull or null-forgiving operator (!) since fields are nullable now
        _mockNugetQueryService!
            .Setup(s => s.GetPackageMetadataAsync(packageId, nugetVersion, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockMetadata.Object);

        // Act
        var result = await _service!.GetPackageDetailsAsync(packageId, version, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null); // NUnit syntax
        Assert.That(result.Id, Is.EqualTo(packageId)); // NUnit syntax
        Assert.That(result.Version, Is.EqualTo(version)); // NUnit syntax
        Assert.That(result.Description, Is.EqualTo(mockMetadata.Object.Description)); // NUnit syntax
        Assert.That(result.Authors, Is.EqualTo(mockMetadata.Object.Authors)); // NUnit syntax
        Assert.That(result.ProjectUrl, Is.EqualTo(mockMetadata.Object.ProjectUrl?.ToString())); // NUnit syntax
        _mockNugetQueryService!.Verify(s => s.GetPackageMetadataAsync(packageId, nugetVersion, It.IsAny<CancellationToken>()), Times.Once);
        _mockNugetQueryService!.Verify(s => s.GetLatestPackageMetadataAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never); // Ensure latest wasn't called
    }

    [Test] // Use NUnit Test attribute
    public async Task GetPackageDetailsAsync_LatestVersion_ReturnsDetails()
    {
        // Arrange
        var packageId = "TestPackage";
        var latestVersion = "2.0.0";
        var mockMetadata = CreateMockMetadata(packageId, latestVersion);

        _mockNugetQueryService!
            .Setup(s => s.GetLatestPackageMetadataAsync(packageId, true, It.IsAny<CancellationToken>())) // Assume true includes prerelease for latest
            .ReturnsAsync(mockMetadata.Object);

        // Act
        var result = await _service!.GetPackageDetailsAsync(packageId, null, CancellationToken.None); // version is null

        // Assert
        Assert.That(result, Is.Not.Null); // NUnit syntax
        Assert.That(result.Id, Is.EqualTo(packageId)); // NUnit syntax
        Assert.That(result.Version, Is.EqualTo(latestVersion)); // NUnit syntax
        _mockNugetQueryService!.Verify(s => s.GetLatestPackageMetadataAsync(packageId, true, It.IsAny<CancellationToken>()), Times.Once);
        _mockNugetQueryService!.Verify(s => s.GetPackageMetadataAsync(It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test] // Use NUnit Test attribute
    public async Task GetPackageDetailsAsync_LatestVersion_FallsBackToStable()
    {
        // Arrange
        var packageId = "TestPackage";
        var latestStableVersion = "1.0.0";
        var mockStableMetadata = CreateMockMetadata(packageId, latestStableVersion);

        _mockNugetQueryService!
            .Setup(s => s.GetLatestPackageMetadataAsync(packageId, true, It.IsAny<CancellationToken>())) // Latest overall returns null
            .ReturnsAsync((IPackageSearchMetadata?)null);
        _mockNugetQueryService!
            .Setup(s => s.GetLatestPackageMetadataAsync(packageId, false, It.IsAny<CancellationToken>())) // Latest stable returns metadata
            .ReturnsAsync(mockStableMetadata.Object);

        // Act
        var result = await _service!.GetPackageDetailsAsync(packageId, null, CancellationToken.None); // version is null

        // Assert
        Assert.That(result, Is.Not.Null); // NUnit syntax
        Assert.That(result.Id, Is.EqualTo(packageId)); // NUnit syntax
        Assert.That(result.Version, Is.EqualTo(latestStableVersion)); // NUnit syntax
        _mockNugetQueryService!.Verify(s => s.GetLatestPackageMetadataAsync(packageId, true, It.IsAny<CancellationToken>()), Times.Once);
        _mockNugetQueryService!.Verify(s => s.GetLatestPackageMetadataAsync(packageId, false, It.IsAny<CancellationToken>()), Times.Once); // Verify fallback was called
        _mockNugetQueryService!.Verify(s => s.GetPackageMetadataAsync(It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test] // Use NUnit Test attribute
    public async Task GetPackageDetailsAsync_SpecificVersion_NotFound_ReturnsNull()
    {
        // Arrange
        var packageId = "NotFoundPackage";
        var version = "1.0.0";
        var nugetVersion = NuGetVersion.Parse(version);

        _mockNugetQueryService!
            .Setup(s => s.GetPackageMetadataAsync(packageId, nugetVersion, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPackageSearchMetadata?)null); // Simulate not found

        // Act
        var result = await _service!.GetPackageDetailsAsync(packageId, version, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null); // NUnit syntax
        _mockNugetQueryService!.Verify(s => s.GetPackageMetadataAsync(packageId, nugetVersion, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test] // Use NUnit Test attribute
    public async Task GetPackageDetailsAsync_LatestVersion_NotFound_ReturnsNull()
    {
        // Arrange
        var packageId = "NotFoundPackage";

        _mockNugetQueryService!
            .Setup(s => s.GetLatestPackageMetadataAsync(packageId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPackageSearchMetadata?)null); // Simulate not found (overall)
        _mockNugetQueryService!
            .Setup(s => s.GetLatestPackageMetadataAsync(packageId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPackageSearchMetadata?)null); // Simulate not found (stable)


        // Act
        var result = await _service!.GetPackageDetailsAsync(packageId, null, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null); // NUnit syntax
        _mockNugetQueryService!.Verify(s => s.GetLatestPackageMetadataAsync(packageId, true, It.IsAny<CancellationToken>()), Times.Once);
        _mockNugetQueryService!.Verify(s => s.GetLatestPackageMetadataAsync(packageId, false, It.IsAny<CancellationToken>()), Times.Once); // Verify fallback was called
    }

    [Test] // Use NUnit Test attribute
    public async Task GetPackageDetailsAsync_InvalidVersionFormat_ReturnsNull()
    {
        // Arrange
        var packageId = "TestPackage";
        var invalidVersion = "not-a-version";

        // Act
        var result = await _service!.GetPackageDetailsAsync(packageId, invalidVersion, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null); // NUnit syntax
        // Verify that no call was made to the query service because parsing failed first
        _mockNugetQueryService!.Verify(s => s.GetPackageMetadataAsync(It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockNugetQueryService!.Verify(s => s.GetLatestPackageMetadataAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test] // Use NUnit Test attribute
    public async Task GetPackageDetailsAsync_QueryServiceThrows_ReturnsNull()
    {
        // Arrange
        var packageId = "TestPackage";
        var version = "1.0.0";
        var nugetVersion = NuGetVersion.Parse(version);
        var exception = new InvalidOperationException("NuGet query failed");

        _mockNugetQueryService!
            .Setup(s => s.GetPackageMetadataAsync(packageId, nugetVersion, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _service!.GetPackageDetailsAsync(packageId, version, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null); // NUnit syntax
        // Verify logger was called with the exception
        _mockLogger!.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error retrieving package details")),
                exception, // Verify the exception was logged
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}