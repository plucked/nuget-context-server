# Task 06: Implement Unit Tests (NUnit & Moq)

**Goal:** Implement unit tests for the core application and infrastructure services to ensure their logic functions correctly in isolation and handle various scenarios gracefully.

**Outcome:** A suite of NUnit tests using Moq for mocking, providing good code coverage for the `Application` services and testable parts of the `Infrastructure` layer. This increases confidence in the server's reliability before integration or end-to-end testing.

---

## Sub-Tasks:

### 6.1 Unit Test `PackageSearchService` (`Application.Tests` Project)
*   **Action:** Create `PackageSearchServiceTests.cs`. Write NUnit tests using Moq to mock `INuGetQueryService`.
*   **Test Scenarios:**
    *   `SearchPackagesAsync_ValidTerm_ReturnsResultsFromQueryService`: Verify the service correctly calls `_nugetQueryService.SearchPackagesAsync` and returns its results.
    *   `SearchPackagesAsync_QueryServiceThrowsException_ReturnsEmptyAndLogsError`: Verify that if `_nugetQueryService` throws an exception, the service catches it, logs an error (using a mocked `ILogger`), and returns an empty enumerable.
    *   `SearchPackagesAsync_HandlesDifferentPrereleaseFlags`: Verify the `includePrerelease` flag is passed correctly to the query service.
*   **Outcome:** Unit tests for `PackageSearchService` covering success and error paths.

### 6.2 Unit Test `PackageVersionService` (`Application.Tests` Project)
*   **Action:** Create `PackageVersionServiceTests.cs`. Write NUnit tests using Moq to mock `INuGetQueryService`.
*   **Test Scenarios:**
    *   `GetPackageVersionsAsync_StableOnly_FiltersPrereleaseAndReturnsSorted`: Verify correct call to `GetAllVersionsAsync`, filtering of pre-release versions, and descending sort order.
    *   `GetPackageVersionsAsync_IncludePrerelease_ReturnsAllSorted`: Verify correct call and descending sort order without filtering.
    *   `GetPackageVersionsAsync_QueryServiceThrows_ReturnsEmptyAndLogsError`: Verify error handling and logging.
    *   `GetLatestPackageVersionAsync_Stable_CallsCorrectQueryServiceMethod`: Verify `GetLatestStableVersionAsync` is called when `includePrerelease` is false.
    *   `GetLatestPackageVersionAsync_Prerelease_CallsCorrectQueryServiceMethod`: Verify `GetLatestVersionAsync` is called when `includePrerelease` is true.
    *   `GetLatestPackageVersionAsync_PackageNotFound_ReturnsNull`: Verify null is returned if the query service methods return null.
    *   `GetLatestPackageVersionAsync_QueryServiceThrows_ReturnsNullAndLogsError`: Verify error handling.
*   **Outcome:** Unit tests for `PackageVersionService` covering different scenarios for version listing and latest version retrieval.

### 6.3 Unit Test `ProjectAnalysisService` (`Application.Tests` Project)
*   **Action:** Create `ProjectAnalysisServiceTests.cs`. Write NUnit tests using Moq to mock `ISolutionParser`, `IProjectParser`, and `INuGetQueryService`.
*   **Test Scenarios:**
    *   `AnalyzeProjectAsync_ValidSlnPath_CallsSolutionAndProjectParsers`: Verify `_solutionParser.GetProjectPathsAsync` is called, and for each returned path, `_projectParser.GetPackageReferencesAsync` is called.
    *   `AnalyzeProjectAsync_ValidCsprojPath_CallsProjectParserDirectly`: Verify `_projectParser.GetPackageReferencesAsync` is called directly.
    *   `AnalyzeProjectAsync_InvalidPath_ReturnsEmptyAndLogsError`: Verify handling of non .sln/.csproj paths.
    *   `AnalyzeProjectAsync_FileNotFound_ReturnsEmptyAndLogsError`: Verify handling if the initial path or project paths from solution don't exist.
    *   `AnalyzeProjectAsync_ParsesReferences_CallsNuGetQueryServiceForEachRef`: Verify `GetLatestStableVersionAsync` and `GetLatestVersionAsync` are called on `_nugetQueryService` for each package reference found by the parser.
    *   `AnalyzeProjectAsync_AggregatesResultsCorrectly`: Verify the final `AnalyzedDependency` list contains the correct requested and latest versions based on mocked parser and query service results.
    *   `AnalyzeProjectAsync_ParserOrQueryServiceThrows_HandlesErrorAndLogs`: Verify graceful error handling if downstream services throw exceptions.
*   **Outcome:** Unit tests for `ProjectAnalysisService` covering solution/project path handling, dependency aggregation, and error scenarios.

### 6.4 Unit Test `SqliteCacheService` (`Infrastructure.Tests` Project)
*   **Action:** Create `SqliteCacheServiceTests.cs`. Testing SQLite directly in unit tests can be tricky. Focus on logic *around* the database interaction or use an in-memory SQLite database for testing.
*   **Test Scenarios (using In-Memory SQLite):**
    *   Setup: Use `Data Source=:memory:` connection string and `_connection.Open()` in test setup to create a temporary in-memory database for each test. Ensure `InitializeDatabase` is called.
    *   `SetAsync_GetAsync_ReturnsCorrectValueBeforeExpiration`: Verify setting and retrieving a value works.
    *   `GetAsync_ExpiredItem_ReturnsNull`: Verify `GetAsync` returns null after the expiration time has passed.
    *   `RemoveExpiredAsync_RemovesExpiredItemsOnly`: Verify `RemoveExpiredAsync` correctly deletes expired items but leaves valid ones.
    *   `SetAsync_ExistingKey_OverwritesValue`: Verify `INSERT OR REPLACE` logic works.
    *   `GetAsync_InvalidJson_ReturnsNullAndRemovesEntry`: (Harder to test reliably without corrupting DB) Mock `JsonSerializer` or test the behavior if deserialization fails.
    *   `GetAsync_NonExistentKey_ReturnsNull`: Verify cache miss behavior.
*   **Alternative (Mocking `SqliteConnection`/`Command`):** This is complex and often not recommended due to the intricacies of mocking ADO.NET. Prefer in-memory testing.
*   **Outcome:** Unit tests for `SqliteCacheService` verifying core caching operations (get, set, expiration, eviction) using an in-memory SQLite database.

### 6.5 Unit Test `NuGetClientWrapper` (`Infrastructure.Tests` Project)
*   **Action:** Create `NuGetClientWrapperTests.cs`. Mock `ICacheService` and potentially parts of the NuGet SDK if feasible, or focus on the caching logic.
*   **Test Scenarios:**
    *   `SearchPackagesAsync_CacheHit_ReturnsCachedDataWithoutApiCall`: Mock `_cacheService.GetAsync` to return data. Verify NuGet SDK resources (`PackageSearchResource`) are *not* requested.
    *   `SearchPackagesAsync_CacheMiss_CallsApiAndSetsCache`: Mock `_cacheService.GetAsync` to return null. Mock NuGet SDK resources to return data. Verify `_cacheService.SetAsync` is called with the fetched data.
    *   `GetAllVersionsAsync_CacheHit_ReturnsCachedData`: Verify cache hit logic for versions.
    *   `GetAllVersionsAsync_CacheMiss_CallsApiAndSetsCache`: Verify cache miss logic for versions.
    *   `GetLatestStableVersionAsync_UsesGetAllVersions`: Verify it leverages `GetAllVersionsAsync` (and thus its caching).
    *   `GetLatestVersionAsync_UsesGetAllVersions`: Verify it leverages `GetAllVersionsAsync`.
    *   `CreateSourceRepository_WithCredentials_SetsCredentialsOnPackageSource`: Verify credential handling logic (might require inspecting the created `SourceRepository` or mocking `Repository.Factory`).
*   **Outcome:** Unit tests for `NuGetClientWrapper` focusing primarily on the interaction with the cache service and basic credential setup logic.

---
*Note: Writing comprehensive tests, especially for infrastructure components interacting with external libraries like MSBuild or ADO.NET, requires careful consideration of mocking strategies versus integration testing.*