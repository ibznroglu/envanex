using Shouldly;

namespace Envanex.Domain.Tests;

public class ArchitectureTests
{
    private static readonly string[] ExpectedApplicationReferences = ["Envanex.Domain"];
    private static readonly string[] ExpectedInfrastructureReferences = ["Envanex.Application", "Envanex.Domain"];
    private static readonly string[] ExpectedSoapApiReferences = ["Envanex.Application"];
    private static readonly string[] ExpectedWebReferences = ["Envanex.Application", "Envanex.Infrastructure", "Envanex.SoapApi"];

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

    [Fact]
    public void Application_ShouldNotReference_EntityFrameworkPackages()
    {
        var csprojContent = ReadCsproj("Envanex.Application");

        var efPackageReferences = MatchPackageReferences(csprojContent, @"Microsoft\.EntityFrameworkCore");

        efPackageReferences.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(@"Microsoft\.AspNetCore\.Identity")]
    [InlineData(@"Microsoft\.AspNetCore\.Authentication")]
    [InlineData(@"Microsoft\.IdentityModel")]
    [InlineData(@"System\.IdentityModel")]
    public void Application_ShouldNotReference_AuthenticationPackages(string packageNamePattern)
    {
        // The EntityFrameworkCore regex matches neither Microsoft.AspNetCore.Identity.EntityFrameworkCore
        // nor Microsoft.AspNetCore.Authentication.JwtBearer, so auth packages need their own patterns.
        var csprojContent = ReadCsproj("Envanex.Application");

        var authPackageReferences = MatchPackageReferences(csprojContent, packageNamePattern);

        authPackageReferences.ShouldBeEmpty();
    }

    [Fact]
    public void ForbiddenPackageMatcher_ShouldMatch_RealAuthPackageNames()
    {
        // Without this, a broken matcher passes Application_ShouldNotReference_AuthenticationPackages
        // by matching nothing at all.
        const string SyntheticCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
                <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
              </ItemGroup>
            </Project>
            """;

        var identityMatches = MatchPackageReferences(SyntheticCsproj, @"Microsoft\.AspNetCore\.Identity");
        var authenticationMatches = MatchPackageReferences(SyntheticCsproj, @"Microsoft\.AspNetCore\.Authentication");

        identityMatches.ShouldBe(["Microsoft.AspNetCore.Identity.EntityFrameworkCore"]);
        authenticationMatches.ShouldBe(["Microsoft.AspNetCore.Authentication.JwtBearer"]);
    }

    [Fact]
    public void Infrastructure_ShouldReference_IdentityEntityFrameworkCore()
    {
        var csprojContent = ReadCsproj("Envanex.Infrastructure");

        var matches = MatchPackageReferences(csprojContent, @"Microsoft\.AspNetCore\.Identity\.EntityFrameworkCore");

        matches.ShouldBe(["Microsoft.AspNetCore.Identity.EntityFrameworkCore"]);
    }

    [Fact]
    public void Web_ShouldReference_JwtBearerPackage()
    {
        // The JWT bearer scheme is validated in the host only. Keeping the package out of
        // Application and Infrastructure is what keeps Infrastructure free of a
        // Microsoft.AspNetCore.App framework reference.
        var csprojContent = ReadCsproj("Envanex.Web");

        var matches = MatchPackageReferences(csprojContent, @"Microsoft\.AspNetCore\.Authentication\.JwtBearer");

        matches.ShouldBe(["Microsoft.AspNetCore.Authentication.JwtBearer"]);
    }

    [Fact]
    public void Web_ShouldOnlyReference_ApplicationInfrastructureAndSoapApi()
    {
        var references = GetProjectReferences("Envanex.Web");

        references.ShouldBe(ExpectedWebReferences);
    }

    [Fact]
    public void DataSourceListDtos_ShouldNotBePositionalRecords()
    {
        var solutionDir = FindSolutionDirectory();
        var applicationDir = Path.Combine(solutionDir, "src", "Envanex.Application");

        var listDtoFiles = Directory
            .GetFiles(applicationDir, "*ListDto.cs", SearchOption.AllDirectories)
            .ToArray();

        listDtoFiles.ShouldNotBeEmpty(
            $"No *ListDto.cs files found under {applicationDir} — " +
            "check that the path is correct so this test does not silently pass.");

        var positionalRecordPattern = new System.Text.RegularExpressions.Regex(
            @"\brecord\s+\w+\s*\(");

        var violatingFiles = listDtoFiles
            .Where(f => positionalRecordPattern.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToArray();

        violatingFiles.ShouldBeEmpty(
            "Positional records produce NewExpression which EF Core DataSourceLoader " +
            "cannot translate OrderBy over — query falls back to client evaluation. " +
            $"Violating files: {string.Join(", ", violatingFiles)}");
    }

    /// <summary>
    /// The single package-name matcher. Extracted so the negative control
    /// (<see cref="ForbiddenPackageMatcher_ShouldMatch_RealAuthPackageNames"/>) runs the same
    /// regex the package rules run, rather than a copy of it.
    /// </summary>
    private static string[] MatchPackageReferences(string csprojContent, string packageNamePattern)
    {
        return System.Text.RegularExpressions.Regex
            .Matches(csprojContent, $@"<PackageReference\s+Include=""([^""]*{packageNamePattern}[^""]*)""")
            .Select(m => m.Groups[1].Value)
            .ToArray();
    }

    private static string ReadCsproj(string projectName)
    {
        var solutionDir = FindSolutionDirectory();
        var csprojPath = Path.Combine(solutionDir, "src", projectName, $"{projectName}.csproj");

        return File.ReadAllText(csprojPath);
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
