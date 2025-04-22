using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace

public interface ISolutionParser
{
    Task<IEnumerable<string>> GetProjectPathsAsync(string solutionPath, CancellationToken cancellationToken);
}