# NuGet Context MCP Server

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A C# application that functions as a **[Model Context Protocol (MCP)](https://github.com/modelcontextprotocol/) server**, providing context about .NET project dependencies (NuGet packages) to Large Language Models (LLMs) or other development tools.

## Overview

This server analyzes .NET solutions (`.sln`) and projects (`.csproj`) to extract NuGet package information. It interacts with NuGet feeds to fetch package details, versions, and metadata, caching results locally using SQLite for improved performance. These capabilities are exposed as tools via the Model Context Protocol (MCP), allowing AI agents or other tools to query NuGet information programmatically.

## Features

The server exposes the following tools via MCP:

*   **`AnalyzeProjectDependencies`**: Analyzes a `.sln` or `.csproj` file to find NuGet dependencies and their latest available versions.
*   **`SearchNuGetPackages`**: Searches the configured NuGet feed for packages matching a search term, with options for pagination and including pre-release versions.
*   **`GetNuGetPackageVersions`**: Lists all available versions (stable or pre-release) for a specific package ID.
*   **`GetLatestNuGetPackageVersion`**: Gets the latest stable or pre-release version string for a specific package ID.
*   **`GetNuGetPackageDetails`**: Retrieves detailed metadata (description, authors, URLs, etc.) for a specific package ID and optional version.

## Prerequisites

*   **.NET 9 SDK** (or later compatible version)

## Installation & Build

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/plucked/nuget-context-server
    cd nuget-context-server
    ```
2.  **Build the server:**
    ```bash
    dotnet build --configuration Release
    ```
    The main executable will be within the `src/NuGetContextMcpServer.Host/bin/Release/net9.0/` directory (adjust path based on actual build output).

## Usage & Configuration

This server is designed to be run by an MCP client application (like an IDE extension). The client is responsible for starting the server process and communicating with it, typically via standard input/output (stdio).

**Configuration:**

The server uses `appsettings.json` (and environment-specific overrides like `appsettings.Development.json`) located in the `src/NuGetContextMcpServer.Host` directory for configuration:

*   **`NuGetSettings`**:
    *   `QueryFeedUrl`: The URL of the NuGet v3 feed index (defaults to `https://api.nuget.org/v3/index.json`).
    *   `Username` (optional): Username for authenticated feeds.
    *   `PasswordOrPat` (optional): Password or Personal Access Token (PAT) for authenticated feeds.
*   **`CacheSettings`**:
    *   `DatabasePath`: Path to the SQLite cache file (defaults to `nuget_cache.db` in the working directory).
    *   `DefaultExpirationMinutes`: Default cache duration in minutes (defaults to 60).
*   **Logging:** Configured via Serilog settings in `appsettings.json`. Logs are written to a file by default.

**Example MCP Client Configuration (using stdio):**

An MCP client would typically need configuration similar to this (syntax may vary based on the client):

```json
{
  "mcpServers": {
    "nuget-context": {
      "command": "dotnet",
      "args": [
        "watch",
        "run",
        "--non-interactive",
        "--project",
        "src/NuGetContextMcpServer.Host/NuGetContextMcpServer.Host.csproj",
        "--",
        "--transport", "stdio"
      ],
      "cwd": ".",
      "disabled": false
    }
  }
}
```

*   **`command`**: The executable to run (`dotnet`).
*   **`args`**: Arguments passed to the command. Uses `dotnet watch run` for development to automatically restart the server on file changes. The `--transport stdio` argument tells the server to use standard I/O for MCP communication. For production or non-watch scenarios, replace `"watch"` with `"run"` and remove `"--non-interactive"`.
*   **`cwd`**: The working directory from which the command should be run (usually the repository root).

## Architecture

The server uses a layered architecture built on the .NET Generic Host:

*   **Host:** Manages application lifetime, Dependency Injection (DI), configuration, logging, and MCP communication setup.
*   **Application:** Defines service interfaces and contains core application logic and MCP tool definitions.
*   **Infrastructure:** Provides concrete implementations for:
    *   NuGet feed interaction (`NuGet.Protocol`).
    *   Project/Solution parsing (`Microsoft.Build`).
    *   Caching (`Microsoft.Data.Sqlite`).

## Contributing

Contributions are welcome! Please refer to the [contribution guidelines](CONTRIBUTING.md) for more details.

## License

This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for details.