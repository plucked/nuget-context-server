using Microsoft.Extensions.DependencyInjection;
using NuGetContextMcpServer.Abstractions.Interfaces;
using NuGetContextMcpServer.Abstractions.Dtos; // For ParsedPackageReference
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading; // Added for CancellationToken
using System.Threading.Tasks;

namespace NuGetContextMcpServer.Integration.Tests;

[TestFixture]
public class ParsingIntegrationTests : IntegrationTestBase
{
    private ISolutionParser _solutionParser = null!;
    private IProjectParser _projectParser = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        // Resolve services once for the fixture
        _solutionParser = ServiceProvider.GetRequiredService<ISolutionParser>();
        _projectParser = ServiceProvider.GetRequiredService<IProjectParser>();
    }

    // Test cases will be added here

    [Test]
    public async Task SolutionParser_ValidSln_ReturnsCorrectProjectPaths()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "SampleSolution", "SampleSolution.sln");
        var expectedProject1Path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "SimpleConsoleApp", "SimpleConsoleApp.csproj"));
        var expectedProject2Path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "ConditionalRefApp", "ConditionalRefApp.csproj"));

        // Act
        var projectPaths = await _solutionParser.GetProjectPathsAsync(solutionPath, CancellationToken.None);

        // Assert
        Assert.That(projectPaths, Is.Not.Null);
        var projectPathList = projectPaths.ToList();

        // Use Contains constraint for flexibility in order, although MSBuild usually preserves it
        Assert.That(projectPathList, Does.Contain(expectedProject1Path), $"Expected path not found: {expectedProject1Path}");
        Assert.That(projectPathList, Does.Contain(expectedProject2Path), $"Expected path not found: {expectedProject2Path}");
        Assert.That(projectPathList.Count, Is.EqualTo(2), "Expected exactly two projects.");
    }

    [Test]
    public async Task SolutionParser_NonExistentSln_ReturnsEmpty()
    {
        // Arrange
        var nonExistentPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "non_existent.sln");

        // Act
        var projectPaths = await _solutionParser.GetProjectPathsAsync(nonExistentPath, CancellationToken.None);

        // Assert
        Assert.That(projectPaths, Is.Not.Null); // Parser should return empty, not null
        Assert.That(projectPaths, Is.Empty);
    }

    [Test]
    public async Task ProjectParser_ValidCsproj_ReturnsCorrectPackageReferences()
    {
        // Arrange
        var projectPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "SimpleConsoleApp", "SimpleConsoleApp.csproj");
        var expectedPackage = new ParsedPackageReference("Newtonsoft.Json", "13.0.1");

        // Act
        var references = await _projectParser.GetPackageReferencesAsync(projectPath, CancellationToken.None);

        // Assert
        Assert.That(references, Is.Not.Null);
        var referenceList = references.ToList();
        Assert.That(referenceList.Count, Is.EqualTo(1), "Expected exactly one package reference.");
        Assert.That(referenceList, Does.Contain(expectedPackage));
    }

    [Test]
    public async Task ProjectParser_CsprojWithConditions_ReturnsCorrectPackageReferences()
    {
        // Arrange
        var projectPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "ConditionalRefApp", "ConditionalRefApp.csproj");
        var expectedNewtonsoft = new ParsedPackageReference("Newtonsoft.Json", "13.0.1");
        var expectedLogging = new ParsedPackageReference("Microsoft.Extensions.Logging", "6.0.0"); // Version from ConditionalRefApp.csproj
        var debugProperties = new Dictionary<string, string> { ["Configuration"] = "Debug" };

        // Act (Debug Configuration)
        var referencesDebug = await _projectParser.GetPackageReferencesAsync(projectPath, CancellationToken.None, debugProperties);

        // Assert (Debug Configuration)
        Assert.That(referencesDebug, Is.Not.Null);
        var referenceListDebug = referencesDebug.ToList();
        Assert.That(referenceListDebug.Count, Is.EqualTo(2), "Expected two packages in Debug config.");
        Assert.That(referenceListDebug, Does.Contain(expectedNewtonsoft), "Newtonsoft.Json missing in Debug config.");
        Assert.That(referenceListDebug, Does.Contain(expectedLogging), "Microsoft.Extensions.Logging missing in Debug config.");

        // Act (Release Configuration)
        var releaseProperties = new Dictionary<string, string> { ["Configuration"] = "Release" };
        var referencesRelease = await _projectParser.GetPackageReferencesAsync(projectPath, CancellationToken.None, releaseProperties);

        // Assert (Release Configuration)
        Assert.That(referencesRelease, Is.Not.Null);
        var referenceListRelease = referencesRelease.ToList();
        Assert.That(referenceListRelease.Count, Is.EqualTo(1), "Expected one package in Release config.");
        Assert.That(referenceListRelease, Does.Contain(expectedNewtonsoft), "Newtonsoft.Json missing in Release config.");
        Assert.That(referenceListRelease, Does.Not.Contain(expectedLogging), "Microsoft.Extensions.Logging should NOT be present in Release config.");
    }

    [Test]
    public async Task ProjectParser_CsprojWithCPM_ReturnsCorrectPackageReferences()
    {
        // Arrange
        var projectPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "CpmEnabledApp", "CpmEnabledApp.csproj");
        // Version comes from Directory.Packages.props
        var expectedPackage = new ParsedPackageReference("Newtonsoft.Json", "13.0.3");

        // Act
        var references = await _projectParser.GetPackageReferencesAsync(projectPath, CancellationToken.None);

        // Assert
        Assert.That(references, Is.Not.Null);
        var referenceList = references.ToList();
        Assert.That(referenceList.Count, Is.EqualTo(1), "Expected exactly one package reference.");
        Assert.That(referenceList, Does.Contain(expectedPackage));
    }

    [Test]
    public async Task ProjectParser_NonExistentCsproj_ReturnsEmpty()
    {
        // Arrange
        var nonExistentPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "non_existent.csproj");

        // Act
        var references = await _projectParser.GetPackageReferencesAsync(nonExistentPath, CancellationToken.None);

        // Assert
        Assert.That(references, Is.Not.Null); // Parser should return empty, not null
        Assert.That(references, Is.Empty);
    }

    [Test]
    public async Task ProjectParser_InvalidCsproj_ReturnsEmpty()
    {
        // Arrange
        var invalidProjectPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "InvalidProject", "Invalid.csproj");

        // Act
        var references = await _projectParser.GetPackageReferencesAsync(invalidProjectPath, CancellationToken.None);

        // Assert
        Assert.That(references, Is.Not.Null); // Parser should return empty on error, not null
        Assert.That(references, Is.Empty);
    }
}