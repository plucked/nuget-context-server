# Contributing to NuGet Context MCP Server

First off, thank you for considering contributing! Your help is appreciated.

## How Can I Contribute?

### Reporting Bugs

*   Ensure the bug was not already reported by searching on GitHub under [Issues](https://github.com/plucked/nuget-context-server/issues).
*   If you're unable to find an open issue addressing the problem, [open a new one](https://github.com/plucked/nuget-context-server/issues/new). Be sure to include a **title and clear description**, as much relevant information as possible, and a **code sample or an executable test case** demonstrating the expected behavior that is not occurring.

### Suggesting Enhancements

*   Open a new issue to start a discussion about your idea. Explain the enhancement you're proposing and why it would be beneficial.
*   Provide as much detail and context as possible.

### Pull Requests

1.  **Fork the repository** on GitHub.
2.  **Clone your fork** locally (`git clone https://github.com/YourUsername/nuget-context-server.git`).
3.  **Create a new branch** for your changes (`git checkout -b feature/your-feature-name` or `bugfix/issue-description`).
4.  **Make your changes.** Ensure you adhere to the existing coding style. Build the project (`dotnet build`) and ensure all tests pass (`dotnet test`).
5.  **Commit your changes** (`git commit -am 'Add some feature'`). Use clear and descriptive commit messages.
6.  **Push to your branch** (`git push origin feature/your-feature-name`).
7.  **Open a Pull Request** on the original `plucked/nuget-context-server` repository.
8.  Clearly describe the changes in the Pull Request description. Link any relevant issues.

## Development Setup

1.  Clone the repository.
2.  Ensure you have the required **.NET 9 SDK** installed.
3.  Build the solution using `dotnet build`.
4.  For running locally during development, you can use `dotnet watch run --project src/NuGetContextMcpServer.Host/NuGetContextMcpServer.Host.csproj -- --transport stdio`.

## Coding Style

Please try to follow the coding style conventions already present in the codebase. Consistency is key.

## Code of Conduct

This project adheres to a Code of Conduct. Please review the [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) file for details. By participating, you are expected to uphold this code. Please report unacceptable behavior.

Thank you for contributing!