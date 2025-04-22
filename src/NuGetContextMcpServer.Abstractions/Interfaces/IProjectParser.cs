using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace

// Represents the result of parsing a package reference
// Moved ParsedPackageReference record here
public record ParsedPackageReference(string Id, string Version);

public interface IProjectParser
{
    Task<IEnumerable<ParsedPackageReference>> GetPackageReferencesAsync(string projectPath, CancellationToken cancellationToken);
    // Potentially add methods for other project details if needed later
}