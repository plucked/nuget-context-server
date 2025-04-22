# Task 01: Initial Solution and Project Setup

**Goal:** Establish the foundational solution and project structure for the NuGet Context MCP Server, targeting .NET 9.

**Outcome:** A correctly structured Visual Studio solution (`.sln`) containing the necessary projects (`.csproj`) with the target framework set to `net9.0`, initial NuGet package dependencies added, and project references configured according to the agreed architecture.

---

## Sub-Tasks:

### 1.1 Create Solution File
*   **Action:** Create a new blank solution file named `NuGetContextMcpServer.sln` in the root directory (`/Users/plucked/Development/nuget-context-server`).
*   **Command (Example):** `dotnet new sln -n NuGetContextMcpServer`
*   **Outcome:** `NuGetContextMcpServer.sln` file exists in the root directory.

### 1.2 Create Host Project (`NuGetContextMcpServer.Host`)
*   **Action:** Create a new .NET 9 Console Application project named `NuGetContextMcpServer.Host` within a `src` subfolder. Add it to the solution.
*   **Command (Example):**
    ```bash
    mkdir src
    dotnet new console -n NuGetContextMcpServer.Host -o src/NuGetContextMcpServer.Host -f net9.0
    dotnet sln NuGetContextMcpServer.sln add src/NuGetContextMcpServer.Host/NuGetContextMcpServer.Host.csproj
    ```
*   **Outcome:** `src/NuGetContextMcpServer.Host/NuGetContextMcpServer.Host.csproj` exists, targets `net9.0`, and is part of the solution.

### 1.3 Create Application Project (`NuGetContextMcpServer.Application`)
*   **Action:** Create a new .NET 9 Class Library project named `NuGetContextMcpServer.Application` within the `src` subfolder. Add it to the solution.
*   **Command (Example):**
    ```bash
    dotnet new classlib -n NuGetContextMcpServer.Application -o src/NuGetContextMcpServer.Application -f net9.0
    dotnet sln NuGetContextMcpServer.sln add src/NuGetContextMcpServer.Application/NuGetContextMcpServer.Application.csproj
    ```
*   **Outcome:** `src/NuGetContextMcpServer.Application/NuGetContextMcpServer.Application.csproj` exists, targets `net9.0`, and is part of the solution.

### 1.4 Create Infrastructure Project (`NuGetContextMcpServer.Infrastructure`)
*   **Action:** Create a new .NET 9 Class Library project named `NuGetContextMcpServer.Infrastructure` within the `src` subfolder. Add it to the solution.
*   **Command (Example):**
    ```bash
    dotnet new classlib -n NuGetContextMcpServer.Infrastructure -o src/NuGetContextMcpServer.Infrastructure -f net9.0
    dotnet sln NuGetContextMcpServer.sln add src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj
    ```
*   **Outcome:** `src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj` exists, targets `net9.0`, and is part of the solution.

### 1.5 Create Test Projects
*   **Action:** Create two NUnit test projects (`.Application.Tests`, `.Infrastructure.Tests`) targeting .NET 9 within a `tests` subfolder. Add them to the solution.
*   **Command (Example):**
    ```bash
    mkdir tests
    dotnet new nunit -n NuGetContextMcpServer.Application.Tests -o tests/NuGetContextMcpServer.Application.Tests -f net9.0
    dotnet sln NuGetContextMcpServer.sln add tests/NuGetContextMcpServer.Application.Tests/NuGetContextMcpServer.Application.Tests.csproj
    dotnet new nunit -n NuGetContextMcpServer.Infrastructure.Tests -o tests/NuGetContextMcpServer.Infrastructure.Tests -f net9.0
    dotnet sln NuGetContextMcpServer.sln add tests/NuGetContextMcpServer.Infrastructure.Tests/NuGetContextMcpServer.Infrastructure.Tests.csproj
    ```
*   **Outcome:** Test projects exist in the `tests` folder, target `net9.0`, reference NUnit, and are part of the solution.

### 1.6 Establish Project References
*   **Action:** Configure the necessary project dependencies based on the architecture:
    *   `Host` depends on `Application` and `Infrastructure`.
    *   `Application` depends on `Infrastructure`.
    *   `Application.Tests` depends on `Application`.
    *   `Infrastructure.Tests` depends on `Infrastructure`.
*   **Command (Example):**
    ```bash
    dotnet add src/NuGetContextMcpServer.Host/NuGetContextMcpServer.Host.csproj reference src/NuGetContextMcpServer.Application/NuGetContextMcpServer.Application.csproj
    dotnet add src/NuGetContextMcpServer.Host/NuGetContextMcpServer.Host.csproj reference src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj
    dotnet add src/NuGetContextMcpServer.Application/NuGetContextMcpServer.Application.csproj reference src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj
    dotnet add tests/NuGetContextMcpServer.Application.Tests/NuGetContextMcpServer.Application.Tests.csproj reference src/NuGetContextMcpServer.Application/NuGetContextMcpServer.Application.csproj
    dotnet add tests/NuGetContextMcpServer.Infrastructure.Tests/NuGetContextMcpServer.Infrastructure.Tests.csproj reference src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj
    ```
*   **Outcome:** `.csproj` files are updated with the correct `<ProjectReference>` tags.

### 1.7 Add Initial NuGet Packages
*   **Action:** Add the core NuGet packages identified in the plan to the relevant projects.
*   **Command (Example):**
    ```bash
    # Host Project
    dotnet add src/NuGetContextMcpServer.Host/NuGetContextMcpServer.Host.csproj package Microsoft.Extensions.Hosting
    dotnet add src/NuGetContextMcpServer.Host/NuGetContextMcpServer.Host.csproj package ModelContextProtocol --prerelease

    # Infrastructure Project
    dotnet add src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj package Microsoft.Build.Locator
    dotnet add src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj package Microsoft.Build --version 17.10.4 # Specify a recent stable version
    dotnet add src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj package NuGet.Protocol --version 6.10.1 # Specify a recent stable version
    dotnet add src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj package NuGet.Versioning --version 6.10.1
    dotnet add src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj package NuGet.Configuration --version 6.10.1
    dotnet add src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj package NuGet.Credentials --version 6.10.1
    dotnet add src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj package NuGet.Common --version 6.10.1
    dotnet add src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj package Microsoft.Data.Sqlite --version 9.0.0-preview.3.24172.4 # Use .NET 9 preview version
    dotnet add src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj package Microsoft.Extensions.Caching.Memory # For potential future use or comparison
    dotnet add src/NuGetContextMcpServer.Infrastructure/NuGetContextMcpServer.Infrastructure.csproj package Microsoft.Extensions.Options.ConfigurationExtensions # Needed for IOptions

    # Test Projects
    dotnet add tests/NuGetContextMcpServer.Application.Tests/NuGetContextMcpServer.Application.Tests.csproj package Moq
    dotnet add tests/NuGetContextMcpServer.Infrastructure.Tests/NuGetContextMcpServer.Infrastructure.Tests.csproj package Moq
    ```
*   **Outcome:** `.csproj` files are updated with the specified `<PackageReference>` tags. Solution is ready for building.

---
*Note: Specific package versions might need adjustment based on compatibility or later releases. The commands provided are examples; execution might vary slightly depending on the shell environment.*