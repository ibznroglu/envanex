using Shouldly;

namespace Envanex.Domain.Tests;

public class ArchitectureTests
{
    private static readonly string[] ExpectedApplicationReferences = ["Envanex.Domain"];
    private static readonly string[] ExpectedInfrastructureReferences = ["Envanex.Application", "Envanex.Domain"];
    private static readonly string[] ExpectedSoapApiReferences = ["Envanex.Application"];

    [Fact]
    public void Domain_ShouldNotReference_AnyProject()
    {
        var references = GetProjectReferences("Envanex.Domain");

        references.ShouldBeEmpty();
    }

    [Fact]
    public void Application_ShouldOnlyReference_Domain()
    {
        var references = GetProjectReferences("Envanex.Application");

        references.ShouldBe(ExpectedApplicationReferences);
    }

    [Fact]
    public void Infrastructure_ShouldOnlyReference_DomainAndApplication()
    {
        var references = GetProjectReferences("Envanex.Infrastructure");

        references.ShouldBe(ExpectedInfrastructureReferences);
    }

    [Fact]
    public void SoapApi_ShouldOnlyReference_Application()
    {
        var references = GetProjectReferences("Envanex.SoapApi");

        references.ShouldBe(ExpectedSoapApiReferences);
    }

    /// <summary>
    /// Reads the .csproj file from disk and extracts project names (directory names)
    /// from ProjectReference elements, returning them in alphabetical order.
    /// </summary>
    private static string[] GetProjectReferences(string projectName)
    {
        var solutionDir = FindSolutionDirectory();
        var csprojPath = Path.Combine(solutionDir, "src", projectName, $"{projectName}.csproj");

        var csprojContent = File.ReadAllText(csprojPath);

        return System.Text.RegularExpressions.Regex
            .Matches(csprojContent, @"<ProjectReference\s+Include=""[^""]*[/\\]([^/\\""]+)[/\\][^/\\""]+\.csproj""")
            .Select(m => m.Groups[1].Value)
            .Order()
            .ToArray();
    }

    private static string FindSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (dir.GetFiles("Envanex.slnx").Length > 0
                || dir.GetFiles("Envanex.sln").Length > 0)
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Envanex.sln or Envanex.slnx not found. The test must run from within the solution directory.");
    }
}
