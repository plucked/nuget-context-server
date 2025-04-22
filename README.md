# NuGet Context MCP Server

## Project Goal

This project aims to develop a C# application that functions as a **Model Context Protocol (MCP) server**. Its primary purpose is to provide context about .NET project dependencies, specifically focusing on NuGet packages, to Large Language Models (LLMs).

The server will:
*   Analyze `.sln` and `.csproj` files to extract NuGet package references.
*   Interact with NuGet feeds (public or private) to search for packages, retrieve version lists, and get metadata.
*   Cache NuGet API responses using an embedded **SQLite** database for performance and efficiency.
*   Expose these capabilities as **MCP tools** callable by LLM agents via **stdio** transport.

## Architecture

The server is built using a layered architecture with the **.NET Generic Host** as the foundation:
*   **Host Layer:** Manages application lifetime, DI, configuration, and MCP communication.
*   **Application Layer:** Contains service interfaces and orchestration logic.
*   **Infrastructure Layer:** Implements concrete details for parsing (using `Microsoft.Build`), NuGet interaction (using `NuGet.Protocol`), caching (using `Microsoft.Data.Sqlite`), and defines the MCP tools.

## Installation (Roocode)

To use this MCP server with Roocode:

1.  **Build the Server:** Ensure the .NET SDK (version specified in `global.json` or latest compatible) is installed. Build the server application by running the following command in the repository root:
   ```bash
   dotnet build
   ```

2.  **Configure Roocode:** Create or edit the project-level MCP configuration file at `.roo/mcp.json` in your workspace root. Add the following server configuration:

   ```json
   {
     "mcpServers": {
       "nuget-context": {
         "command": "dotnet",
         "args": [
           "run",
           "--project",
           "src/NuGetContextMcpServer.Host/NuGetContextMcpServer.Host.csproj",
           "--", // Separates dotnet run arguments from application arguments
           "--transport", // Specify the MCP transport mechanism
           "stdio"        // Use standard input/output for local communication
         ],
         "cwd": ".", // Ensure the command runs from the repository root
         "disabled": false // Ensure the server is enabled
       }
       // Add other MCP server configurations here if needed
     }
   }
   ```
   *   **Note:** This configuration assumes the server application (`NuGetContextMcpServer.Host`) accepts `--transport stdio` command-line arguments. If the server defaults to or only supports STDIO, these arguments might be removable.
   *   The `cwd` value assumes Roocode is opened with this repository as the root workspace folder.

3.  **Restart Roocode/VS Code:** After saving the `.roo/mcp.json` file, restart VS Code or reload the window to ensure Roocode picks up the new configuration. Roocode should then automatically start the server process when needed.

## Research (`deep-research/`)

This folder contains the initial research documents that informed the technical blueprint and design decisions:

*   `01.md`: Initial blueprint focusing on a C# server with ASP.NET Core Web API.
*   `02.md`: Introduced optional MCP integration and a recommended project layout.
*   `03.md`: Shifted focus to an MCP-first architecture using the .NET Generic Host and detailed NUnit/Moq testing strategy.
*   `04.md`: Evaluated embedded caching options (JSON files, SQLite, LiteDB), recommending SQLite or LiteDB. (Decision made to use SQLite).

## Implementation Plan (`implementation/`)

This folder contains a detailed, step-by-step plan for implementing the server:

*   **`task_01.md`:** Initial Solution and Project Setup (.NET 9, project structure, dependencies).
*   **`task_02.md`:** Interfaces, Configuration, and MSBuild Initialization (Service contracts, `appsettings.json`, User Secrets, `MsBuildInitializer`).
*   **`task_03.md`:** Implement Infrastructure Services (Parsing, SQLite Caching, NuGet Client Wrapper).
*   **`task_04.md`:** Implement Application Services and MCP Tools (Orchestration logic, MCP tool definitions).
*   **`task_05.md`:** Configure Host, DI, MCP Server, and Cache Eviction (Wiring everything together in `Program.cs`).
*   **`task_06.md`:** Implement Unit Tests (NUnit & Moq) (Writing tests for application and infrastructure layers).

## Current Status

**Planning Complete.** The architecture is defined, research reviewed, and a detailed implementation plan (Tasks 01-06) is documented. The project is ready to proceed with implementation, starting with Task 01.