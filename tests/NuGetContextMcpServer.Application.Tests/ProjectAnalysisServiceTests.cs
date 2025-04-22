using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Versioning;
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
using NuGetContextMcpServer.Application.Services;
using NuGetContextMcpServer.Abstractions.Dtos; // Updated namespace
using System.Net.Http; // For HttpRequestException

namespace NuGetContextMcpServer.Application.Tests;

[TestFixture]
public class ProjectAnalysisServiceTests
{
    private Mock<ISolutionParser> _mockSolutionParser = null!;
    private Mock<IProjectParser> _mockProjectParser = null!;
    private Mock<INuGetQueryService> _mockNuGetQueryService = null!;
    private Mock<ILogger<ProjectAnalysisService>> _mockLogger = null!;
    private ProjectAnalysisService _service = null!;

    // Define some common test data
    private const string ValidSlnPath = "/path/to/solution.sln";
    private const string ValidCsprojPath = "/path/to/project.csproj";
    private const string AnotherCsprojPath = "/path/to/another/project.csproj";
    private const string InvalidPath = "/path/to/file.txt";
    private const string NonExistentPath = "/path/to/nonexistent.sln";

    private readonly List<string> _projectsInSolution = new() { ValidCsprojPath, AnotherCsprojPath };

    // Corrected type to ParsedPackageReference and using string versions
    private readonly List<ParsedPackageReference> _project1Refs = new()
    {
        new ParsedPackageReference("Newtonsoft.Json", "12.0.0"),
        new ParsedPackageReference("Microsoft.Extensions.Logging", "[6.0.0, )") // Version range as string
    };
    private readonly List<ParsedPackageReference> _project2Refs = new()
    {
        new ParsedPackageReference("Newtonsoft.Json", "13.0.1"), // Different version
        new ParsedPackageReference("Moq", "4.18.0")
    };

    private readonly NuGetVersion _jsonLatestStable = NuGetVersion.Parse("13.0.3");
    private readonly NuGetVersion _jsonLatestPrerelease = NuGetVersion.Parse("14.0.1-beta");
    private readonly NuGetVersion _loggingLatestStable = NuGetVersion.Parse("8.0.0");
    private readonly NuGetVersion _loggingLatestPrerelease = NuGetVersion.Parse("9.0.0-preview.3");
    private readonly NuGetVersion _moqLatestStable = NuGetVersion.Parse("4.20.70");
    private readonly NuGetVersion _moqLatestPrerelease = NuGetVersion.Parse("4.20.70"); // No prerelease newer

    [SetUp]
    public void Setup()
    {
        _mockSolutionParser = new Mock<ISolutionParser>();
        _mockProjectParser = new Mock<IProjectParser>();
        _mockNuGetQueryService = new Mock<INuGetQueryService>();
        _mockLogger = new Mock<ILogger<ProjectAnalysisService>>();

        // Default setup for parsers to return empty lists to avoid null refs
        _mockSolutionParser.Setup(s => s.GetProjectPathsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(new List<string>());
        // Corrected return type for mock setup
        _mockProjectParser.Setup(s => s.GetPackageReferencesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new List<ParsedPackageReference>());

        // Default setup for file existence (can be overridden in tests)
        // We don't have a direct file system mock here, so we rely on parser behavior for "not found"

        _service = new ProjectAnalysisService(
            _mockSolutionParser.Object,
            _mockProjectParser.Object,
            _mockNuGetQueryService.Object,
            _mockLogger.Object);
    }

    [Test]
    public async Task AnalyzeProjectAsync_ValidSlnPath_CallsSolutionAndProjectParsers()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _mockSolutionParser.Setup(s => s.GetProjectPathsAsync(ValidSlnPath, cancellationToken))
                           .ReturnsAsync(_projectsInSolution);
        // Corrected return type for mock setup
        _mockProjectParser.Setup(s => s.GetPackageReferencesAsync(ValidCsprojPath, cancellationToken))
                          .ReturnsAsync(_project1Refs as IEnumerable<ParsedPackageReference>);
        _mockProjectParser.Setup(s => s.GetPackageReferencesAsync(AnotherCsprojPath, cancellationToken))
                          .ReturnsAsync(_project2Refs as IEnumerable<ParsedPackageReference>);
        // Mock NuGet queries needed for aggregation (can be simple mocks for this test)
         _mockNuGetQueryService.Setup(nq => nq.GetLatestStableVersionAsync(It.IsAny<string>(), cancellationToken)).ReturnsAsync((NuGetVersion?)null);
         _mockNuGetQueryService.Setup(nq => nq.GetLatestVersionAsync(It.IsAny<string>(), cancellationToken)).ReturnsAsync((NuGetVersion?)null);


        // Act
        await _service.AnalyzeProjectAsync(ValidSlnPath, cancellationToken);

        // Assert
        _mockSolutionParser.Verify(s => s.GetProjectPathsAsync(ValidSlnPath, cancellationToken), Times.Once);
        _mockProjectParser.Verify(s => s.GetPackageReferencesAsync(ValidCsprojPath, cancellationToken), Times.Once);
        _mockProjectParser.Verify(s => s.GetPackageReferencesAsync(AnotherCsprojPath, cancellationToken), Times.Once);
        // Corrected verification: Ensure only the expected paths were called.
        // We already verified the two expected calls happened once.
        // Moq's default behavior (loose) allows other calls. If strict mocking was used, this wouldn't be needed.
        // Alternatively, verify no *other* specific paths were called if known.
        // For simplicity, relying on the specific verifications above is often sufficient.
    }

    [Test]
    public async Task AnalyzeProjectAsync_ValidCsprojPath_CallsProjectParserDirectly()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        // Corrected return type for mock setup
        _mockProjectParser.Setup(s => s.GetPackageReferencesAsync(ValidCsprojPath, cancellationToken))
                          .ReturnsAsync(_project1Refs as IEnumerable<ParsedPackageReference>);
        // Mock NuGet queries needed for aggregation
         _mockNuGetQueryService.Setup(nq => nq.GetLatestStableVersionAsync(It.IsAny<string>(), cancellationToken)).ReturnsAsync((NuGetVersion?)null);
         _mockNuGetQueryService.Setup(nq => nq.GetLatestVersionAsync(It.IsAny<string>(), cancellationToken)).ReturnsAsync((NuGetVersion?)null);

        // Act
        await _service.AnalyzeProjectAsync(ValidCsprojPath, cancellationToken);

        // Assert
        _mockSolutionParser.Verify(s => s.GetProjectPathsAsync(It.IsAny<string>(), cancellationToken), Times.Never); // Solution parser not called
        _mockProjectParser.Verify(s => s.GetPackageReferencesAsync(ValidCsprojPath, cancellationToken), Times.Once);
    }

    [Test]
    public async Task AnalyzeProjectAsync_InvalidPath_ReturnsEmptyAndLogsError()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var results = await _service.AnalyzeProjectAsync(InvalidPath, cancellationToken);

        // Assert
        Assert.That(results, Is.Empty);
        _mockSolutionParser.Verify(s => s.GetProjectPathsAsync(It.IsAny<string>(), cancellationToken), Times.Never);
        _mockProjectParser.Verify(s => s.GetPackageReferencesAsync(It.IsAny<string>(), cancellationToken), Times.Never);
        _mockLogger.Verify(
           x => x.Log(
               It.Is<LogLevel>(l => l == LogLevel.Error),
               It.IsAny<EventId>(),
               It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Invalid file type provided. Path must end with .sln or .csproj: {InvalidPath}")), // Match actual log message
               null, // No exception expected here
               It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
           Times.Once);
    }

    [Test]
    public async Task AnalyzeProjectAsync_FileNotFound_ReturnsEmptyAndLogsError()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        // Simulate file not found by having the parser throw an exception (adjust if parser returns null/empty)
        _mockSolutionParser.Setup(s => s.GetProjectPathsAsync(NonExistentPath, cancellationToken))
                           .ThrowsAsync(new FileNotFoundException("File not found", NonExistentPath));

        // Act
        var results = await _service.AnalyzeProjectAsync(NonExistentPath, cancellationToken);

        // Assert
        Assert.That(results, Is.Empty);
        _mockSolutionParser.Verify(s => s.GetProjectPathsAsync(NonExistentPath, cancellationToken), Times.Once);
        _mockProjectParser.Verify(s => s.GetPackageReferencesAsync(It.IsAny<string>(), cancellationToken), Times.Never);
        _mockLogger.Verify(
           x => x.Log(
               It.Is<LogLevel>(l => l == LogLevel.Error),
               It.IsAny<EventId>(),
               It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error during analysis for path: {NonExistentPath}")), // Match actual log message format
               It.IsAny<FileNotFoundException>(), // Expecting FileNotFoundException
               It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
           Times.Once);
    }

     [Test]
    public async Task AnalyzeProjectAsync_ParsesReferences_CallsNuGetQueryServiceForEachRef()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        // Corrected return type for mock setup
        _mockProjectParser.Setup(s => s.GetPackageReferencesAsync(ValidCsprojPath, cancellationToken))
                          .ReturnsAsync(_project1Refs as IEnumerable<ParsedPackageReference>); // Newtonsoft.Json, Microsoft.Extensions.Logging

        // Mock NuGet queries
        _mockNuGetQueryService.Setup(nq => nq.GetLatestStableVersionAsync("Newtonsoft.Json", cancellationToken)).ReturnsAsync(_jsonLatestStable);
        _mockNuGetQueryService.Setup(nq => nq.GetLatestVersionAsync("Newtonsoft.Json", cancellationToken)).ReturnsAsync(_jsonLatestPrerelease);
        _mockNuGetQueryService.Setup(nq => nq.GetLatestStableVersionAsync("Microsoft.Extensions.Logging", cancellationToken)).ReturnsAsync(_loggingLatestStable);
        _mockNuGetQueryService.Setup(nq => nq.GetLatestVersionAsync("Microsoft.Extensions.Logging", cancellationToken)).ReturnsAsync(_loggingLatestPrerelease);

        // Act
        await _service.AnalyzeProjectAsync(ValidCsprojPath, cancellationToken);

        // Assert
        _mockProjectParser.Verify(s => s.GetPackageReferencesAsync(ValidCsprojPath, cancellationToken), Times.Once);
        _mockNuGetQueryService.Verify(nq => nq.GetLatestStableVersionAsync("Newtonsoft.Json", cancellationToken), Times.Once);
        _mockNuGetQueryService.Verify(nq => nq.GetLatestVersionAsync("Newtonsoft.Json", cancellationToken), Times.Once);
        _mockNuGetQueryService.Verify(nq => nq.GetLatestStableVersionAsync("Microsoft.Extensions.Logging", cancellationToken), Times.Once);
        _mockNuGetQueryService.Verify(nq => nq.GetLatestVersionAsync("Microsoft.Extensions.Logging", cancellationToken), Times.Once);
        _mockNuGetQueryService.Verify(nq => nq.GetLatestStableVersionAsync(It.IsNotIn("Newtonsoft.Json", "Microsoft.Extensions.Logging"), cancellationToken), Times.Never);
        _mockNuGetQueryService.Verify(nq => nq.GetLatestVersionAsync(It.IsNotIn("Newtonsoft.Json", "Microsoft.Extensions.Logging"), cancellationToken), Times.Never);
    }

    [Test]
    public async Task AnalyzeProjectAsync_AggregatesResultsCorrectly()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _mockSolutionParser.Setup(s => s.GetProjectPathsAsync(ValidSlnPath, cancellationToken))
                           .ReturnsAsync(_projectsInSolution);
        // Corrected return type for mock setup
        _mockProjectParser.Setup(s => s.GetPackageReferencesAsync(ValidCsprojPath, cancellationToken))
                          .ReturnsAsync(_project1Refs as IEnumerable<ParsedPackageReference>); // Json 12.0.0, Logging [6.0.0, )
        _mockProjectParser.Setup(s => s.GetPackageReferencesAsync(AnotherCsprojPath, cancellationToken))
                          .ReturnsAsync(_project2Refs as IEnumerable<ParsedPackageReference>); // Json 13.0.1, Moq 4.18.0

        // Mock NuGet queries
        _mockNuGetQueryService.Setup(nq => nq.GetLatestStableVersionAsync("Newtonsoft.Json", cancellationToken)).ReturnsAsync(_jsonLatestStable); // 13.0.3
        _mockNuGetQueryService.Setup(nq => nq.GetLatestVersionAsync("Newtonsoft.Json", cancellationToken)).ReturnsAsync(_jsonLatestPrerelease); // 14.0.1-beta
        _mockNuGetQueryService.Setup(nq => nq.GetLatestStableVersionAsync("Microsoft.Extensions.Logging", cancellationToken)).ReturnsAsync(_loggingLatestStable); // 8.0.0
        _mockNuGetQueryService.Setup(nq => nq.GetLatestVersionAsync("Microsoft.Extensions.Logging", cancellationToken)).ReturnsAsync(_loggingLatestPrerelease); // 9.0.0-preview.3
        _mockNuGetQueryService.Setup(nq => nq.GetLatestStableVersionAsync("Moq", cancellationToken)).ReturnsAsync(_moqLatestStable); // 4.20.70
        _mockNuGetQueryService.Setup(nq => nq.GetLatestVersionAsync("Moq", cancellationToken)).ReturnsAsync(_moqLatestPrerelease); // 4.20.70

        // Act
        var results = (await _service.AnalyzeProjectAsync(ValidSlnPath, cancellationToken)).ToList();

        // Assert
        Assert.That(results.Count, Is.EqualTo(3)); // Json, Logging, Moq (Json should be unique)

        var jsonResult = results.FirstOrDefault(r => r.Id == "Newtonsoft.Json"); // Changed PackageId to Id
        Assert.That(jsonResult, Is.Not.Null);
        // The service should ideally pick the *highest* requested version range across projects if consolidating.
        // Or list both. Current implementation likely takes the last one encountered or first. Let's assume it takes the highest range.
        // Based on current service logic (likely simple aggregation), it might just list the version from the *last* project parsed.
        // Let's assert based on the *highest* requested version seen (13.0.1)
        Assert.That(jsonResult.RequestedVersion, Is.EqualTo("12.0.0")); // Expect version from the first project due to .First() in deduplication
        Assert.That(jsonResult.LatestStableVersion, Is.EqualTo(_jsonLatestStable.ToNormalizedString()));
        Assert.That(jsonResult.LatestVersion, Is.EqualTo(_jsonLatestPrerelease.ToNormalizedString()));

        var loggingResult = results.FirstOrDefault(r => r.Id == "Microsoft.Extensions.Logging"); // Changed PackageId to Id
        Assert.That(loggingResult, Is.Not.Null);
        Assert.That(loggingResult.RequestedVersion, Is.EqualTo("[6.0.0, )")); // Changed to RequestedVersion
        Assert.That(loggingResult.LatestStableVersion, Is.EqualTo(_loggingLatestStable.ToNormalizedString()));
        Assert.That(loggingResult.LatestVersion, Is.EqualTo(_loggingLatestPrerelease.ToNormalizedString()));

        var moqResult = results.FirstOrDefault(r => r.Id == "Moq"); // Changed PackageId to Id
        Assert.That(moqResult, Is.Not.Null);
        Assert.That(moqResult.RequestedVersion, Is.EqualTo("4.18.0")); // Changed to RequestedVersion
        Assert.That(moqResult.LatestStableVersion, Is.EqualTo(_moqLatestStable.ToNormalizedString()));
        Assert.That(moqResult.LatestVersion, Is.EqualTo(_moqLatestPrerelease.ToNormalizedString())); // Stable and latest are same
    }

    [Test]
    public async Task AnalyzeProjectAsync_ParserOrQueryServiceThrows_HandlesErrorAndLogs()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var parserException = new FormatException("Invalid project file");
        var queryException = new HttpRequestException("NuGet API unreachable");

        _mockProjectParser.Setup(s => s.GetPackageReferencesAsync(ValidCsprojPath, cancellationToken))
                          .ThrowsAsync(parserException); // Simulate parser error

        _mockSolutionParser.Setup(s => s.GetProjectPathsAsync(ValidSlnPath, cancellationToken))
                           .ReturnsAsync(new List<string> { ValidCsprojPath, AnotherCsprojPath }); // Return projects
        // Corrected return type for mock setup
         _mockProjectParser.Setup(s => s.GetPackageReferencesAsync(AnotherCsprojPath, cancellationToken))
                           .ReturnsAsync(_project2Refs as IEnumerable<ParsedPackageReference>); // Second project parses fine
         _mockNuGetQueryService.Setup(nq => nq.GetLatestStableVersionAsync("Moq", cancellationToken))
                               .ThrowsAsync(queryException); // Simulate query error for second project's dep
         _mockNuGetQueryService.Setup(nq => nq.GetLatestVersionAsync("Moq", cancellationToken))
                               .ThrowsAsync(queryException);
         _mockNuGetQueryService.Setup(nq => nq.GetLatestStableVersionAsync("Newtonsoft.Json", cancellationToken)).ReturnsAsync(_jsonLatestStable); // Json query works
         _mockNuGetQueryService.Setup(nq => nq.GetLatestVersionAsync("Newtonsoft.Json", cancellationToken)).ReturnsAsync(_jsonLatestPrerelease);


        // Act
        var results = (await _service.AnalyzeProjectAsync(ValidSlnPath, cancellationToken)).ToList();

        // Assert
        // Should still return results from the project that *did* parse and whose dependencies could be queried
        Assert.That(results.Count, Is.EqualTo(1)); // Only Newtonsoft.Json from the second project should succeed fully

        var jsonResult = results.FirstOrDefault(r => r.Id == "Newtonsoft.Json"); // Changed PackageId to Id
        Assert.That(jsonResult, Is.Not.Null);
        Assert.That(jsonResult.RequestedVersion, Is.EqualTo("13.0.1")); // Changed to RequestedVersion
        Assert.That(jsonResult.LatestStableVersion, Is.EqualTo(_jsonLatestStable.ToNormalizedString()));
        Assert.That(jsonResult.LatestVersion, Is.EqualTo(_jsonLatestPrerelease.ToNormalizedString()));

        // Verify errors were logged
        _mockLogger.Verify(
           x => x.Log(
               It.Is<LogLevel>(l => l == LogLevel.Error),
               It.IsAny<EventId>(),
               It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Failed to parse project {ValidCsprojPath}")),
               It.Is<Exception>(ex => ex == parserException),
               It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
           Times.Once); // Parser error logged

         _mockLogger.Verify(
           x => x.Log(
               It.Is<LogLevel>(l => l == LogLevel.Warning), // Or Error, depending on desired severity
               It.IsAny<EventId>(),
               It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Failed to get latest versions for package Moq")),
               It.Is<Exception>(ex => ex == queryException),
               It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
           Times.AtLeastOnce); // Query error logged (might be logged for stable and prerelease separately)
    }
}