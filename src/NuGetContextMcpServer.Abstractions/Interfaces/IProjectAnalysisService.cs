using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGetContextMcpServer.Abstractions.Dtos; // Updated using for DTOs

namespace NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace

// Removed placeholder DTO

public interface IProjectAnalysisService
{
    // Changed Mcp.AnalyzedDependency to just AnalyzedDependency (using updated namespace)
    Task<IEnumerable<AnalyzedDependency>> AnalyzeProjectAsync(string projectOrSolutionPath, CancellationToken cancellationToken);
}