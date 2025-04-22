# Task 07: Implement Integration Tests (Incremental Approach)

**Goal:** Verify the interactions between different components of the `NuGetContextMcpServer` application, ensuring they work together correctly using real dependencies or controlled test environments. Implement and run tests incrementally to detect integration issues early.

**Outcome:** A suite of NUnit integration tests providing confidence that the parsing, caching, NuGet interaction, and application service layers function correctly together.

**Context & Approach:**

*   These tests complement the unit tests (Task 06) by focusing on component interactions.
*   **Incremental Testing:** Tests should be implemented and **run** progressively. For example, after implementing the parsing tests, run them to validate the parsing infrastructure against test assets before moving on. Similarly, run NuGet/Cache tests after implementation, and so on. This helps catch integration errors early.
*   **Local NuGet Feed & Docker:** For tests involving NuGet interactions (Tasks 7.4, 7.6, 7.7), a local NuGet feed (BaGet) running in a Docker container managed by `Testcontainers` will be set up **as part of this task** (specifically within the `[OneTimeSetUp]` of relevant test fixtures). This setup, including starting the container, pushing test packages, and cleanup, should follow the guidance in `deep-research/05.md` (Assumed finalized; implementer should review this document for specific Testcontainers setup guidance). The test host configuration will dynamically point to this ephemeral feed's URL. **Prerequisite:** Ensure the development/test machine has Docker Desktop (or equivalent) running and that Testcontainers is correctly configured (e.g., Docker CLI accessible and Docker daemon running).
*   **Test Assets:** Specific sample `.sln` and `.csproj` files will be created within the test project to provide controlled inputs for parsing tests.
*   **Test Host:** We will use the `.NET Generic Host` (`Host.CreateDefaultBuilder`) within the test project setup to leverage the application's DI container and configuration system, allowing us to run tests with real service implementations but potentially overridden configurations (e.g., pointing to the local feed or a test database).
*   **Test Integrity:** The primary purpose of these tests is to validate the correct functioning and integration of the application components. If a test fails, the default assumption must be that there is an issue in the application code (parsing logic, service coordination, caching, NuGet interaction, etc.). **Do not modify tests simply to make them pass.** Investigate the failure, identify the root cause in the application code, and fix the application code. Only modify a test if it is determined to be genuinely incorrect or if the requirements/design it was testing have explicitly changed. Maintaining this discipline is crucial for ensuring the tests provide real value and confidence in the application's correctness.
*   **Code Review Prerequisite:** Before implementing tests for a specific component or interaction (e.g., parsing, caching, a service), the implementing agent **must** first read and understand the relevant application source code being tested (using the `read_file` tool). This ensures tests accurately reflect the code's behavior and intended functionality.
*   **Parallel Execution:** NUnit might run test fixtures in parallel. To ensure isolation, especially with shared resources like the Testcontainer or cache files:
    *   Manage the Testcontainer lifecycle per-fixture (e.g., using non-static members in a base class `[SetUp]` / `[TearDown]` or `[OneTimeSetUp]` / `[OneTimeTearDown]`) rather than using static members that might conflict across parallel fixtures.
    *   Ensure unique cache database paths per test run if not using shared in-memory mode.
    *   Alternatively, consider explicitly disabling parallel execution for this integration test assembly if managing shared resources proves complex initially.
*   **CI/CD Considerations:** If these tests are intended to run in a CI/CD pipeline (e.g., GitHub Actions, Azure Pipelines), the pipeline environment must support Docker execution (e.g., using Docker-enabled runners). Specific CI configuration is outside the scope of this document.
*   **Test Isolation:** Wherever practical, ensure each test fixture (`[TestFixture]`) sets up its own required state (e.g., pushes necessary packages to the Testcontainer feed, prepares cache state) independently in its `[SetUp]` or `[OneTimeSetUp]` to avoid implicit dependencies between different test fixtures.
*   **Test Naming Convention:** Use a consistent naming convention for tests, such as `MethodOrComponent_Scenario_ExpectedResult` (e.g., `GetAllVersions_CacheMiss_FetchesFromLocalFeedAndPopulatesCache`).
*   **CI/CD Considerations:** If these tests are intended to run in a CI/CD pipeline (e.g., GitHub Actions, Azure Pipelines), the pipeline environment must support Docker execution (e.g., using Docker-enabled runners). Specific CI configuration is outside the scope of this document.
*   **Test Isolation:** Wherever practical, ensure each test fixture (`[TestFixture]`) sets up its own required state (e.g., pushes necessary packages to the Testcontainer feed, prepares cache state) independently in its `[SetUp]` or `[OneTimeSetUp]` to avoid implicit dependencies between different test fixtures.
*   **Test Naming Convention:** Use a consistent naming convention for tests, such as `MethodOrComponent_Scenario_ExpectedResult` (e.g., `GetAllVersions_CacheMiss_FetchesFromLocalFeedAndPopulatesCache`).

---

## Sub-Tasks:

### 7.1 Create Integration Test Project

*   **Action:** Create a new NUnit test project named `NuGetContextMcpServer.Integration.Tests` within the `tests` folder. Add it to the `NuGetContextMcpServer.sln` solution.
*   **Configuration:**
    *   Target Framework: `net9.0`
    *   Add Project References to:
        *   `src/NuGetContextMcpServer.Host/NuGetContextMcpServer.Host.csproj`
        *   `src/NuGetContextMcpServer.Application/NuGetContextMcpServer.Application.csproj`
        *   `src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj`
        *   `src/NuGetContextMcpServer.Abstractions/NuGetContextMcpServer.Abstractions.csproj`
    *   Add NuGet Packages:
        *   `Microsoft.NET.Test.Sdk`
        *   `NUnit`, `NUnit3TestAdapter`, `NUnit.Analyzers`
        *   `Moq` (Optional, but can be useful for specific isolation within integration tests)
        *   `Microsoft.Extensions.Hosting` (To build the test host)
        *   `coverlet.collector`
*   **Relevant Files:**
    *   `NuGetContextMcpServer.sln`
    *   `tests/NuGetContextMcpServer.Integration.Tests/NuGetContextMcpServer.Integration.Tests.csproj` (To be created)
*   **Outcome:** A new test project configured for integration testing.

### 7.2 Create Test Assets

*   **Action:** Create a `TestAssets` folder within the `tests/NuGetContextMcpServer.Integration.Tests` project. Create the following sample files and configure them to be copied to the output directory (`<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` in the `.csproj`).
*   **Files & Content:**
    *   **`TestAssets/SimpleConsoleApp/SimpleConsoleApp.csproj`**:
        ```xml
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net9.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
          </ItemGroup>
        </Project>
        ```
    *   **`TestAssets/ConditionalRefApp/ConditionalRefApp.csproj`**:
        ```xml
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net9.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
            <PackageReference Include="Microsoft.Extensions.Logging" Version="6.0.0" Condition="'$(Configuration)'=='Debug'" />
          </ItemGroup>
        </Project>
        ```
    *   **`TestAssets/CpmEnabledApp/Directory.Packages.props`**:
        ```xml
        <Project>
          <PropertyGroup>
            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
          </PropertyGroup>
          <ItemGroup>
            <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
          </ItemGroup>
        </Project>
        ```
    *   **`TestAssets/CpmEnabledApp/CpmEnabledApp.csproj`**:
        ```xml
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net9.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" />
          </ItemGroup>
        </Project>
        ```
    *   **`TestAssets/SampleSolution/SampleSolution.sln`**: (Content generated via `dotnet new sln` and `dotnet sln add ...`) - Ensure it references `../SimpleConsoleApp/SimpleConsoleApp.csproj` and `../ConditionalRefApp/ConditionalRefApp.csproj`.
*   **Validation:** The implementer **must** run `dotnet build` within each sample project directory (`SimpleConsoleApp`, `ConditionalRefApp`, `CpmEnabledApp`) and on the solution (`SampleSolution`) after creation to ensure they are valid MSBuild artifacts before proceeding with tests that use them. This validation is part of Task 7.2.
*   **Outcome:** A `TestAssets` folder containing valid sample projects for testing parsing scenarios.

### 7.3 Configure Test Host Setup

*   **Action:** Within the integration test classes (e.g., in `[SetUp]` or `[OneTimeSetUp]` methods), use `Host.CreateDefaultBuilder()` to configure and build a test host instance. **Important:** Ensure `MsBuildInitializer.EnsureMsBuildRegistered()` is called explicitly in the test setup (e.g., `[OneTimeSetUp]` or `[SetUpFixture]`) *before* `Host.CreateDefaultBuilder()` is invoked, similar to `Program.cs`.
*   **Create Test Configuration:** Create an `appsettings.Testing.json` file within the integration test project (`tests/NuGetContextMcpServer.Integration.Tests`). Configure it to be copied to the output directory (`<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`). This file will be loaded by the test host to override default settings. Initial content should include placeholders or test defaults for `NuGetSettings:QueryFeedUrl` (e.g., `"http://localhost:9999/v3/index.json"`) and `CacheSettings:DatabasePath` (e.g., `"test_cache.db"`), and potentially adjusted `Logging` levels (e.g., setting `Default` to `Debug`).
*   **Configuration Overrides:** Use `ConfigureAppConfiguration` within the test host builder to load `appsettings.Testing.json` and potentially environment variables to override settings dynamically:
    *   `NuGetSettings:QueryFeedUrl`: Point to the local NuGet feed URL.
    *   `CacheSettings:DatabasePath`: Use a unique path per test run (e.g., using `Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".db")`) or configure SQLite for shared in-memory mode.
    *   **Note on Shared In-Memory SQLite:** If using in-memory mode for persistence across tests within a fixture, use the connection string `"Data Source=SharedInMemoryCache;Mode=Memory;Cache=Shared"`. A single `SqliteConnection` instance must be created and opened in `[OneTimeSetUp]` and kept open until disposed in `[OneTimeTearDown]` to maintain the database state.
    *   **Cleanup Note:** If using unique temporary files for `CacheSettings:DatabasePath` (e.g., via `Path.GetTempPath()`), ensure these files are explicitly deleted in the corresponding `[TearDown]` or `[OneTimeTearDown]` method to avoid cluttering the temporary directory.
*   **DI Access:** Use `host.Services.GetRequiredService<T>()` to resolve instances of the application's services (`ISolutionParser`, `IProjectParser`, `INuGetQueryService`, `ICacheService`, `IProjectAnalysisService`) for use within tests.
*   **Relevant Files:**
    *   `src/NuGetContextMcpServer.Host/Program.cs` (Reference for DI setup)
    *   `src/NuGetContextMcpServer.Infrastructure/Parsing/MsBuildInitializer.cs` (**Important:** `EnsureMsBuildRegistered()` **must** be called explicitly in the test setup (e.g., `[OneTimeSetUp]` or `[SetUpFixture]`) *before* `Host.CreateDefaultBuilder()` is invoked, similar to `Program.cs`).
*   **Outcome:** A pattern for creating configured test host instances providing access to real services for testing.

### 7.4 Create and Push Test Artifacts (NuGet Packages)

*   **Action:** For tests requiring interaction with the local NuGet feed (BaGetter), implement logic within the `[OneTimeSetUp]` method of the relevant test fixture(s) to create and push dummy `.nupkg` artifacts.
*   **Steps:**
    1.  **Define Dummy Projects:** Create one or more minimal C# class library projects under a `tests/TestPackages/` solution folder (e.g., `TestPackageA`, `TestPackageB`) with specific versions defined in their `.csproj` files. Add these to the solution if not already present. (Note: The internal code content of these projects is irrelevant; only their packability and metadata (ID, Version) matter).
    2.  **Pack Projects:** Use `System.Diagnostics.Process` to execute `dotnet pack` for each dummy project, outputting the `.nupkg` files to a known temporary location (e.g., a unique subdirectory under `Path.GetTempPath()` or `TestContext.CurrentContext.WorkDirectory`). Check the process exit code for success.
    3.  **Locate Artifacts:** Identify the paths to the generated `.nupkg` files in the output directory.
    4.  **Push Artifacts:** Use `System.Diagnostics.Process` to execute `dotnet nuget push` for each `.nupkg`, targeting the dynamic BaGetter feed URL obtained from the Testcontainer instance. Include the API key defined during Testcontainer setup (e.g., define a constant `const string TestApiKey = "TEST_KEY";` in test setup, pass it via `.WithEnvironment("ApiKey", TestApiKey)`, and use the `-k {TestApiKey}` argument for `dotnet nuget push`). Check the process exit code for success.
    5.  **(Optional) Validation:** Add a step to query the BaGetter feed API (using `NuGet.Protocol`) to confirm the packages were successfully pushed and are listed.
*   **Relevant Files:**
    *   Dummy project `.csproj` files (e.g., `tests/TestPackages/TestPackageA/TestPackageA.csproj`) (To be created/defined).
    *   NUnit test fixture files where `[OneTimeSetUp]` is implemented (e.g., `NuGetClientCacheIntegrationTests.cs`).
*   **Error Handling:** If `dotnet pack` or `dotnet nuget push` commands fail during `[OneTimeSetUp]`, the expectation is that the setup will throw an exception, causing the NUnit test runner to fail the entire test fixture, preventing subsequent tests from running. Consider logging the `stdout` and `stderr` from the failed process to aid debugging. This is the desired behavior as the test environment is incomplete.
*   **Cleanup Note:** The temporary directory created for packed `.nupkg` files should be deleted recursively in the `[OneTimeTearDown]` method.
*   **Outcome:** Test setup logic that automatically generates and uploads necessary test NuGet packages to the ephemeral BaGetter instance before tests run.

### 7.5 Implement and Run Parsing Integration Tests

*   **Action:** Create `tests/NuGetContextMcpServer.Integration.Tests/ParsingIntegrationTests.cs`. Implement tests using the test host to resolve `ISolutionParser` and `IProjectParser` and run them against the `TestAssets`.
*   **Test Cases:**
    *   `SolutionParser_ValidSln_ReturnsCorrectProjectPaths`
    *   `SolutionParser_NonExistentSln_ReturnsEmpty`
    *   `SolutionParser_SlnWithMissingProject_ReturnsOnlyExistingProjectPaths`
    *   `ProjectParser_ValidCsproj_ReturnsCorrectPackageReferences`
    *   `ProjectParser_CsprojWithConditions_ReturnsCorrectPackageReferences` (Explicitly pass MSBuild global properties like `new Dictionary<string, string> { ["Configuration"] = "Debug" }` when evaluating the project via `IProjectParser` for this test case to ensure the condition is met).
    *   `ProjectParser_CsprojWithCPM_ReturnsCorrectPackageReferences`
    *   `ProjectParser_NonExistentCsproj_ReturnsEmpty`
    *   `ProjectParser_InvalidCsproj_ReturnsEmpty`
*   **Relevant Files:**
    *   Interfaces: `ISolutionParser.cs`, `IProjectParser.cs`
    *   Implementations: `MsBuildSolutionParser.cs`, `MsBuildProjectParser.cs`
    *   Test Assets: Files created in step 7.2.
*   **Instruction:** **Run these tests immediately after implementation** to validate the parsing infrastructure against the sample files.

### 7.6 Implement and Run NuGet Client & Cache Integration Tests

*   **Action:** Create `tests/NuGetContextMcpServer.Integration.Tests/NuGetClientCacheIntegrationTests.cs`. Implement tests using the test host (configured for local feed and test DB) to resolve `INuGetQueryService` and `ICacheService`.
*   **Test Cases:**
    *   `GetAllVersions_CacheMiss_FetchesFromLocalFeedAndPopulatesCache`
    *   `GetAllVersions_CacheHit_ReturnsFromCacheWithoutHittingFeed`
    *   `SearchPackages_CacheMiss_FetchesFromLocalFeedAndPopulatesCache`
    *   `SearchPackages_CacheHit_ReturnsFromCacheWithoutHittingFeed`
    *   `GetLatestVersion_UsesCachePopulatedByGetAllVersions`
    *   `QueryService_WithCredentials_AuthenticatesAgainstLocalFeed` (If applicable)
*   **Relevant Files:**
    *   Interfaces: `INuGetQueryService.cs`, `ICacheService.cs`
    *   Implementations: `NuGetClientWrapper.cs`, `SqliteCacheService.cs`
    *   Configuration: `NuGetSettings.cs`, `CacheSettings.cs`
*   **Instruction:** **Run these tests immediately after implementation**, ensuring the local NuGet feed is running and the test host is configured correctly to use it.

### 7.7 Implement and Run Application Service Integration Tests

*   **Action:** Create `tests/NuGetContextMcpServer.Integration.Tests/ProjectAnalysisServiceIntegrationTests.cs`. Implement tests using the test host (configured for local feed, test DB, and test assets) to resolve `IProjectAnalysisService`.
*   **Test Cases:**
    *   `AnalyzeProject_ValidCsproj_ReturnsCorrectAnalyzedDependencies`
    *   `AnalyzeProject_ValidSln_ReturnsCorrectAnalyzedDependenciesForAllProjects`
    *   `AnalyzeProject_ProjectWithUnknownPackage_ReturnsDependencyWithNullLatestVersions`
    *   `AnalyzeProject_HandlesMixedProjectTypesInSolution` (If applicable)
*   **Relevant Files:**
    *   Interface: `IProjectAnalysisService.cs`
    *   Implementation: `ProjectAnalysisService.cs`
    *   Dependencies: All previously tested interfaces/implementations.
*   **Instruction:** **Run these tests immediately after implementation** to validate the end-to-end analysis flow using real infrastructure components.

### 7.8 Implement and Run Cache Eviction Integration Tests

*   **Action:** Create `tests/NuGetContextMcpServer.Integration.Tests/CacheEvictionIntegrationTests.cs`. Implement tests using the test host to resolve `ICacheService`. Manually add expired/non-expired entries using `SetAsync`, then manually trigger the core eviction logic by calling `ICacheService.RemoveExpiredAsync()` directly. Verify the cache state using `GetAsync`. (Testing the `IHostedService` timer mechanism itself is out of scope; focus on the `RemoveExpiredAsync` logic it invokes).
*   **Test Cases:**
    *   `CacheEviction_RemovesExpiredEntry`
    *   `CacheEviction_DoesNotRemoveValidEntry`
*   **Relevant Files:**
    *   Interface: `ICacheService.cs`
    *   Implementations: `SqliteCacheService.cs`, `CacheEvictionService.cs`
*   **Instruction:** **Run these tests immediately after implementation** to validate the cache eviction mechanism.

---

**Conclusion:** By following this incremental approach – implementing a set of related integration tests and running them immediately – we can ensure that different parts of the application work together correctly and catch integration issues much earlier in the development cycle.